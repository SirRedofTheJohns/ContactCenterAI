using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
namespace ContactCenterAI.Simulator;

public static class SourceApi
{
    public static WebApplication Build(WebApplicationBuilder builder, ReservationStore store)
    {
        var key = builder.Configuration["CCAI_SOURCE_SERVICE_KEY"];
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("SOURCE_KEY_REQUIRED");
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16384);
        builder.Services.ConfigureHttpJsonOptions(options =>
        { options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow; options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)); });
        builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        var keyHash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        var app = builder.Build();
        app.Use(async (http, next) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (http.Request.Host.ToString() != "127.0.0.1:7453" || http.Connection.RemoteIpAddress is { } address && !IPAddress.IsLoopback(address))
            { await Error(http, 403, "LOCAL_SOURCE_ONLY"); return; }
            if (http.Request.Path.StartsWithSegments("/source"))
            {
                var provided = http.Request.Headers["X-Service-Key"];
                if (provided.Count != 1 || !CryptographicOperations.FixedTimeEquals(keyHash, SHA256.HashData(Encoding.UTF8.GetBytes(provided.ToString()))))
                { await Error(http, 401, "SERVICE_AUTH_REQUIRED"); return; }
                var member = http.Request.Headers["X-Member-Ref"];
                if (member.Count != 1 || string.IsNullOrWhiteSpace(member[0]) || member[0]!.Length > 40)
                { await Error(http, 403, "MEMBER_CONTEXT_REQUIRED"); return; }
            }
            try { await next(http); }
            catch (SourceRejected failure) { await Error(http, failure.Status, failure.Code); }
            catch (SqliteException) { await Error(http, 503, "SOURCE_UNAVAILABLE"); }
            catch (BadHttpRequestException) { await Error(http, 400, "INVALID_REQUEST"); }
        });
        app.MapGet("/health/live", () => Results.Ok(new { status = "Alive", component = "ReservationSource" }));
        app.MapGet("/source/reservations", (HttpContext http) => Results.Ok(store.List(http.Request.Headers["X-Member-Ref"].ToString())));
        app.MapGet("/source/reservations/{id}", (string id, HttpContext http) => Results.Ok(store.Get(http.Request.Headers["X-Member-Ref"].ToString(), id)));
        app.MapPost("/source/commands/cancel", (CancelSourceCommand request, HttpContext http) => Results.Ok(store.Cancel(http.Request.Headers["X-Member-Ref"].ToString(), request)));
        app.MapGet("/source/commands/{id:guid}", (Guid id, HttpContext http) =>
        {
            var receipt = store.Receipt(http.Request.Headers["X-Member-Ref"].ToString(), id);
            return receipt is null ? Results.NotFound(new { code = "RESOURCE_NOT_FOUND" }) : Results.Ok(receipt);
        });
        return app;
    }
    private static Task Error(HttpContext http, int status, string code) => Results.Problem(statusCode: status, title: code,
        extensions: new Dictionary<string, object?> { ["code"] = code }).ExecuteAsync(http);
}
