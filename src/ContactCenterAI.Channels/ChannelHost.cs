using System.Threading.RateLimiting;
using System.Text.Json;
using ContactCenterAI.Application;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;

namespace ContactCenterAI.Channels;

public static class ChannelHost
{
    // Port zero is reserved for isolated HTTP tests; runtime always uses loopback:7454.
    public static WebApplication Build(ChannelStore store, EndpointOptions? meta, int port = 7454)
    {
        meta?.Validate();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Production" });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server =>
        {
            server.AddServerHeader = false;
            server.Limits.MaxRequestBodySize = ProviderInput.MaximumBody;
            server.Listen(System.Net.IPAddress.Loopback, port);
        });
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddFixedWindowLimiter("meta", limit =>
            {
                limit.PermitLimit = 60; limit.Window = TimeSpan.FromMinutes(1);
                limit.QueueLimit = 0; limit.AutoReplenishment = true;
            });
        });
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            try { await next(context); }
            catch (Exception error) when (error is RequestRejected or JsonException or KeyNotFoundException or InvalidOperationException or SqliteException or BadHttpRequestException)
            {
                if (context.Response.HasStarted) { context.Abort(); return; }
                context.Response.Clear();
                context.Response.StatusCode = error switch { RequestRejected rejected => rejected.Status, SqliteException => 503, BadHttpRequestException bad => bad.StatusCode, _ => 400 };
                await context.Response.WriteAsJsonAsync(new { code = error is SqliteException ? "CHANNEL_STORE_UNAVAILABLE" : "CHANNEL_REQUEST_REJECTED" });
            }
        });
        app.UseRateLimiter();
        app.MapGet("/health/live", () => Results.Json(new { alive = true }));
        app.MapGet("/webhooks/whatsapp", (HttpRequest request) =>
        {
            if (meta is null) return Results.StatusCode(503);
            var challenge = request.Query["hub.challenge"].ToString();
            if (request.Query["hub.mode"] != "subscribe" || challenge.Length is < 1 or > 200 ||
                !ProviderInput.VerifyChallenge(request.Query["hub.verify_token"], meta.VerifyToken)) return Results.StatusCode(403);
            return Results.Text(challenge, "text/plain");
        }).RequireRateLimiting("meta");
        app.MapPost("/webhooks/whatsapp", async (HttpRequest request, CancellationToken ct) =>
        {
            if (meta is null) return Results.StatusCode(503);
            if (request.ContentLength > ProviderInput.MaximumBody) return Results.StatusCode(413);
            using var body = new MemoryStream(); var buffer = new byte[8192]; int read;
            while ((read = await request.Body.ReadAsync(buffer, ct)) > 0)
            {
                if (body.Length + read > ProviderInput.MaximumBody) return Results.StatusCode(413);
                await body.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            var raw = body.ToArray();
            if (!ProviderInput.VerifyMeta(raw, request.Headers["X-Hub-Signature-256"], meta.AppSecret)) return Results.StatusCode(401);
            using var document = ProviderInput.Parse(raw);
            var inputs = ProviderInput.Meta(document.RootElement, meta.EndpointId, DateTimeOffset.UtcNow);
            foreach (var input in inputs) _ = store.Accept(input, meta);
            if (document.RootElement.GetProperty("object").GetString() == "whatsapp_business_account")
                store.MetaStatuses(document.RootElement, meta.EndpointId);
            return Results.Ok(); // Only a durable acknowledgment, never an inference result.
        }).RequireRateLimiting("meta");
        return app;
    }
}
