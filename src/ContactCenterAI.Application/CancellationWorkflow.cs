using System.Text.Json;
using ContactCenterAI.Domain;
namespace ContactCenterAI.Application;

public sealed record CancellationOffer(Guid OfferId, Guid ConversationId, string ReservationId, string PropertyName,
    DateTimeOffset CheckInUtc, string SourceVersion, string PolicyVersion, DateTimeOffset ExpiresAt,
    long Version, string Status, int PenaltyMinorUnits = 0, string Currency = "USD");
public sealed record ConfirmationReceipt(Guid? OperationId, string Status, long Version);
public sealed record OperationView(Guid OperationId, Guid ConversationId, string ReservationId, string Status,
    string ReasonCode, string? SourceReference, bool RequiresHumanReview);
public sealed record ActionSnapshot(IReadOnlyList<CancellationOffer> Offers, IReadOnlyList<OperationView> Operations);
public sealed record SourceCancelRequest(Guid CommandId, string ReservationId, string ExpectedReservationVersion, string PolicyVersion);
public sealed record SourceCancelReceipt(Guid CommandId, string ReservationId, string Status, string CurrentVersion, string ReasonCode, string SourceReference);
public sealed class SourceCommandRejected(int status, string code) : Exception(code)
{ public int Status { get; } = status; public string Code { get; } = code; }
public interface ISourceCommandPort
{
    Task<SourceCancelReceipt> CancelAsync(string member, SourceCancelRequest request, CancellationToken ct);
    // null has definitive not-accepted semantics ONLY for the ADR-015 local transactional source.
    Task<SourceCancelReceipt?> ReceiptAsync(string member, Guid commandId, CancellationToken ct);
}
public interface ICancellationStore
{
    Task<CancellationOffer> SaveOfferAsync(Actor actor, Guid conversation, SourceReservation reservation, long version, DateTimeOffset now, CancellationToken ct);
    Task<ConfirmationReceipt?> ReplayAsync(Actor actor, Guid conversation, string key, string hash, DateTimeOffset now, CancellationToken ct);
    Task<ConfirmationReceipt> ConfirmAsync(Actor actor, Guid conversation, Guid offer, string decision, long version,
        string key, string hash, SourceReservation? current, DateTimeOffset now, CancellationToken ct);
    Task<ActionSnapshot> ActionsAsync(Actor actor, Guid conversation, DateTimeOffset now, CancellationToken ct);
    Task<OperationView> OperationAsync(Actor actor, Guid operation, DateTimeOffset now, CancellationToken ct);
}
public sealed class TransactionGate(bool disabled)
{ public bool Disabled { get; set; } = disabled; }
public sealed class CancellationWorkflow(IOperationalStore operational, ICancellationStore store,
    IReservationSource source, TimeProvider clock, TransactionGate gate)
{
    private async Task<ConversationSnapshot> Require(Actor actor, Guid conversation, CancellationToken ct)
    {
        var snapshot = await operational.ReadConversationAsync(actor, conversation, clock.GetUtcNow(), ct)
            ?? throw new RequestRejected(404, "RESOURCE_NOT_FOUND");
        if (actor.PrincipalId is null || actor.MemberRef is null || !actor.Roles.HasFlag(ActorRoles.Customer))
            throw new RequestRejected(403, "VERIFIED_CUSTOMER_REQUIRED");
        return snapshot;
    }
    public async Task<CancellationOffer> PreviewAsync(Actor actor, Guid conversation, string reservationId, long version, CancellationToken ct)
    {
        _ = await Require(actor, conversation, ct);
        if (gate.Disabled) throw new RequestRejected(503, "TRANSACTIONS_DISABLED");
        if (string.IsNullOrWhiteSpace(reservationId) || reservationId.Length > 40) throw new RequestRejected(400, "INVALID_REQUEST");
        var reservation = (await source.ListAsync(actor.MemberRef!, ct)).SingleOrDefault(item => item.ReservationId == reservationId)
            ?? throw new RequestRejected(404, "RESOURCE_NOT_FOUND");
        return await store.SaveOfferAsync(actor, conversation, reservation, version, clock.GetUtcNow(), ct);
    }
    public async Task<ConfirmationReceipt> ConfirmAsync(Actor actor, Guid conversation, Guid offer, string decision, long version, string key, CancellationToken ct)
    {
        _ = await Require(actor, conversation, ct);
        if (offer == Guid.Empty || decision is not ("confirm" or "reject") || string.IsNullOrWhiteSpace(key) || key.Length > 128)
            throw new RequestRejected(400, "INVALID_REQUEST");
        var keyHash = ConversationIngress.Hash(key);
        var hash = ConversationIngress.Hash(JsonSerializer.Serialize(new { conversation, offer, decision, version }));
        var replay = await store.ReplayAsync(actor, conversation, keyHash, hash, clock.GetUtcNow(), ct);
        if (replay is not null) return replay;
        if (decision == "confirm" && gate.Disabled) throw new RequestRejected(503, "TRANSACTIONS_DISABLED");
        var actions = await store.ActionsAsync(actor, conversation, clock.GetUtcNow(), ct);
        var selected = actions.Offers.SingleOrDefault(item => item.OfferId == offer) ?? throw new RequestRejected(404, "RESOURCE_NOT_FOUND");
        SourceReservation? current = null;
        if (decision == "confirm") current = (await source.ListAsync(actor.MemberRef!, ct)).SingleOrDefault(item => item.ReservationId == selected.ReservationId)
            ?? throw new RequestRejected(404, "RESOURCE_NOT_FOUND");
        return await store.ConfirmAsync(actor, conversation, offer, decision, version, keyHash, hash, current, clock.GetUtcNow(), ct);
    }
}
public sealed record DispatchLease(Guid OperationId, Guid LeaseId, string MemberRef, SourceCancelRequest Payload,
    bool IsRecovery, int Attempts, DateTimeOffset SubmittedAt);
