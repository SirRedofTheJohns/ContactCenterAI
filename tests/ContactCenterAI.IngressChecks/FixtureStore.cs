using ContactCenterAI.Application;
using ContactCenterAI.Domain;

namespace ContactCenterAI.IngressChecks;

// TEST ONLY: validates the HTTP pipeline against a deterministic port. It does
// not stand in for SQL durability, isolation, failure or native-driver evidence.
internal sealed class FixtureStore : IOperationalStore
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, Actor> sessions = [];
    private readonly Dictionary<Guid, ConversationSnapshot> conversations = [];
    private readonly Dictionary<(Guid, string), (string, ConversationReceipt)> creations = [];
    private readonly Dictionary<(Guid, Guid), (string, MessageReceipt)> receipts = [];
    public bool Unavailable { get; set; }
    public int ConversationCount => conversations.Count;
    public int MessageCount => receipts.Count;
    private void Available() { if (Unavailable) throw new OperationalUnavailable(); }
    public Actor Seed(Actor actor) { sessions[actor.SessionId] = actor; return actor; }
    public Task<Actor> CreateGuestAsync(DateTimeOffset now, CancellationToken ct)
    { Available(); return Task.FromResult(Seed(new Actor(Guid.NewGuid(), "tenant-demo", null, null, ActorRoles.None, now.AddMinutes(15)))); }
    public Task<Actor> LoginAsync(string issuer, string subject, ActorRoles roles, Guid? previous, DateTimeOffset now, CancellationToken ct)
        => throw new NotSupportedException("Live OIDC binding is outside this fixture.");
    public Task<Actor?> FindSessionAsync(Guid id, DateTimeOffset now, CancellationToken ct)
    { Available(); return Task.FromResult(sessions.TryGetValue(id, out var actor) && actor.ExpiresAt > now ? actor : null); }
    public Task RevokeSessionAsync(Guid id, DateTimeOffset now, CancellationToken ct)
    { Available(); sessions.Remove(id); return Task.CompletedTask; }
    public Task<ConversationReceipt> CreateConversationAsync(Actor actor, string language, string key, string hash, DateTimeOffset now, CancellationToken ct)
    {
        lock (gate)
        {
            Available();
            if (actor.PrincipalId is not null && !actor.Roles.HasFlag(ActorRoles.Customer)) throw new RequestRejected(403, "CUSTOMER_SESSION_REQUIRED");
            var scope = actor.PrincipalId ?? actor.SessionId;
            if (creations.TryGetValue((scope, key), out var prior))
            { if (prior.Item1 != hash) throw new RequestRejected(409, "IDEMPOTENCY_CONFLICT"); return Task.FromResult(prior.Item2); }
            var id = Guid.NewGuid(); var receipt = new ConversationReceipt(id, 1);
            conversations[id] = new(new(id, actor.TenantId, actor.PrincipalId, actor.PrincipalId is null ? actor.SessionId : null, 1, 1), language, []);
            creations[(scope, key)] = (hash, receipt); return Task.FromResult(receipt);
        }
    }
    public Task<ConversationSnapshot?> ReadConversationAsync(Actor actor, Guid id, DateTimeOffset now, CancellationToken ct)
    {
        Available(); conversations.TryGetValue(id, out var result);
        return Task.FromResult(result is not null && ResourceAccess.CanAccess(actor, result.Resource, null, now) ? result : null);
    }
    public Task<MessageReceipt> SubmitMessageAsync(Actor actor, Guid id, Guid client, string text, string hash, long version, DateTimeOffset now, CancellationToken ct)
    {
        lock (gate)
        {
            Available();
            if (!conversations.TryGetValue(id, out var conversation) || !ResourceAccess.CanAccess(actor, conversation.Resource, null, now)) throw new RequestRejected(404, "RESOURCE_NOT_FOUND");
            if (receipts.TryGetValue((id, client), out var prior))
            { if (prior.Item1 != hash) throw new RequestRejected(409, "MESSAGE_ID_CONFLICT"); return Task.FromResult(prior.Item2); }
            if (conversation.Resource.Version != version) throw new RequestRejected(409, "VERSION_CONFLICT");
            var receipt = new MessageReceipt(Guid.NewGuid(), Guid.NewGuid(), version + 1);
            conversations[id] = conversation with { Resource = conversation.Resource with { Version = version + 1 },
                Messages = [.. conversation.Messages, new StoredMessage(receipt.MessageId, client, text, receipt.TurnId)] };
            receipts[(id, client)] = (hash, receipt); return Task.FromResult(receipt);
        }
    }
}
