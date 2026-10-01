using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ContactCenterAI.Application;

namespace ContactCenterAI.Channels;

public sealed record EndpointOptions(string Channel, string EndpointId, IReadOnlySet<string> Recipients,
    string AccessToken, string AppSecret = "", string VerifyToken = "", string ApiVersion = "", bool TestResourcesConfirmed = false)
{
    public void Validate()
    {
        if (Channel is not ("telegram" or "whatsapp") || !NumericId(EndpointId) ||
            Recipients.Count == 0 || Recipients.Any(id => !NumericId(id)) || string.IsNullOrWhiteSpace(AccessToken))
            throw new RequestRejected(503, "CHANNEL_CONFIG_REQUIRED");
        if (Channel == "telegram" && !Regex.IsMatch(AccessToken, @"\A[0-9]{5,20}:[A-Za-z0-9_-]{30,100}\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
            throw new RequestRejected(503, "CHANNEL_CONFIG_REQUIRED");
        if (Channel == "whatsapp" && (!TestResourcesConfirmed || AppSecret.Length < 16 || VerifyToken.Length < 24 ||
            !Regex.IsMatch(ApiVersion, @"\Av[0-9]{1,2}\.0\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))))
            throw new RequestRejected(503, "META_TEST_CONFIG_REQUIRED");
    }
    public static bool NumericId(string value) => value.Length is > 0 and <= 20 && value.All(c => c is >= '0' and <= '9') && value.Any(c => c != '0');
    public bool Allows(ChannelText input) => input.Channel == Channel && input.EndpointId == EndpointId && Recipients.Contains(input.SenderId);
}

public sealed record ProviderUpdate(string EventId, ChannelText? Text);
public static class ProviderInput
{
    public const int MaximumBody = 256 * 1024;
    public static bool VerifyMeta(ReadOnlySpan<byte> body, string? signature, string appSecret)
    {
        if (body.Length > MaximumBody || signature is null || signature.Length != 71 || !signature.StartsWith("sha256=", StringComparison.Ordinal) || appSecret.Length < 16)
            return false;
        try
        {
            var supplied = Convert.FromHexString(signature[7..]);
            var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), body);
            return CryptographicOperations.FixedTimeEquals(expected, supplied);
        }
        catch (FormatException) { return false; }
    }
    public static bool VerifyChallenge(string? supplied, string expected)
    {
        if (expected.Length < 24 || supplied is null || supplied.Length > 256) return false;
        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(supplied)), SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
    }
    public static JsonDocument Parse(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > MaximumBody) throw new RequestRejected(413, "CHANNEL_BODY_TOO_LARGE");
        try { return JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 }); }
        catch (JsonException) { throw new RequestRejected(400, "CHANNEL_PAYLOAD_REJECTED"); }
    }
    public static IReadOnlyList<ProviderUpdate> Telegram(JsonElement root, string botId, DateTimeOffset received)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True ||
            !root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array || result.GetArrayLength() > 100)
            throw new RequestRejected(503, "TELEGRAM_RESPONSE_REJECTED");
        var updates = new List<ProviderUpdate>();
        foreach (var update in result.EnumerateArray())
        {
            if (update.ValueKind != JsonValueKind.Object || !update.TryGetProperty("update_id", out var id) || id.ValueKind != JsonValueKind.Number || !id.TryGetInt64(out var number) || number < 0 || number == long.MaxValue)
                throw new RequestRejected(503, "TELEGRAM_RESPONSE_REJECTED");
            var key = number.ToString(CultureInfo.InvariantCulture);
            ChannelText? input = null;
            try
            {
                var message = update.GetProperty("message"); var chat = message.GetProperty("chat"); var from = message.GetProperty("from");
                var chatId = chat.GetProperty("id").GetInt64(); var sender = from.GetProperty("id").GetInt64();
                if (chat.GetProperty("type").GetString() == "private" && chatId > 0 && sender == chatId &&
                    from.GetProperty("is_bot").ValueKind == JsonValueKind.False && message.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    input = new("telegram", botId, key, sender.ToString(CultureInfo.InvariantCulture), received,
                        DateTimeOffset.FromUnixTimeSeconds(message.GetProperty("date").GetInt64()), text.GetString()!);
            }
            catch (Exception error) when (error is KeyNotFoundException or InvalidOperationException or FormatException or ArgumentOutOfRangeException) { }
            updates.Add(new(key, input));
        }
        return updates;
    }
    public static IReadOnlyList<ChannelText> Meta(JsonElement root, string phoneId, DateTimeOffset received)
    {
        var output = new List<ChannelText>(); var count = 0;
        try
        {
            if (root.GetProperty("object").GetString() != "whatsapp_business_account") return output;
            foreach (var entry in root.GetProperty("entry").EnumerateArray())
            foreach (var change in entry.GetProperty("changes").EnumerateArray())
            {
                var value = change.GetProperty("value");
                if (value.TryGetProperty("messages", out var events)) count += events.GetArrayLength();
                if (value.TryGetProperty("statuses", out var statuses)) count += statuses.GetArrayLength();
                if (count > 100) throw new RequestRejected(400, "CHANNEL_BATCH_TOO_LARGE");
                if (change.GetProperty("field").GetString() != "messages" || value.GetProperty("metadata").GetProperty("phone_number_id").GetString() != phoneId) continue;
                if (!value.TryGetProperty("messages", out var messages)) continue;
                foreach (var message in messages.EnumerateArray())
                {
                    if (message.GetProperty("type").GetString() != "text") continue;
                    var id = message.GetProperty("id").GetString() ?? throw new JsonException(); var sender = message.GetProperty("from").GetString() ?? throw new JsonException();
                    if (id.Length is < 1 or > 200 || !EndpointOptions.NumericId(sender)) throw new JsonException();
                    var timestamp = long.Parse(message.GetProperty("timestamp").GetString() ?? throw new JsonException(), CultureInfo.InvariantCulture);
                    output.Add(new("whatsapp", phoneId, id, sender, received, DateTimeOffset.FromUnixTimeSeconds(timestamp), message.GetProperty("text").GetProperty("body").GetString() ?? throw new JsonException()));
                }
            }
            return output;
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException or ArgumentOutOfRangeException)
        { throw new RequestRejected(400, "CHANNEL_PAYLOAD_REJECTED"); }
    }
}
