using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ContactCenterAI.Domain;

namespace ContactCenterAI.Application;

public sealed class RequestRejected(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
public sealed class OperationalUnavailable(int? storeErrorCode = null) : Exception("OPERATIONAL_STORE_UNAVAILABLE")
{ public int? StoreErrorCode { get; } = storeErrorCode; }
public sealed record ConversationReceipt(Guid ConversationId, long Version, string Ownership = "AI");
public sealed record MessageReceipt(Guid MessageId, Guid TurnId, long Version, string Status = "Pending");
public sealed record ConversationSnapshot(ConversationResource Resource, string Language, IReadOnlyList<StoredMessage> Messages);
public sealed record StoredMessage(Guid MessageId, Guid ClientMessageId, string Text, Guid TurnId);

public interface IOperationalStore
{
    Task<Actor> CreateGuestAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task<Actor> LoginAsync(string issuer, string subject, ActorRoles validatedRoles, Guid? previousSession, DateTimeOffset now, CancellationToken cancellationToken);
    Task<Actor?> FindSessionAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken);
    Task RevokeSessionAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<ConversationReceipt> CreateConversationAsync(Actor actor, string language, string keyHash, string payloadHash, DateTimeOffset now, CancellationToken cancellationToken);
    Task<ConversationSnapshot?> ReadConversationAsync(Actor actor, Guid id, DateTimeOffset now, CancellationToken cancellationToken);
    Task<MessageReceipt> SubmitMessageAsync(Actor actor, Guid id, Guid clientMessageId, string sanitizedText, string payloadHash, long expectedVersion, DateTimeOffset now, CancellationToken cancellationToken);
}

public sealed partial class ConversationIngress(IOperationalStore store, TimeProvider clock)
{
    public Task<ConversationReceipt> CreateAsync(Actor actor, string language, string key, CancellationToken cancellationToken)
    {
        if (language is not ("es" or "en") || string.IsNullOrWhiteSpace(key) || key.Length > 128)
            throw new RequestRejected(400, "INVALID_REQUEST");
        return store.CreateConversationAsync(actor, language, Hash(key), Hash(language), clock.GetUtcNow(), cancellationToken);
    }

    public Task<MessageReceipt> SubmitAsync(Actor actor, Guid conversationId, Guid clientMessageId, string text, long version, CancellationToken cancellationToken)
    {
        if (clientMessageId == Guid.Empty || string.IsNullOrWhiteSpace(text) || text.EnumerateRunes().Count() > 2000 || version < 1)
            throw new RequestRejected(400, "INVALID_REQUEST");
        // Reject payment-like numbers and labelled CVV; do not persist a masked PAN.
        if (PaymentPattern().IsMatch(text)) throw new RequestRejected(400, "SENSITIVE_PAYMENT_DATA");
        var sanitized = EmailPattern().Replace(text, "[EMAIL]");
        sanitized = PhonePattern().Replace(sanitized, "[PHONE]");
        sanitized = SecretPattern().Replace(sanitized, "[SECRET]");
        return store.SubmitMessageAsync(actor, conversationId, clientMessageId, sanitized, Hash(text), version, clock.GetUtcNow(), cancellationToken);
    }

    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    [GeneratedRegex(@"(?i)(?:\d[ -]?){13,19}|\b(?:cvv|cvc|security\s*code)\s*[:=]?\s*\d{3,4}\b", RegexOptions.CultureInvariant, 100)]
    private static partial Regex PaymentPattern();
    [GeneratedRegex(@"[\w.+-]+@[\w.-]+\.[a-zA-Z]{2,}", RegexOptions.CultureInvariant, 100)]
    private static partial Regex EmailPattern();
    [GeneratedRegex(@"(?<!\w)\+?\d[\d ().-]{7,}\d(?!\w)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex PhonePattern();
    [GeneratedRegex(@"(?i)\b(?:password|secret|api[_-]?key|token)\s*[:=]\s*[^\s,;]+|\bsk-[A-Za-z0-9_-]{8,}", RegexOptions.CultureInvariant, 100)]
    private static partial Regex SecretPattern();
}
