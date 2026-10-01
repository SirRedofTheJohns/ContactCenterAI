using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ContactCenterAI.Application;

namespace ContactCenterAI.Infrastructure;

public sealed record LocalInferenceMeasurement(string Model, string Digest, int InputTokens, int OutputTokens, long ElapsedMilliseconds);

public sealed class OllamaIntentProvider(HttpClient client) : IIntentProvider
{
    public const string ModelTag = "qwen3-vl:latest";
    public const string ModelDigest = "901cae73216286ea8c5aba8b46d307ff7188f737285ec500c795a12f05225d28";
    public const string WrapperVersion = "raw-chatml-empty-think-v1";
    private const int MaximumBodyBytes = 65536;
    private static readonly string SystemPrompt = Resource("intent-local-v1.system.txt");
    private static readonly string Schema = Resource("intent-local-v1.schema.json");
    public static string PromptHash => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(SystemPrompt)));
    public static string SchemaHash => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Schema)));
    public string ProviderId => "ollama:qwen3-vl@901cae732162:intent-local-v1";
    public LocalInferenceMeasurement? LastMeasurement { get; private set; }

    public static HttpClient CreateClient() => new(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false }) { Timeout = Timeout.InfiniteTimeSpan };
    private static string Resource(string name)
    {
        using var stream = typeof(OllamaIntentProvider).Assembly.GetManifestResourceStream("ContactCenterAI.Infrastructure.Prompts." + name)
            ?? throw new InvalidOperationException("MODEL_CONTRACT_RESOURCE_MISSING");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
    internal static async Task<JsonDocument> ReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode) throw new RequestRejected(503, "LOCAL_MODEL_UNAVAILABLE");
        if (response.Content.Headers.ContentLength > MaximumBodyBytes) throw new RequestRejected(502, "MODEL_RESPONSE_REJECTED");
        using var bytes = new MemoryStream();
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[4096];
        int count;
        while ((count = await input.ReadAsync(buffer, ct)) != 0)
        {
            if (bytes.Length + count > MaximumBodyBytes) throw new RequestRejected(502, "MODEL_RESPONSE_REJECTED");
            bytes.Write(buffer, 0, count);
        }
        return JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = 16 });
    }
    public async Task<string> ProposeAsync(string sanitizedText, string language, CancellationToken ct)
    {
        LastMeasurement = null;
        if (language is not ("es" or "en") || sanitizedText.Length > 2000 || string.IsNullOrWhiteSpace(sanitizedText))
            throw new RequestRejected(400, "MODEL_INPUT_REJECTED");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(8));
        var timer = Stopwatch.StartNew();
        try
        {
            using var tagsRequest = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:11434/api/tags");
            using var tagsResponse = await client.SendAsync(tagsRequest, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            using var tags = await ReadAsync(tagsResponse, deadline.Token);
            if (!tags.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array ||
                !models.EnumerateArray().Any(model => model.TryGetProperty("name", out var name) && name.GetString() == ModelTag &&
                    model.TryGetProperty("digest", out var digest) && digest.GetString() == ModelDigest))
                throw new RequestRejected(503, "LOCAL_MODEL_PIN_MISMATCH");
            var schema = JsonNode.Parse(Schema)!;
            schema["properties"]!["language"] = new JsonObject { ["type"] = "string", ["const"] = language };
            var payload = new
            {
                model = ModelTag,
                prompt = "<|im_start|>system\n" + SystemPrompt + "<|im_end|>\n<|im_start|>user\n" +
                    JsonSerializer.Serialize(new { language, message = sanitizedText }) +
                    "<|im_end|>\n<|im_start|>assistant\n<think>\n\n</think>\n\n",
                raw = true, stream = false, think = false, format = schema, keep_alive = "15m",
                options = new { temperature = 0, num_ctx = 2048, num_predict = 128, num_gpu = 0, num_thread = 8 }
            };
            using var chatRequest = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:11434/api/generate") { Content = JsonContent.Create(payload) };
            using var chatResponse = await client.SendAsync(chatRequest, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            using var body = await ReadAsync(chatResponse, deadline.Token);
            var root = body.RootElement;
            if (!root.TryGetProperty("done", out var done) || done.ValueKind != JsonValueKind.True ||
                !root.TryGetProperty("done_reason", out var reason) || reason.GetString() != "stop" ||
                !root.TryGetProperty("model", out var returnedModel) || returnedModel.GetString() != ModelTag ||
                !root.TryGetProperty("response", out var content) || content.ValueKind != JsonValueKind.String ||
                root.TryGetProperty("tool_calls", out var calls) && (calls.ValueKind != JsonValueKind.Array || calls.GetArrayLength() != 0))
                throw new RequestRejected(502, "MODEL_RESPONSE_REJECTED");
            var output = content.GetString()!;
            if (output.Length > 2048) throw new RequestRejected(502, "MODEL_RESPONSE_REJECTED");
            var inputTokens = TokenCount(root, "prompt_eval_count");
            var outputTokens = TokenCount(root, "eval_count");
            LastMeasurement = new(ModelTag, ModelDigest, inputTokens, outputTokens, timer.ElapsedMilliseconds);
            _ = ProposalGateway.Validate(output, language);
            return output;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or IOException)
        { throw new RequestRejected(502, "MODEL_RESPONSE_REJECTED"); }
    }
    private static int TokenCount(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.TryGetInt32(out var count) && count >= 0
        ? count : throw new RequestRejected(502, "MODEL_RESPONSE_REJECTED");
}
