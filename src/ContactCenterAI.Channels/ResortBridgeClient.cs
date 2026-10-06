using System.Net;
using System.Net.Http.Json;
using ContactCenterAI.Application;

namespace ContactCenterAI.Channels;

// A fixed loopback service, never a URL or an identity supplied by chat/model output.
public sealed class ResortBridgeClient : IDisposable
{
    private readonly HttpClient client;
    private readonly ChannelStore store;
    public ResortBridgeClient(string key, ChannelStore store, HttpMessageHandler? handler = null)
    {
        if (key.Length is < 32 or > 128) throw new RequestRejected(503, "RESORT_BRIDGE_CONFIG_REQUIRED");
        this.store = store;
        client = new(handler ?? new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false });
        client.BaseAddress = new Uri("http://127.0.0.1:7452/"); client.Timeout = TimeSpan.FromSeconds(7);
        client.DefaultRequestHeaders.Add("X-Resort-Service-Key", key);
    }
    public static bool Recognizes(string text) => ResortLanguage.LinkCommand(text) ||
        text.TrimStart().StartsWith("vincular ", StringComparison.OrdinalIgnoreCase) || text.TrimStart().StartsWith("link ", StringComparison.OrdinalIgnoreCase) ||
        ResortLanguage.IsConfirmation(text) || ResortLanguage.Parse(text, DateTimeOffset.UtcNow) is not null;
    public async Task<ChannelReply> AnswerAsync(ChannelJob job, CancellationToken ct)
    {
        bool en = job.Route.Language == "en";
        if (!store.CanAct(job)) return new(en ? "The bot is paused. No booking request was sent." : "El bot está en pausa. No se envió ninguna solicitud de reserva.", "CHANNEL_AUTHORITY_CHANGED", []);
        try
        {
            using var response = await client.PostAsJsonAsync("internal/resort/channel", new ResortChannelRequest(job.Route.Key, job.Route.Epoch,
                job.Route.EndpointId, job.Route.Channel, job.Text, job.Key, job.Route.Language), ct);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ResortChannelResult>(ct);
                if (result is not null && result.Text.Length <= 3500) return new(result.Text, result.Code, []);
            }
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException) { }
        return new(en ? "I could not verify the booking result. Check My bookings in the resort page before requesting another action. No success is assumed." :
            "No pude comprobar el resultado de la reserva. Revisa Mis reservas en la página del resort antes de solicitar otra acción. No se da por hecho que se ejecutó.", "RESORT_RESULT_UNVERIFIED", []);
    }
    public void Dispose() => client.Dispose();
}
