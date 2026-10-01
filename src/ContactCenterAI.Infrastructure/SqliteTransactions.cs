using System.Text.Json;
using ContactCenterAI.Application;
using ContactCenterAI.Domain;
using Microsoft.Data.Sqlite;
namespace ContactCenterAI.Infrastructure;

public sealed partial class SqliteOperationalStore
{
    private static void InitializeTransactions(SqliteConnection db)
    {
        using var schema = Command(db, null, """
            CREATE TABLE IF NOT EXISTS Offer(Id TEXT PRIMARY KEY,ConversationId TEXT NOT NULL,PrincipalId TEXT NOT NULL,TenantId TEXT NOT NULL,Epoch INTEGER NOT NULL,Version INTEGER NOT NULL,ReservationId TEXT NOT NULL,PropertyName TEXT NOT NULL,CheckInUtc INTEGER NOT NULL,SourceVersion TEXT NOT NULL,PolicyVersion TEXT NOT NULL,ExpiresAt INTEGER NOT NULL,Status TEXT NOT NULL,FOREIGN KEY(ConversationId) REFERENCES Conversation(Id));
            CREATE TABLE IF NOT EXISTS Confirmation(PrincipalId TEXT NOT NULL,ConversationId TEXT NOT NULL,KeyHash TEXT NOT NULL,PayloadHash TEXT NOT NULL,OfferId TEXT NOT NULL UNIQUE,ResultJson TEXT NOT NULL,PRIMARY KEY(PrincipalId,ConversationId,KeyHash),FOREIGN KEY(OfferId) REFERENCES Offer(Id));
            CREATE TABLE IF NOT EXISTS BusinessCommand(Id TEXT PRIMARY KEY,ConversationId TEXT NOT NULL,OfferId TEXT NOT NULL UNIQUE,PrincipalId TEXT NOT NULL,TenantId TEXT NOT NULL,MemberRef TEXT NOT NULL,PayloadJson TEXT NOT NULL,Status TEXT NOT NULL,ReasonCode TEXT NOT NULL,ReceiptJson TEXT,LeaseId TEXT,LeaseUntil INTEGER,Attempts INTEGER NOT NULL,NextAttempt INTEGER NOT NULL,SubmittedAt INTEGER,RequiresHumanReview INTEGER NOT NULL,CreatedAt INTEGER NOT NULL,FOREIGN KEY(OfferId) REFERENCES Offer(Id),FOREIGN KEY(ConversationId) REFERENCES Conversation(Id));
            CREATE INDEX IF NOT EXISTS CommandDispatch ON BusinessCommand(Status,NextAttempt,LeaseUntil);
            CREATE TABLE IF NOT EXISTS HumanReview(OperationId TEXT PRIMARY KEY,Status TEXT NOT NULL,OpenedAt INTEGER NOT NULL,ResolvedAt INTEGER,FOREIGN KEY(OperationId) REFERENCES BusinessCommand(Id));
            """); schema.ExecuteNonQuery();
    }
    private static (Actor Actor, ConversationResource Resource) Owned(SqliteConnection db, SqliteTransaction tx, Actor supplied, Guid id, DateTimeOffset now)
    {
        var actor = Require(db, tx, supplied, now);
        var resource = Resource(db, tx, actor, id, now) ?? throw new RequestRejected(404, "RESOURCE_NOT_FOUND");
        if (actor.PrincipalId is null || actor.MemberRef is null || !actor.Roles.HasFlag(ActorRoles.Customer))
            throw new RequestRejected(403, "VERIFIED_CUSTOMER_REQUIRED");
        return (actor, resource.Resource);
    }
    private static CancellationOffer OfferRow(SqliteDataReader row) => new(Guid.Parse(row.GetString(0)), Guid.Parse(row.GetString(1)),
        row.GetString(6), row.GetString(7), DateTimeOffset.FromUnixTimeMilliseconds(row.GetInt64(8)), row.GetString(9), row.GetString(10),
        DateTimeOffset.FromUnixTimeMilliseconds(row.GetInt64(11)), row.GetInt64(5), row.GetString(12));
    public Task<CancellationOffer> SaveOfferAsync(Actor supplied, Guid id, SourceReservation reservation, long version, DateTimeOffset now, CancellationToken ct) => Run(ct, (db, tx) =>
    {
        var (actor, resource) = Owned(db, tx, supplied, id, now);
        RequireAiOwnership(db, tx, id);
        if (resource.Version != version) throw new RequestRejected(409, "VERSION_CONFLICT");
        if (reservation.MemberRef != actor.MemberRef) throw new RequestRejected(404, "RESOURCE_NOT_FOUND");
        var eligibility = CancellationPolicy.Evaluate(reservation.Status, reservation.CheckInUtc, now);
        if (!eligibility.Eligible) throw new RequestRejected(422, eligibility.ReasonCode);
        using (var pending = Command(db, tx, "SELECT COUNT(*) FROM BusinessCommand WHERE ConversationId=@id AND Status IN ('Pending','Submitted','Unknown')", ("@id", id)))
            if ((long)pending.ExecuteScalar()! != 0) throw new RequestRejected(409, "OPERATION_PENDING");
        var offer = new CancellationOffer(Guid.NewGuid(), id, reservation.ReservationId, reservation.PropertyName, reservation.CheckInUtc,
            reservation.Version, CancellationPolicy.Version, now.AddMinutes(5), version + 1, "Active");
        using var insert = Command(db, tx, """
            UPDATE Offer SET Status='Invalidated' WHERE ConversationId=@conversation AND Status='Active';
            UPDATE Conversation SET Version=Version+1 WHERE Id=@conversation;
            INSERT INTO Offer VALUES(@id,@conversation,@principal,@tenant,@epoch,@version,@reservation,@property,@date,@source,@policy,@expiry,'Active');
            """, ("@id", offer.OfferId), ("@conversation", id), ("@principal", actor.PrincipalId), ("@tenant", actor.TenantId), ("@epoch", resource.Epoch),
            ("@version", offer.Version), ("@reservation", offer.ReservationId), ("@property", offer.PropertyName), ("@date", offer.CheckInUtc),
            ("@source", offer.SourceVersion), ("@policy", offer.PolicyVersion), ("@expiry", offer.ExpiresAt));
        insert.ExecuteNonQuery(); Audit(db, tx, actor, id, "CANCELLATION_OFFERED", now); return offer;
    });
    private static ConfirmationReceipt? Replay(SqliteConnection db, SqliteTransaction tx, Actor actor, Guid conversation, string key, string hash)
    {
        using var query = Command(db, tx, "SELECT PayloadHash,ResultJson FROM Confirmation WHERE PrincipalId=@principal AND ConversationId=@conversation AND KeyHash=@key",
            ("@principal", actor.PrincipalId), ("@conversation", conversation), ("@key", key)); using var row = query.ExecuteReader();
        if (!row.Read()) return null;
        return row.GetString(0) == hash ? JsonSerializer.Deserialize<ConfirmationReceipt>(row.GetString(1))! : throw new RequestRejected(409, "IDEMPOTENCY_CONFLICT");
    }
    public Task<ConfirmationReceipt?> ReplayAsync(Actor supplied, Guid id, string key, string hash, DateTimeOffset now, CancellationToken ct) => Run(ct, (db, tx) =>
    { var (actor, _) = Owned(db, tx, supplied, id, now); return Replay(db, tx, actor, id, key, hash); });
    public Task<ConfirmationReceipt> ConfirmAsync(Actor supplied, Guid id, Guid offerId, string decision, long version,
        string key, string hash, SourceReservation? current, DateTimeOffset now, CancellationToken ct) => Run(ct, (db, tx) =>
    {
        var (actor, resource) = Owned(db, tx, supplied, id, now);
        var replay = Replay(db, tx, actor, id, key, hash); if (replay is not null) return replay;
        CancellationOffer offer; long epoch;
        using (var lookup = Command(db, tx, "SELECT * FROM Offer WHERE Id=@offer AND ConversationId=@conversation AND PrincipalId=@principal AND TenantId=@tenant",
            ("@offer", offerId), ("@conversation", id), ("@principal", actor.PrincipalId), ("@tenant", actor.TenantId)))
        using (var row = lookup.ExecuteReader())
        { if (!row.Read()) throw new RequestRejected(404, "RESOURCE_NOT_FOUND"); offer = OfferRow(row); epoch = row.GetInt64(4); }
        if (offer.Status != "Active") throw new RequestRejected(409, "OFFER_CONSUMED");
        RequireAiOwnership(db, tx, id);
        if (offer.ExpiresAt <= now) throw new RequestRejected(409, "OFFER_EXPIRED");
        if (offer.Version != version || resource.Version != version || resource.Epoch != epoch) throw new RequestRejected(409, "VERSION_CONFLICT");
        if (decision is not ("confirm" or "reject")) throw new RequestRejected(400, "INVALID_REQUEST");
        if (decision == "confirm")
        {
            if (current is null || current.MemberRef != actor.MemberRef || current.ReservationId != offer.ReservationId) throw new RequestRejected(404, "RESOURCE_NOT_FOUND");
            if (current.Version != offer.SourceVersion || offer.PolicyVersion != CancellationPolicy.Version) throw new RequestRejected(409, "SOURCE_VERSION_CONFLICT");
            if (!CancellationPolicy.Evaluate(current.Status, current.CheckInUtc, now).Eligible) throw new RequestRejected(422, "CANCELLATION_NOT_ELIGIBLE");
        }
        var receipt = new ConfirmationReceipt(decision == "confirm" ? Guid.NewGuid() : null, decision == "confirm" ? "Pending" : "Rejected", version + 1);
        using (var consume = Command(db, tx, """
            UPDATE Offer SET Status=@status WHERE Id=@offer AND Status='Active';
            INSERT INTO Confirmation VALUES(@principal,@conversation,@key,@hash,@offer,@result);
            UPDATE Conversation SET Version=Version+1 WHERE Id=@conversation;
            """, ("@status", decision == "confirm" ? "Consumed" : "Rejected"), ("@offer", offerId), ("@principal", actor.PrincipalId),
            ("@conversation", id), ("@key", key), ("@hash", hash), ("@result", JsonSerializer.Serialize(receipt)))) consume.ExecuteNonQuery();
        if (receipt.OperationId is { } commandId)
        {
            var payload = new SourceCancelRequest(commandId, offer.ReservationId, offer.SourceVersion, offer.PolicyVersion);
            using var insert = Command(db, tx, "INSERT INTO BusinessCommand VALUES(@id,@conversation,@offer,@principal,@tenant,@member,@payload,'Pending','AWAITING_SOURCE',NULL,NULL,NULL,0,@now,NULL,0,@now)",
                ("@id", commandId), ("@conversation", id), ("@offer", offerId), ("@principal", actor.PrincipalId), ("@tenant", actor.TenantId),
                ("@member", actor.MemberRef), ("@payload", JsonSerializer.Serialize(payload)), ("@now", now)); insert.ExecuteNonQuery();
        }
        Audit(db, tx, actor, id, receipt.OperationId is null ? "OFFER_REJECTED" : "COMMAND_PENDING", now); return receipt;
    });
    private static OperationView OperationRow(SqliteDataReader row)
    {
        var receipt = row.IsDBNull(3) ? null : JsonSerializer.Deserialize<SourceCancelReceipt>(row.GetString(3));
        return new(Guid.Parse(row.GetString(0)), Guid.Parse(row.GetString(1)), JsonSerializer.Deserialize<SourceCancelRequest>(row.GetString(2))!.ReservationId,
            row.GetString(4), row.GetString(5), receipt?.SourceReference, row.GetInt64(6) != 0);
    }
    private const string OperationColumns = "Id,ConversationId,PayloadJson,ReceiptJson,Status,ReasonCode,RequiresHumanReview";
    public Task<ActionSnapshot> ActionsAsync(Actor supplied, Guid id, DateTimeOffset now, CancellationToken ct) => Run(ct, (db, tx) =>
    {
        var (actor, _) = Owned(db, tx, supplied, id, now); var offers = new List<CancellationOffer>(); var operations = new List<OperationView>();
        using (var query = Command(db, tx, "SELECT * FROM Offer WHERE ConversationId=@id AND PrincipalId=@principal ORDER BY ExpiresAt DESC LIMIT 10", ("@id", id), ("@principal", actor.PrincipalId)))
        using (var row = query.ExecuteReader()) while (row.Read()) { var offer = OfferRow(row); offers.Add(offer.Status == "Active" && offer.ExpiresAt <= now ? offer with { Status = "Expired" } : offer); }
        using (var query = Command(db, tx, "SELECT " + OperationColumns + " FROM BusinessCommand WHERE ConversationId=@id AND PrincipalId=@principal ORDER BY CreatedAt DESC LIMIT 20", ("@id", id), ("@principal", actor.PrincipalId)))
        using (var row = query.ExecuteReader()) while (row.Read()) operations.Add(OperationRow(row));
        return new ActionSnapshot(offers, operations);
    });
    public Task<OperationView> OperationAsync(Actor supplied, Guid operationId, DateTimeOffset now, CancellationToken ct) => Run(ct, (db, tx) =>
    {
        var actor = Require(db, tx, supplied, now); OperationView result;
        using (var query = Command(db, tx, "SELECT " + OperationColumns + " FROM BusinessCommand WHERE Id=@id AND PrincipalId=@principal AND TenantId=@tenant",
            ("@id", operationId), ("@principal", actor.PrincipalId), ("@tenant", actor.TenantId)))
        using (var row = query.ExecuteReader()) { if (!row.Read()) throw new RequestRejected(404, "RESOURCE_NOT_FOUND"); result = OperationRow(row); }
        _ = Owned(db, tx, actor, result.ConversationId, now); return result;
    });
    public Task<DispatchLease?> ClaimAsync(DateTimeOffset now, bool killSwitch, CancellationToken ct) => Run<DispatchLease?>(ct, (db, tx) =>
    {
        Guid id; string status, member, payload, tenant; Guid principal, conversation; int attempts; DateTimeOffset submitted;
        using (var query = Command(db, tx, """
            SELECT Id,Status,MemberRef,PayloadJson,TenantId,PrincipalId,ConversationId,Attempts,COALESCE(SubmittedAt,@now)
            FROM BusinessCommand WHERE Status IN ('Pending','Submitted','Unknown') AND NextAttempt<=@now AND (LeaseUntil IS NULL OR LeaseUntil<=@now)
            ORDER BY CreatedAt LIMIT 1
            """, ("@now", now)))
        using (var row = query.ExecuteReader())
        {
            if (!row.Read()) return null; id = Guid.Parse(row.GetString(0)); status = row.GetString(1); member = row.GetString(2); payload = row.GetString(3);
            tenant = row.GetString(4); principal = Guid.Parse(row.GetString(5)); conversation = Guid.Parse(row.GetString(6)); attempts = row.GetInt32(7) + 1;
            submitted = DateTimeOffset.FromUnixTimeMilliseconds(row.GetInt64(8));
        }
        if (status == "Pending")
        {
            using var control = Command(db, tx, "SELECT Ownership FROM ConversationControl WHERE ConversationId=@id", ("@id", conversation));
            var humanOwned = control.ExecuteScalar() is string owner && owner != "AI";
            using var binding = Command(db, tx, "SELECT COUNT(*) FROM Principal WHERE Id=@id AND TenantId=@tenant AND MemberRef=@member AND Active=1 AND (AllowedRoles & 1)=1",
                ("@id", principal), ("@tenant", tenant), ("@member", member));
            if (killSwitch || humanOwned || (long)binding.ExecuteScalar()! != 1)
            {
                using var reject = Command(db, tx, "UPDATE BusinessCommand SET Status='Rejected',ReasonCode=@reason WHERE Id=@id", ("@id", id), ("@reason", killSwitch ? "TRANSACTIONS_DISABLED" : humanOwned ? "HUMAN_OWNS_CONVERSATION" : "AUTHORIZATION_REVOKED")); reject.ExecuteNonQuery();
                WorkerAudit(db, tx, tenant, id, "COMMAND_REJECTED", now); return null;
            }
        }
        var lease = Guid.NewGuid();
        using var claim = Command(db, tx, "UPDATE BusinessCommand SET Status='Submitted',LeaseId=@lease,LeaseUntil=@until,Attempts=@attempts,SubmittedAt=COALESCE(SubmittedAt,@now) WHERE Id=@id",
            ("@lease", lease), ("@until", now.AddSeconds(20)), ("@attempts", attempts), ("@now", now), ("@id", id)); claim.ExecuteNonQuery();
        WorkerAudit(db, tx, tenant, id, status == "Pending" ? "COMMAND_SUBMITTED" : "COMMAND_RECONCILING", now);
        return new DispatchLease(id, lease, member, JsonSerializer.Deserialize<SourceCancelRequest>(payload)!, status != "Pending", attempts, submitted);
    });
    private static void WorkerAudit(SqliteConnection db, SqliteTransaction tx, string tenant, Guid operation, string code, DateTimeOffset now)
    {
        using var insert = Command(db, tx, "INSERT INTO Audit VALUES(@id,@tenant,@worker,@resource,@code,@now)",
            ("@id", Guid.NewGuid()), ("@tenant", tenant), ("@worker", Guid.Empty), ("@resource", operation), ("@code", code), ("@now", now)); insert.ExecuteNonQuery();
    }
    public async Task FinishAsync(DispatchLease lease, string status, string reason, SourceCancelReceipt? receipt, DateTimeOffset now, CancellationToken ct) => await Run(ct, (db, tx) =>
    {
        if (status is not ("Completed" or "Rejected" or "Conflict" or "Unknown")) throw new ArgumentException("Invalid final observation.");
        string tenant;
        using (var lookup = Command(db, tx, "SELECT TenantId FROM BusinessCommand WHERE Id=@id AND LeaseId=@lease AND Status='Submitted'", ("@id", lease.OperationId), ("@lease", lease.LeaseId)))
            if (lookup.ExecuteScalar() is string value) tenant = value; else return 0;
        var human = status == "Unknown" && now - lease.SubmittedAt >= TimeSpan.FromSeconds(60);
        using var update = Command(db, tx, """
            UPDATE BusinessCommand SET Status=@status,ReasonCode=@reason,ReceiptJson=@receipt,LeaseId=NULL,LeaseUntil=NULL,NextAttempt=@next,
                RequiresHumanReview=MAX(RequiresHumanReview,@human) WHERE Id=@id AND LeaseId=@lease
            """, ("@status", status), ("@reason", reason), ("@receipt", receipt is null ? null : JsonSerializer.Serialize(receipt)),
            ("@next", now.AddSeconds(Math.Min(30, Math.Pow(2, Math.Min(lease.Attempts, 5))))), ("@human", human ? 1 : 0), ("@id", lease.OperationId), ("@lease", lease.LeaseId));
        var count = update.ExecuteNonQuery(); WorkerAudit(db, tx, tenant, lease.OperationId, "COMMAND_" + status.ToUpperInvariant(), now);
        if (human)
        {
            using var review = Command(db, tx, "INSERT OR IGNORE INTO HumanReview VALUES(@id,'Open',@now,NULL)", ("@id", lease.OperationId), ("@now", now)); review.ExecuteNonQuery();
            WorkerAudit(db, tx, tenant, lease.OperationId, "HUMAN_REVIEW_REQUIRED", now);
        }
        if (status is "Completed" or "Rejected" or "Conflict")
        { using var resolved = Command(db, tx, "UPDATE HumanReview SET Status='Resolved',ResolvedAt=@now WHERE OperationId=@id", ("@id", lease.OperationId), ("@now", now)); resolved.ExecuteNonQuery(); }
        return count;
    });
    private static void RequireAiOwnership(SqliteConnection db, SqliteTransaction tx, Guid conversation)
    {
        using var query = Command(db, tx, "SELECT Ownership FROM ConversationControl WHERE ConversationId=@id", ("@id", conversation));
        if (query.ExecuteScalar() is string state && state != "AI") throw new RequestRejected(409, "HUMAN_OWNS_CONVERSATION");
    }
}