public interface ICommandLedger
{
    Task<DispatchLease?> ClaimAsync(DateTimeOffset now, bool killSwitch, CancellationToken ct);
    Task FinishAsync(DispatchLease lease, string status, string reason, SourceCancelReceipt? receipt, DateTimeOffset now, CancellationToken ct);
}
public sealed class CommandDispatcher(ICommandLedger ledger, ISourceCommandPort source, TimeProvider clock, TransactionGate gate)
{
    public async Task<bool> RunOnceAsync(CancellationToken ct)
    {
        var lease = await ledger.ClaimAsync(clock.GetUtcNow(), gate.Disabled, ct);
        if (lease is null) return false;
        try
        {
            SourceCancelReceipt? receipt = null;
            if (lease.IsRecovery) receipt = await source.ReceiptAsync(lease.MemberRef, lease.OperationId, ct);
            if (receipt is null)
            {
                // Local source guarantees atomic receipt/effect and retained dedupe. Same immutable ID only.
                if (gate.Disabled) { await ledger.FinishAsync(lease, "Unknown", "RECOVERY_WRITE_PAUSED", null, clock.GetUtcNow(), ct); return true; }
                receipt = await source.CancelAsync(lease.MemberRef, lease.Payload, ct);
            }
            if (receipt.CommandId != lease.OperationId || receipt.ReservationId != lease.Payload.ReservationId || receipt.Status != "Completed" ||
                receipt.ReasonCode != "CANCELLED_FREE" || string.IsNullOrWhiteSpace(receipt.SourceReference) || receipt.SourceReference.Length > 100)
                throw new RequestRejected(503, "SOURCE_RESULT_INVALID");
            await ledger.FinishAsync(lease, "Completed", receipt.ReasonCode, receipt, clock.GetUtcNow(), ct);
        }
        catch (SourceCommandRejected error) when (error.Status is 400 or 403 or 404 or 409 or 422)
        { await ledger.FinishAsync(lease, error.Status == 409 ? "Conflict" : "Rejected", error.Code, null, clock.GetUtcNow(), ct); }
        catch (Exception error) when (error is RequestRejected or HttpRequestException or System.Text.Json.JsonException || error is OperationCanceledException && !ct.IsCancellationRequested)
        { await ledger.FinishAsync(lease, "Unknown", "SOURCE_OUTCOME_UNCERTAIN", null, clock.GetUtcNow(), ct); }
        // Host shutdown leaves Submitted durable; expired lease is reconciled after restart.
        return true;
    }
}
