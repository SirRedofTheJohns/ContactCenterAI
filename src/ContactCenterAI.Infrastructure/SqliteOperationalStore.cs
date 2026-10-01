using System.Globalization;
using ContactCenterAI.Application;
using ContactCenterAI.Domain;
using Microsoft.Data.Sqlite;

namespace ContactCenterAI.Infrastructure;

// Persistent local presentation adapter. SQL Server stays the enterprise adapter.
public sealed partial class SqliteOperationalStore : IOperationalStore, IRuntimeReadiness, ICancellationStore, ICommandLedger
{
    private const string Issuer = "http://localhost:8080/realms/contactcenterai-local";
    private readonly string connectionString;
    // SQLite permits one writer. Await locally instead of parking request-pool threads in busy waits;
    // database transactions/constraints remain the authority across independent processes and instances.
    private readonly SemaphoreSlim localAccess = new(1, 1);
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private readonly ITextEmbeddingProvider? embeddings;
    private readonly IKnowledgeReranker? reranker;
    public SqliteOperationalStore(string path,ITextEmbeddingProvider? embeddings=null,IKnowledgeReranker? reranker=null)
    {
        this.embeddings=embeddings;
        this.reranker=reranker;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true, DefaultTimeout = 5 }.ToString();
        using var db = Open();
        using var command = Command(db, null, """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS Principal(Id TEXT PRIMARY KEY,TenantId TEXT NOT NULL,Issuer TEXT NOT NULL,Subject TEXT NOT NULL,MemberRef TEXT,AllowedRoles INTEGER NOT NULL,Active INTEGER NOT NULL,UNIQUE(Issuer,Subject),UNIQUE(Id,TenantId));
            CREATE TABLE IF NOT EXISTS UserSession(Id TEXT PRIMARY KEY,TenantId TEXT NOT NULL,PrincipalId TEXT,Roles INTEGER NOT NULL,ExpiresAt INTEGER NOT NULL,RevokedAt INTEGER,FOREIGN KEY(PrincipalId,TenantId) REFERENCES Principal(Id,TenantId),CHECK(PrincipalId IS NOT NULL OR Roles=0));
            CREATE TABLE IF NOT EXISTS Conversation(Id TEXT PRIMARY KEY,TenantId TEXT NOT NULL,PrincipalId TEXT,GuestSessionId TEXT,Language TEXT NOT NULL CHECK(Language IN ('es','en')),Version INTEGER NOT NULL CHECK(Version>0),Epoch INTEGER NOT NULL CHECK(Epoch>0),CreatedAt INTEGER NOT NULL,FOREIGN KEY(PrincipalId,TenantId) REFERENCES Principal(Id,TenantId),FOREIGN KEY(GuestSessionId) REFERENCES UserSession(Id),CHECK((PrincipalId IS NULL AND GuestSessionId IS NOT NULL) OR (PrincipalId IS NOT NULL AND GuestSessionId IS NULL)));
            CREATE TABLE IF NOT EXISTS Assignment(Id TEXT PRIMARY KEY,TenantId TEXT NOT NULL,PrincipalId TEXT NOT NULL,ConversationId TEXT NOT NULL,StartsAt INTEGER NOT NULL,EndsAt INTEGER NOT NULL,Revoked INTEGER NOT NULL,FOREIGN KEY(PrincipalId,TenantId) REFERENCES Principal(Id,TenantId),FOREIGN KEY(ConversationId) REFERENCES Conversation(Id));
            CREATE TABLE IF NOT EXISTS Idempotency(ScopeId TEXT NOT NULL,KeyHash TEXT NOT NULL,PayloadHash TEXT NOT NULL,ConversationId TEXT NOT NULL,PRIMARY KEY(ScopeId,KeyHash),FOREIGN KEY(ConversationId) REFERENCES Conversation(Id));
            CREATE TABLE IF NOT EXISTS Message(Id TEXT PRIMARY KEY,ConversationId TEXT NOT NULL,ClientMessageId TEXT NOT NULL,SanitizedText TEXT NOT NULL,PayloadHash TEXT NOT NULL,TurnId TEXT NOT NULL,AcceptedVersion INTEGER NOT NULL,CreatedAt INTEGER NOT NULL,UNIQUE(ConversationId,ClientMessageId),FOREIGN KEY(ConversationId) REFERENCES Conversation(Id));
            CREATE TABLE IF NOT EXISTS Inbox(Id TEXT PRIMARY KEY,ConversationId TEXT NOT NULL,MessageId TEXT NOT NULL UNIQUE,Status TEXT NOT NULL,CreatedAt INTEGER NOT NULL,FOREIGN KEY(MessageId) REFERENCES Message(Id),FOREIGN KEY(ConversationId) REFERENCES Conversation(Id));
            CREATE TABLE IF NOT EXISTS Audit(Id TEXT PRIMARY KEY,TenantId TEXT NOT NULL,SessionId TEXT NOT NULL,ResourceId TEXT,Code TEXT NOT NULL,OccurredAt INTEGER NOT NULL);
            PRAGMA user_version=1;
            """);
        command.ExecuteNonQuery();
        InitializeTransactions(db);
        InitializeKnowledge(db);
        InitializeSemanticKnowledge(db);
        InitializeAssistant(db);
        using var tx = db.BeginTransaction(deferred: false);
        foreach (var (number, role) in new[] { (1,1), (2,1), (3,2), (4,2), (5,4), (6,8), (7,16), (8,32) })
        {
            var subject = $"10000000-0000-0000-0000-{number:D12}";
            using var seed = Command(db, tx, "INSERT OR IGNORE INTO Principal VALUES(@id,'tenant-demo',@issuer,@id,@member,@roles,1)",
                ("@id", subject), ("@issuer", Issuer), ("@member", number <= 2 ? $"MEM-{number:D3}" : null), ("@roles", role));
            seed.ExecuteNonQuery();
        }
        tx.Commit();
    }
    private SqliteConnection Open() { var db = new SqliteConnection(connectionString); db.Open(); return db; }
    private static SqliteCommand Command(SqliteConnection db, SqliteTransaction? tx, string sql, params (string Name, object? Value)[] parameters)
    {
        var command = db.CreateCommand(); command.Transaction = tx; command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value switch { Guid id => id.ToString(), DateTimeOffset date => date.ToUnixTimeMilliseconds(), null => DBNull.Value, _ => value });
        return command;
    }
    private async Task<T> Run<T>(CancellationToken ct, Func<SqliteConnection, SqliteTransaction, T> action)
    {
        await localAccess.WaitAsync(ct);
        try
        {
            using var db = Open(); using var tx = db.BeginTransaction(deferred: false);
            var result = action(db, tx); tx.Commit(); return result;
        }
        catch (SqliteException error) { throw new OperationalUnavailable(error.SqliteErrorCode); }
        finally { localAccess.Release(); }
    }
    private static Guid? NullableGuid(SqliteDataReader row, int index) => row.IsDBNull(index) ? null : Guid.Parse(row.GetString(index));
    private static Actor? Session(SqliteConnection db, SqliteTransaction tx, Guid id, DateTimeOffset now)
    {
        using var command = Command(db, tx, """
            SELECT s.Id,s.TenantId,s.PrincipalId,p.MemberRef,(s.Roles & COALESCE(p.AllowedRoles,0)),s.ExpiresAt
            FROM UserSession s LEFT JOIN Principal p ON p.Id=s.PrincipalId AND p.TenantId=s.TenantId
            WHERE s.Id=@id AND s.RevokedAt IS NULL AND s.ExpiresAt>@now AND (s.PrincipalId IS NULL OR p.Active=1)
            """, ("@id", id), ("@now", now));
        using var row = command.ExecuteReader();
        return row.Read() ? new Actor(Guid.Parse(row.GetString(0)), row.GetString(1), NullableGuid(row, 2), row.IsDBNull(3) ? null : row.GetString(3),
            (ActorRoles)row.GetInt32(4), DateTimeOffset.FromUnixTimeMilliseconds(row.GetInt64(5))) : null;
    }
    private static Actor Require(SqliteConnection db, SqliteTransaction tx, Actor supplied, DateTimeOffset now)
    {
        var actor = Session(db, tx, supplied.SessionId, now);
        return actor is not null && actor.PrincipalId == supplied.PrincipalId ? actor : throw new RequestRejected(401, "SESSION_REQUIRED");
    }
    private static void Audit(SqliteConnection db, SqliteTransaction tx, Actor actor, Guid? resource, string code, DateTimeOffset now)
    {
        using var command = Command(db, tx, "INSERT INTO Audit VALUES(@id,@tenant,@session,@resource,@code,@now)", ("@id", Guid.NewGuid()),
            ("@tenant", actor.TenantId), ("@session", actor.SessionId), ("@resource", resource), ("@code", code), ("@now", now));
        command.ExecuteNonQuery();
    }
    public Task<Actor?> FindSessionAsync(Guid id, DateTimeOffset now, CancellationToken ct) => Run(ct, (db, tx) => Session(db, tx, id, now));
    public Task<Actor> CreateGuestAsync(DateTimeOffset now, CancellationToken ct) => Run(ct, (db, tx) =>
    {
        var actor = new Actor(Guid.NewGuid(), "tenant-demo", null, null, ActorRoles.None, now.AddMinutes(15));
        using var command = Command(db, tx, "INSERT INTO UserSession VALUES(@id,@tenant,NULL,0,@expiry,NULL)", ("@id", actor.SessionId), ("@tenant", actor.TenantId), ("@expiry", actor.ExpiresAt));
        command.ExecuteNonQuery(); Audit(db, tx, actor, null, "SESSION_CREATED", now); return actor;
    });
    public Task<Actor> LoginAsync(string issuer, string subject, ActorRoles roles, Guid? previousId, DateTimeOffset now, CancellationToken ct) => Run(ct, (db, tx) =>
    {
        Actor actor;
        using (var binding = Command(db, tx, "SELECT Id,TenantId,MemberRef,AllowedRoles FROM Principal WHERE Issuer=@issuer AND Subject=@subject AND Active=1", ("@issuer", issuer), ("@subject", subject)))
        using (var row = binding.ExecuteReader())
        {
            if (!row.Read()) throw new RequestRejected(403, "BINDING_REQUIRED");
            var allowed = roles & (ActorRoles)row.GetInt32(3);
            if (allowed == ActorRoles.None) throw new RequestRejected(403, "ROLE_REQUIRED");
            actor = new Actor(Guid.NewGuid(), row.GetString(1), Guid.Parse(row.GetString(0)), row.IsDBNull(2) ? null : row.GetString(2), allowed, now.AddMinutes(15));
        }
        var previous = previousId is { } oldId ? Session(db, tx, oldId, now) : null;
        using (var insert = Command(db, tx, "INSERT INTO UserSession VALUES(@id,@tenant,@principal,@roles,@expiry,NULL)", ("@id", actor.SessionId),
            ("@tenant", actor.TenantId), ("@principal", actor.PrincipalId), ("@roles", (int)actor.Roles), ("@expiry", actor.ExpiresAt))) insert.ExecuteNonQuery();
        if (previous is not null)
        {
            using var revoke = Command(db, tx, "UPDATE UserSession SET RevokedAt=@now WHERE Id=@old", ("@now", now), ("@old", previous.SessionId)); revoke.ExecuteNonQuery();
            if (previous.PrincipalId is null && actor.Roles.HasFlag(ActorRoles.Customer))
            {
                using var adopt = Command(db, tx, "UPDATE Conversation SET PrincipalId=@principal,GuestSessionId=NULL,Version=Version+1,Epoch=Epoch+1 WHERE GuestSessionId=@old AND TenantId=@tenant AND PrincipalId IS NULL",
                    ("@principal", actor.PrincipalId), ("@old", previous.SessionId), ("@tenant", actor.TenantId)); adopt.ExecuteNonQuery();
            }
        }
        Audit(db, tx, actor, null, "LOGIN_ROTATED", now); return actor;
    });
    public async Task RevokeSessionAsync(Guid id, DateTimeOffset now, CancellationToken ct) => await Run(ct, (db, tx) =>
    {
        var actor = Session(db, tx, id, now);
        using var command = Command(db, tx, "UPDATE UserSession SET RevokedAt=@now WHERE Id=@id", ("@id", id), ("@now", now));
        var count = command.ExecuteNonQuery(); if (actor is not null) Audit(db, tx, actor, null, "SESSION_REVOKED", now); return count;
    });
    public Task<ConversationReceipt> CreateConversationAsync(Actor supplied, string language, string key, string hash, DateTimeOffset now, CancellationToken ct) => Run(ct, (db, tx) =>
    {
        var actor = Require(db, tx, supplied, now);
        if (actor.PrincipalId is not null && (!actor.Roles.HasFlag(ActorRoles.Customer) || actor.MemberRef is null)) throw new RequestRejected(403, "CUSTOMER_SESSION_REQUIRED");
        var scope = actor.PrincipalId ?? actor.SessionId;
        using (var lookup = Command(db, tx, "SELECT PayloadHash,ConversationId FROM Idempotency WHERE ScopeId=@scope AND KeyHash=@key", ("@scope", scope), ("@key", key)))
        using (var row = lookup.ExecuteReader())
            if (row.Read()) return row.GetString(0) == hash ? new ConversationReceipt(Guid.Parse(row.GetString(1)), 1) : throw new RequestRejected(409, "IDEMPOTENCY_CONFLICT");
        var id = Guid.NewGuid();
        using var command = Command(db, tx, """
            INSERT INTO Conversation VALUES(@id,@tenant,@principal,@guest,@language,1,1,@now);
            INSERT INTO Idempotency VALUES(@scope,@key,@hash,@id);
            """, ("@id", id), ("@tenant", actor.TenantId), ("@principal", actor.PrincipalId), ("@guest", actor.PrincipalId is null ? actor.SessionId : null),
            ("@language", language), ("@now", now), ("@scope", scope), ("@key", key), ("@hash", hash));
        command.ExecuteNonQuery(); EnsureControl(db, tx, id); Audit(db, tx, actor, id, "CONVERSATION_CREATED", now); return new ConversationReceipt(id, 1);
    });
    private static (ConversationResource Resource, string Language)? Resource(SqliteConnection db, SqliteTransaction tx, Actor actor, Guid id, DateTimeOffset now)
    {
        ConversationResource resource; string language;
        using (var command = Command(db, tx, "SELECT Id,TenantId,PrincipalId,GuestSessionId,Version,Epoch,Language FROM Conversation WHERE Id=@id AND TenantId=@tenant", ("@id", id), ("@tenant", actor.TenantId)))
        using (var row = command.ExecuteReader())
        {
            if (!row.Read()) return null;
            resource = new(Guid.Parse(row.GetString(0)), row.GetString(1), NullableGuid(row, 2), NullableGuid(row, 3), row.GetInt64(4), row.GetInt64(5)); language = row.GetString(6);
        }
        ActiveAssignment? assignment = null;
        if (actor.PrincipalId is { } principal && (actor.Roles & (ActorRoles.Agent | ActorRoles.Supervisor)) != 0)
        {
            using var lookup = Command(db, tx, "SELECT StartsAt,EndsAt FROM Assignment WHERE PrincipalId=@principal AND ConversationId=@id AND TenantId=@tenant AND Revoked=0 AND StartsAt<=@now AND EndsAt>@now LIMIT 1", ("@principal", principal), ("@id", id), ("@tenant", actor.TenantId), ("@now", now));
            using var row = lookup.ExecuteReader();
            if (row.Read()) assignment = new(principal, id, actor.TenantId, DateTimeOffset.FromUnixTimeMilliseconds(row.GetInt64(0)), DateTimeOffset.FromUnixTimeMilliseconds(row.GetInt64(1)), false);
        }
        return ResourceAccess.CanAccess(actor, resource, assignment, now) ? (resource, language) : null;
    }
    public Task<ConversationSnapshot?> ReadConversationAsync(Actor supplied, Guid id, DateTimeOffset now, CancellationToken ct) => Run<ConversationSnapshot?>(ct, (db, tx) =>
    {
        var actor = Require(db, tx, supplied, now); var resource = Resource(db, tx, actor, id, now); if (resource is null) return null;
        var messages = new List<StoredMessage>();
        using var command = Command(db, tx, "SELECT Id,ClientMessageId,SanitizedText,TurnId FROM Message WHERE ConversationId=@id ORDER BY AcceptedVersion LIMIT 100", ("@id", id));
        using var row = command.ExecuteReader();
        while (row.Read()) messages.Add(new(Guid.Parse(row.GetString(0)), Guid.Parse(row.GetString(1)), row.GetString(2), Guid.Parse(row.GetString(3))));
        return new ConversationSnapshot(resource.Value.Resource, resource.Value.Language, messages);
    });
    public Task<MessageReceipt> SubmitMessageAsync(Actor supplied, Guid id, Guid client, string text, string hash, long version, DateTimeOffset now, CancellationToken ct) => Run(ct, (db, tx) =>
    {
        var actor = Require(db, tx, supplied, now); var resource = Resource(db, tx, actor, id, now) ?? throw new RequestRejected(404, "RESOURCE_NOT_FOUND");
        using (var lookup = Command(db, tx, "SELECT PayloadHash,Id,TurnId,AcceptedVersion FROM Message WHERE ConversationId=@id AND ClientMessageId=@client", ("@id", id), ("@client", client)))
        using (var row = lookup.ExecuteReader())
            if (row.Read()) return row.GetString(0) == hash ? new MessageReceipt(Guid.Parse(row.GetString(1)), Guid.Parse(row.GetString(2)), row.GetInt64(3)) : throw new RequestRejected(409, "MESSAGE_ID_CONFLICT");
        if (resource.Resource.Version != version) throw new RequestRejected(409, "VERSION_CONFLICT");
        var receipt = new MessageReceipt(Guid.NewGuid(), Guid.NewGuid(), version + 1);
        using var command = Command(db, tx, """
            UPDATE Conversation SET Version=@version WHERE Id=@id;
            INSERT INTO Message VALUES(@message,@id,@client,@text,@hash,@turn,@version,@now);
            INSERT INTO Inbox VALUES(@turn,@id,@message,'Pending',@now);
            """, ("@version", receipt.Version), ("@id", id), ("@message", receipt.MessageId), ("@client", client), ("@text", text), ("@hash", hash), ("@turn", receipt.TurnId), ("@now", now));
        command.ExecuteNonQuery(); QueueTurn(db, tx, actor, id, receipt, resource.Resource.Epoch, now);
        Audit(db, tx, actor, id, "MESSAGE_ACCEPTED", now); return receipt;
    });
    public ValueTask<RuntimeReadiness> CheckAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try { using var db = Open(); using var command = Command(db, null, "PRAGMA user_version"); return ValueTask.FromResult(new RuntimeReadiness(Convert.ToInt64(command.ExecuteScalar(), Culture) == 1, "DEMO_SQLITE_READY")); }
        catch (SqliteException) { return ValueTask.FromResult(new RuntimeReadiness(false, "OPERATIONAL_STORE_UNAVAILABLE")); }
    }
}
