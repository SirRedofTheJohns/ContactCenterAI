using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ContactCenterAI.Application;
using ContactCenterAI.Domain;
namespace ContactCenterAI.Infrastructure;

public sealed class HttpReservationSource : IReservationSource, ISourceCommandPort, IDisposable
{
    private readonly HttpClient client;
    private readonly string serviceKey;
    private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, Converters = { new JsonStringEnumConverter(allowIntegerValues: false) } };
    public HttpReservationSource(string serviceKey)
    {
        this.serviceKey = serviceKey;
        client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri("http://127.0.0.1:7453"), Timeout = TimeSpan.FromSeconds(3), MaxResponseContentBufferSize = 65536 };
    }
    public async Task<IReadOnlyList<SourceReservation>> ListAsync(string serverMemberRef, CancellationToken ct)
    {
        using var activity=DemoTelemetry.Activities.StartActivity("source.read");
        if (string.IsNullOrWhiteSpace(serviceKey)) throw new RequestRejected(503, "RESERVATION_SOURCE_UNAVAILABLE");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/source/reservations");
            request.Headers.Add("X-Service-Key", serviceKey); request.Headers.Add("X-Member-Ref", serverMemberRef);
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw new RequestRejected(503, "RESERVATION_SOURCE_UNAVAILABLE");
            var items = await response.Content.ReadFromJsonAsync<SourceReservation[]>(json, ct);
            if (items is null || items.Length > 20 || items.Any(item => item.MemberRef != serverMemberRef ||
                string.IsNullOrWhiteSpace(item.ReservationId) || item.ReservationId.Length > 40 ||
                string.IsNullOrWhiteSpace(item.PropertyName) || item.PropertyName.Length > 100 || !Enum.IsDefined(item.Status) ||
                !long.TryParse(item.Version, out var version) || version < 1))
                throw new RequestRejected(503, "RESERVATION_SOURCE_UNAVAILABLE");
            return items;
        }
        catch (Exception failure) when (failure is HttpRequestException or JsonException || failure is TaskCanceledException && !ct.IsCancellationRequested)
        { throw new RequestRejected(503, "RESERVATION_SOURCE_UNAVAILABLE"); }
    }
    public void Dispose() => client.Dispose();
    private HttpRequestMessage CommandRequest(HttpMethod method, string path, string member)
    {
        if (string.IsNullOrWhiteSpace(serviceKey)) throw new RequestRejected(503, "RESERVATION_SOURCE_UNAVAILABLE");
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Service-Key", serviceKey); request.Headers.Add("X-Member-Ref", member); return request;
    }
    public async Task<SourceCancelReceipt> CancelAsync(string member, SourceCancelRequest payload, CancellationToken ct)
    {
        using var activity=DemoTelemetry.Activities.StartActivity("source.cancel");
        using var request = CommandRequest(HttpMethod.Post, "/source/commands/cancel", member);
        request.Content = JsonContent.Create(payload);
        using var response = await client.SendAsync(request, ct);
        if ((int)response.StatusCode is 400 or 403 or 404 or 409 or 422)
            throw new SourceCommandRejected((int)response.StatusCode, response.StatusCode == System.Net.HttpStatusCode.Conflict ? "SOURCE_VERSION_CONFLICT" : "SOURCE_REJECTED");
        if (!response.IsSuccessStatusCode) throw new RequestRejected(503, "SOURCE_OUTCOME_UNCERTAIN");
        return await response.Content.ReadFromJsonAsync<SourceCancelReceipt>(json, ct) ?? throw new RequestRejected(503, "SOURCE_RESULT_INVALID");
    }
    public async Task<SourceCancelReceipt?> ReceiptAsync(string member, Guid id, CancellationToken ct)
    {
        using var activity=DemoTelemetry.Activities.StartActivity("source.receipt");
        using var request = CommandRequest(HttpMethod.Get, "/source/commands/" + id, member);
        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode) throw new RequestRejected(503, "SOURCE_OUTCOME_UNCERTAIN");
        return await response.Content.ReadFromJsonAsync<SourceCancelReceipt>(json, ct) ?? throw new RequestRejected(503, "SOURCE_RESULT_INVALID");
    }
}
