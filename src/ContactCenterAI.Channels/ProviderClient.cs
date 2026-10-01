using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ContactCenterAI.Application;

namespace ContactCenterAI.Channels;

public sealed record TransportResult(string Status, string? ProviderId = null, int RetryAfterSeconds = 0);
public sealed class ProviderClient : IDisposable
{
    private readonly HttpClient client;
    public EndpointOptions Options { get; }
    public ProviderClient(EndpointOptions options, HttpMessageHandler? fixture = null)
    {
        options.Validate(); Options = options;
        client = new(fixture ?? new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(35) };
    }
    private Uri TelegramUri(string method) => new("https://api.telegram.org/bot" + Options.AccessToken + "/" + method);
    private static async Task<JsonDocument> Read(HttpResponseMessage response, CancellationToken ct)
    {
        using var input = await response.Content.ReadAsStreamAsync(ct); using var output = new MemoryStream(); var bytes = new byte[8192];
        int count;
        while ((count = await input.ReadAsync(bytes, ct)) > 0)
        { if (output.Length + count > ProviderInput.MaximumBody) throw new RequestRejected(503, "PROVIDER_RESPONSE_TOO_LARGE"); await output.WriteAsync(bytes.AsMemory(0, count), ct); }
        return ProviderInput.Parse(output.ToArray());
    }
    public async Task CheckTelegramAsync(CancellationToken ct)
    {
        if (Options.Channel != "telegram") throw new RequestRejected(503, "CHANNEL_CONFIG_REQUIRED");
        using var meResponse = await client.GetAsync(TelegramUri("getMe"), ct); using var me = await Read(meResponse, ct);
        if (!meResponse.IsSuccessStatusCode || me.RootElement.GetProperty("ok").ValueKind != JsonValueKind.True ||
            me.RootElement.GetProperty("result").GetProperty("id").GetInt64().ToString(CultureInfo.InvariantCulture) != Options.EndpointId ||
            me.RootElement.GetProperty("result").GetProperty("is_bot").ValueKind != JsonValueKind.True)
            throw new RequestRejected(503, "TELEGRAM_IDENTITY_REJECTED");
        using var hookResponse = await client.GetAsync(TelegramUri("getWebhookInfo"), ct); using var hook = await Read(hookResponse, ct);
        if (!hookResponse.IsSuccessStatusCode || hook.RootElement.GetProperty("ok").ValueKind != JsonValueKind.True) throw new RequestRejected(503, "TELEGRAM_RESPONSE_REJECTED");
        if (!string.IsNullOrEmpty(hook.RootElement.GetProperty("result").GetProperty("url").GetString())) throw new RequestRejected(503, "TELEGRAM_WEBHOOK_CONFLICT");
    }
    public async Task<IReadOnlyList<ProviderUpdate>> PollAsync(long offset, TimeProvider clock, CancellationToken ct)
    {
        if (Options.Channel != "telegram" || offset < 0) throw new RequestRejected(503, "CHANNEL_CONFIG_REQUIRED");
        using var response = await client.PostAsJsonAsync(TelegramUri("getUpdates"), new { offset, timeout = 25, limit = 100, allowed_updates = new[] { "message" } }, ct);
        if (!response.IsSuccessStatusCode) throw new RequestRejected(503, "TELEGRAM_POLL_FAILED");
        using var document = await Read(response, ct); return ProviderInput.Telegram(document.RootElement, Options.EndpointId, clock.GetUtcNow());
    }
    public async Task<TransportResult> SendAsync(ChannelOutput output, DateTimeOffset now, CancellationToken ct)
    {
        var route = output.Route;
        if (route.Channel != Options.Channel || route.EndpointId != Options.EndpointId || !Options.Recipients.Contains(route.SenderId) ||
            route.Epoch != output.Epoch || route.Channel == "whatsapp" && now >= route.LastInbound.AddHours(24))
            return new("Suppressed");
        var reply = output.Output.Reply;
        var text = reply.Text + string.Concat(reply.Citations.Select(c => $"\n[{c.DocumentId} v{c.Version}, {c.SectionId}] {c.Title}"));
        if (text.EnumerateRunes().Count() > 3500) return new("Failed");
        using var request = Options.Channel == "telegram"
            ? new HttpRequestMessage(HttpMethod.Post, TelegramUri("sendMessage")) { Content = JsonContent.Create(new { chat_id = route.SenderId, text, allow_paid_broadcast = false, link_preview_options = new { is_disabled = true } }) }
            : new HttpRequestMessage(HttpMethod.Post, new Uri("https://graph.facebook.com/" + Options.ApiVersion + "/" + Options.EndpointId + "/messages"))
            { Content = JsonContent.Create(new { messaging_product = "whatsapp", recipient_type = "individual", to = route.SenderId, type = "text", text = new { body = text, preview_url = false }, biz_opaque_callback_data = output.Id.ToString("D") }) };
        if (Options.Channel == "whatsapp") request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Options.AccessToken);
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var delay = (int)Math.Clamp(Math.Ceiling(response.Headers.RetryAfter?.Delta?.TotalSeconds ??
                    (response.Headers.RetryAfter?.Date - now)?.TotalSeconds ?? 1), 1, 86400);
                if (Options.Channel == "telegram")
                {
                    try { using var data = await Read(response, ct); if (data.RootElement.TryGetProperty("parameters", out var parameters) && parameters.TryGetProperty("retry_after", out var after) && after.TryGetInt32(out var seconds)) delay = Math.Clamp(seconds, 1, 86400); }
                    catch (Exception error) when (error is RequestRejected or JsonException or InvalidOperationException) { }
                }
                return new(output.Attempts >= 2 ? "Failed" : "Pending", RetryAfterSeconds: delay);
            }
            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout || (int)response.StatusCode is >= 300 and < 400) return new("Unknown");
            if (!response.IsSuccessStatusCode) return new("Failed");
            using var document = await Read(response, ct); string? id;
            if (Options.Channel == "telegram")
            {
                if (document.RootElement.GetProperty("ok").ValueKind != JsonValueKind.True) return new("Unknown");
                var result = document.RootElement.GetProperty("result");
                if (result.GetProperty("chat").GetProperty("id").GetInt64().ToString(CultureInfo.InvariantCulture) != route.SenderId) return new("Unknown");
                var number = result.GetProperty("message_id").GetInt64(); id = number > 0 ? number.ToString(CultureInfo.InvariantCulture) : null;
            }
            else id = document.RootElement.GetProperty("messages")[0].GetProperty("id").GetString();
            return string.IsNullOrWhiteSpace(id) || id.Length > 200 ? new("Unknown") : new("Sent", id);
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or RequestRejected or JsonException or KeyNotFoundException or InvalidOperationException or FormatException or IndexOutOfRangeException or OverflowException)
        { return new("Unknown"); }
    }
    public void Dispose() => client.Dispose();
}
