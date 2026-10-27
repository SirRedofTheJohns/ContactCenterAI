using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ContactCenterAI.Application;

namespace ContactCenterAI.Infrastructure;

// Resort chat adapter for the existing pinned local model. The model only proposes intent and fields; the
// conversation still applies the same clarification, ownership, offer and explicit-confirmation rules.
// Failures return an explicit error code: there is no silent fallback to the deterministic interpreter.
public sealed partial class OllamaResortInterpreter(HttpClient client, string endpoint = OllamaResortInterpreter.LoopbackEndpoint) : IResortInterpreter
{
    public const string LoopbackEndpoint = "http://127.0.0.1:11434";
    // Docker Desktop forwards this name to the host loopback, so Ollama keeps listening on 127.0.0.1 only.
    public const string ContainerHostEndpoint = "http://host.docker.internal:11434";
    public static bool IsAllowedEndpoint(string value, bool container) => value == LoopbackEndpoint || container && value == ContainerHostEndpoint;
    private static readonly string SystemPrompt = Resource("resort-intent-local-v1.system.txt");
    private static readonly string Schema = Resource("resort-intent-local-v1.schema.json");
    public string Mode => "local-llm";
    public string ProviderId => "ollama:qwen3-vl@901cae732162:resort-intent-local-v1";

    private static string Resource(string name)
    {
        using var stream = typeof(OllamaResortInterpreter).Assembly.GetManifestResourceStream("ContactCenterAI.Infrastructure.Prompts." + name)
            ?? throw new InvalidOperationException("MODEL_CONTRACT_RESOURCE_MISSING");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
    public async Task<ResortInterpretation> InterpretAsync(string sanitizedText, string language, DateOnly today, CancellationToken ct)
    {
        if (language is not ("es" or "en") || sanitizedText.Length > 2000 || string.IsNullOrWhiteSpace(sanitizedText)) return Failed("MODEL_INPUT_REJECTED");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(8));
        try
        {
            if (!await PinnedAsync(deadline.Token)) return Failed("LOCAL_MODEL_PIN_MISMATCH");
            var payload = new
            {
                model = OllamaIntentProvider.ModelTag,
                prompt = "<|im_start|>system\n" + SystemPrompt + "<|im_end|>\n<|im_start|>user\n" +
                    JsonSerializer.Serialize(new { language, today = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), message = sanitizedText }) +
                    "<|im_end|>\n<|im_start|>assistant\n<think>\n\n</think>\n\n",
                raw = true, stream = false, think = false, format = JsonNode.Parse(Schema), keep_alive = "15m",
                options = new { temperature = 0, num_ctx = LoadOptions.num_ctx, num_predict = 128, num_gpu = LoadOptions.num_gpu, num_thread = LoadOptions.num_thread }
            };
            using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, endpoint + "/api/generate") { Content = JsonContent.Create(payload) }, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            using var body = await OllamaIntentProvider.ReadAsync(response, deadline.Token);
            var root = body.RootElement;
            if (!root.TryGetProperty("done", out var done) || done.ValueKind != JsonValueKind.True ||
                !root.TryGetProperty("model", out var returned) || returned.GetString() != OllamaIntentProvider.ModelTag ||
                !root.TryGetProperty("response", out var content) || content.ValueKind != JsonValueKind.String || content.GetString()!.Length > 2048)
                return Failed("MODEL_RESPONSE_REJECTED");
            return Validate(content.GetString()!, ProviderId);
        }
        catch (RequestRejected rejected) { return Failed(rejected.Code); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Failed("LOCAL_MODEL_TIMEOUT"); }
        catch (Exception error) when (error is HttpRequestException or JsonException or IOException) { return Failed("LOCAL_MODEL_UNAVAILABLE"); }
    }
    private ResortInterpretation Failed(string code) => new(null, null, false, ProviderId, code);
    // Same load options as inference, so the warm-up load is reused instead of reloaded.
    private static readonly (int num_ctx, int num_gpu, int num_thread) LoadOptions = (2048, 0, 8);
    private async Task<bool> PinnedAsync(CancellationToken ct)
    {
        using var tagsResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, endpoint + "/api/tags"), HttpCompletionOption.ResponseHeadersRead, ct);
        using var tags = await OllamaIntentProvider.ReadAsync(tagsResponse, ct);
        return tags.RootElement.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Array &&
            models.EnumerateArray().Any(model => model.TryGetProperty("name", out var name) && name.GetString() == OllamaIntentProvider.ModelTag &&
                model.TryGetProperty("digest", out var digest) && digest.GetString() == OllamaIntentProvider.ModelDigest);
    }
    // Cold-loading the model on CPU can exceed the eight-second answer deadline, so startup loads it once
    // (pinned digest only, no prompt, no inference). Failure changes nothing: the chat still reports its explicit error.
    public async Task<bool> WarmUpAsync(CancellationToken ct)
    {
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromMinutes(3));
            if (!await PinnedAsync(deadline.Token)) return false;
            var payload = new { model = OllamaIntentProvider.ModelTag, keep_alive = "15m", options = new { num_ctx = LoadOptions.num_ctx, num_gpu = LoadOptions.num_gpu, num_thread = LoadOptions.num_thread } };
            using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, endpoint + "/api/generate") { Content = JsonContent.Create(payload) }, deadline.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or IOException or JsonException or RequestRejected) { return false; }
    }
    // Closed-contract validation of untrusted model output. Anything outside the contract is rejected, not repaired.
    public static ResortInterpretation Validate(string output, string source)
    {
        ResortInterpretation Reject() => new(null, null, false, source, "MODEL_RESPONSE_REJECTED");
        JsonObject? json;
        try { json = JsonNode.Parse(output, documentOptions: new JsonDocumentOptions { MaxDepth = 4 }) as JsonObject; }
        catch (JsonException) { return Reject(); }
        string[] fields = ["action", "type", "arrival", "departure", "guests", "stayId"];
        if (json is null || json.Count != fields.Length || fields.Any(f => !json.ContainsKey(f))) return Reject();
        string? Text(string name) => json[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        bool IsNullOrString(string name) => json[name] is null || json[name] is JsonValue v && v.TryGetValue<string>(out _);
        if (!fields.Where(f => f != "guests").All(IsNullOrString)) return Reject();
        var action = Text("action"); var type = Text("type"); var arrival = Text("arrival"); var departure = Text("departure"); var stayId = Text("stayId");
        if (action is not ("catalog" or "availability" or "create" or "change" or "cancel" or "list" or "policy" or "unknown")) return Reject();
        if (type is not (null or "standard" or "deluxe" or "suite")) return Reject();
        if (arrival is not null && !IsoDate(arrival) || departure is not null && !IsoDate(departure)) return Reject();
        if (stayId is not null && !StayCode().IsMatch(stayId)) return Reject();
        int? guests = null;
        if (json["guests"] is not null) { if (json["guests"] is not JsonValue g || !g.TryGetValue<int>(out var n) || n is < 1 or > 20) return Reject(); guests = n; }
        if (action == "unknown") return new(null, guests, false, source);
        // Change/cancel without a literal code stays a request for the code; the model may not invent one.
        return new(new ResortRequest(action, stayId, type, arrival, departure, guests ?? 2), guests, false, source);
    }
    private static bool IsoDate(string value) => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    [GeneratedRegex(@"^STAY-[A-F0-9]{8}$", RegexOptions.CultureInvariant, 100)] private static partial Regex StayCode();
}
