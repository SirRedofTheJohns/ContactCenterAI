using System.Data;
using ContactCenterAI.Application;
using ContactCenterAI.Domain;
using Microsoft.Data.SqlClient;

namespace ContactCenterAI.Infrastructure;

public sealed class SqlOperationalStore(string password) : IOperationalStore, IRuntimeReadiness
{
    private string ConnectionString => new SqlConnectionStringBuilder
    {
        DataSource = "tcp:127.0.0.1,14333", InitialCatalog = "ContactCenterAI_Operations",
        UserID = "ccai_app", Password = password, Encrypt = SqlConnectionEncryptOption.Mandatory,
        // Synthetic local self-signed server only; endpoint/database are not caller configurable.
        TrustServerCertificate = true, ConnectTimeout = 5, ApplicationName = "ContactCenterAI.Api", Pooling = true
    }.ConnectionString;

    private async Task<SqlConnection> Open(CancellationToken ct)
    {
        var connection = new SqlConnection(ConnectionString);
        try { await connection.OpenAsync(ct); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }
    private static SqlCommand Command(SqlConnection db, SqlTransaction? tx, string sql, params (string Name, object? Value)[] values)
    {
        var command = db.CreateCommand(); command.Transaction = tx; command.CommandText = sql; command.CommandTimeout = 5;
        foreach (var (name, value) in values)
        {
            var parameter = value switch
            {
                Guid => new SqlParameter(name, SqlDbType.UniqueIdentifier),
                long => new SqlParameter(name, SqlDbType.BigInt),
                int => new SqlParameter(name, SqlDbType.Int),
                DateTimeOffset => new SqlParameter(name, SqlDbType.DateTimeOffset),
                _ => new SqlParameter(name, SqlDbType.NVarChar, value is string text ? Math.Max(1, text.Length) : 128)
            };
            parameter.Value = value ?? DBNull.Value; command.Parameters.Add(parameter);
        }
        return command;
    }
    private static async Task<T> Guard<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (SqlException) { throw new OperationalUnavailable(); }
    }
    private static Guid? NullableGuid(SqlDataReader row, int index) => row.IsDBNull(index) ? null : row.GetGuid(index);
    private static async Task<Actor?> Session(SqlConnection db, SqlTransaction? tx, Guid id, DateTimeOffset now, CancellationToken ct)
    {
        await using var command = Command(db, tx, """
            SELECT s.Id,s.TenantId,s.PrincipalId,p.MemberRef,(s.Roles & ISNULL(p.AllowedRoles,0)),s.ExpiresAt
            FROM dbo.UserSession s LEFT JOIN dbo.Principal p ON p.Id=s.PrincipalId AND p.TenantId=s.TenantId
            WHERE s.Id=@id AND s.ExpiresAt>@now AND s.RevokedAt IS NULL AND (s.PrincipalId IS NULL OR p.Active=1)
            """, ("@id", id), ("@now", now));
        await using var row = await command.ExecuteReaderAsync(ct);
        return await row.ReadAsync(ct) ? new Actor(row.GetGuid(0), row.GetString(1), NullableGuid(row, 2),
            row.IsDBNull(3) ? null : row.GetString(3), (ActorRoles)row.GetInt32(4), row.GetFieldValue<DateTimeOffset>(5)) : null;
    }
    private static async Task<Actor> RequireSession(SqlConnection db, SqlTransaction tx, Actor supplied, DateTimeOffset now, CancellationToken ct)
    {
        var actor = await Session(db, tx, supplied.SessionId, now, ct);
        if (actor is null || actor.PrincipalId != supplied.PrincipalId) throw new RequestRejected(401, "SESSION_REQUIRED");
        return actor;
    }
    private static async Task Audit(SqlConnection db, SqlTransaction tx, Actor actor, Guid? resource, string code, DateTimeOffset now, CancellationToken ct)
    {
        await using var command = Command(db, tx, "INSERT dbo.Audit VALUES(@id,@tenant,@session,@resource,@code,@now)",
            ("@id", Guid.NewGuid()), ("@tenant", actor.TenantId), ("@session", actor.SessionId), ("@resource", resource), ("@code", code), ("@now", now));
        // NULL resource must retain its SQL GUID type.
        command.Parameters["@resource"].SqlDbType = SqlDbType.UniqueIdentifier;
        await command.ExecuteNonQueryAsync(ct);
    }
    public Task<Actor?> FindSessionAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken) => Guard(async () =>
    {
        await using var db = await Open(cancellationToken); return await Session(db, null, sessionId, now, cancellationToken);
    });
    public Task<Actor> CreateGuestAsync(DateTimeOffset now, CancellationToken cancellationToken) => Guard(async () =>
    {
        var actor = new Actor(Guid.NewGuid(), "tenant-demo", null, null, ActorRoles.None, now.AddMinutes(15));
        await using var db = await Open(cancellationToken);
        await using var tx = (SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await using var command = Command(db, tx, "INSERT dbo.UserSession VALUES(@id,@tenant,NULL,0,@expiry,NULL)",
            ("@id", actor.SessionId), ("@tenant", actor.TenantId), ("@expiry", actor.ExpiresAt));
        await command.ExecuteNonQueryAsync(cancellationToken);
        await Audit(db, tx, actor, null, "SESSION_CREATED", now, cancellationToken);
        await tx.CommitAsync(cancellationToken); return actor;
    });
    public Task<Actor> LoginAsync(string issuer, string subject, ActorRoles validatedRoles, Guid? previousSession, DateTimeOffset now, CancellationToken cancellationToken) => Guard(async () =>
    {
        await using var db = await Open(cancellationToken);
        await using var tx = (SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        Actor actor;
        await using (var command = Command(db, tx, "SELECT Id,TenantId,MemberRef,AllowedRoles FROM dbo.Principal WHERE Issuer=@issuer AND Subject=@subject AND Active=1", ("@issuer", issuer), ("@subject", subject)))
        await using (var row = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await row.ReadAsync(cancellationToken)) throw new RequestRejected(403, "BINDING_REQUIRED");
            var roles = validatedRoles & (ActorRoles)row.GetInt32(3);
            if (roles == ActorRoles.None) throw new RequestRejected(403, "ROLE_REQUIRED");
            actor = new Actor(Guid.NewGuid(), row.GetString(1), row.GetGuid(0), row.IsDBNull(2) ? null : row.GetString(2), roles, now.AddMinutes(15));
        }
        var previous = previousSession is { } previousId ? await Session(db, tx, previousId, now, cancellationToken) : null;
        await using (var command = Command(db, tx, "INSERT dbo.UserSession VALUES(@id,@tenant,@principal,@roles,@expiry,NULL)",
            ("@id", actor.SessionId), ("@tenant", actor.TenantId), ("@principal", actor.PrincipalId), ("@roles", (int)actor.Roles), ("@expiry", actor.ExpiresAt)))
            await command.ExecuteNonQueryAsync(cancellationToken);
        if (previous is not null)
        {
            await using var command = Command(db, tx, """
                UPDATE dbo.UserSession SET RevokedAt=@now WHERE Id=@old;
                IF @adopt=1 UPDATE dbo.Conversation SET PrincipalId=@principal,GuestSessionId=NULL,Version=Version+1,Epoch=Epoch+1
                 WHERE GuestSessionId=@old AND TenantId=@tenant AND PrincipalId IS NULL;
                """, ("@now", now), ("@old", previous.SessionId), ("@adopt", previous.PrincipalId is null && actor.Roles.HasFlag(ActorRoles.Customer) ? 1 : 0),
                ("@principal", actor.PrincipalId), ("@tenant", actor.TenantId));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await Audit(db, tx, actor, null, "LOGIN_ROTATED", now, cancellationToken);
        await tx.CommitAsync(cancellationToken); return actor;
    });
    public async Task RevokeSessionAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken) => await Guard(async () =>
    {
        await using var db = await Open(cancellationToken);
        await using var tx = (SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var actor = await Session(db, tx, sessionId, now, cancellationToken);
        await using var command = Command(db, tx, "UPDATE dbo.UserSession SET RevokedAt=@now WHERE Id=@id AND RevokedAt IS NULL", ("@id", sessionId), ("@now", now));
        var changed = await command.ExecuteNonQueryAsync(cancellationToken);
        if (actor is not null) await Audit(db, tx, actor, null, "SESSION_REVOKED", now, cancellationToken);
        await tx.CommitAsync(cancellationToken); return changed;
    });
    public Task<ConversationReceipt> CreateConversationAsync(Actor supplied, string language, string keyHash, string payloadHash, DateTimeOffset now, CancellationToken cancellationToken) => Guard(async () =>
    {
        await using var db = await Open(cancellationToken);
        await using var tx = (SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var actor = await RequireSession(db, tx, supplied, now, cancellationToken);
        if (actor.PrincipalId is not null && (!actor.Roles.HasFlag(ActorRoles.Customer) || actor.MemberRef is null)) throw new RequestRejected(403, "CUSTOMER_SESSION_REQUIRED");
        var scope = actor.PrincipalId ?? actor.SessionId;
        await using (var lookup = Command(db, tx, "SELECT PayloadHash,ConversationId FROM dbo.Idempotency WITH(UPDLOCK,HOLDLOCK) WHERE ScopeId=@scope AND KeyHash=@key", ("@scope", scope), ("@key", keyHash)))
        await using (var row = await lookup.ExecuteReaderAsync(cancellationToken))
        {
            if (await row.ReadAsync(cancellationToken))
            {
                if (row.GetString(0) != payloadHash) throw new RequestRejected(409, "IDEMPOTENCY_CONFLICT");
                // The immutable creation receipt stays version 1, even after subsequent turns.
                return new ConversationReceipt(row.GetGuid(1), 1);
            }
        }
        var id = Guid.NewGuid();
        await using (var command = Command(db, tx, """
            INSERT dbo.Conversation VALUES(@id,@tenant,@principal,@guest,@language,1,1,'AI',@now);
            INSERT dbo.Idempotency VALUES(@scope,@key,@hash,@id);
            """, ("@id", id), ("@tenant", actor.TenantId), ("@principal", actor.PrincipalId),
            ("@guest", actor.PrincipalId is null ? actor.SessionId : null), ("@language", language), ("@now", now),
            ("@scope", scope), ("@key", keyHash), ("@hash", payloadHash)))
        {
            command.Parameters["@principal"].SqlDbType = command.Parameters["@guest"].SqlDbType = SqlDbType.UniqueIdentifier;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await Audit(db, tx, actor, id, "CONVERSATION_CREATED", now, cancellationToken);
        await tx.CommitAsync(cancellationToken); return new ConversationReceipt(id, 1);
    });
    private static async Task<(ConversationResource Resource, string Language)?> Resource(SqlConnection db, SqlTransaction tx, Actor actor, Guid id, DateTimeOffset now, CancellationToken ct)
    {
        ConversationResource resource; string language;
        await using (var command = Command(db, tx, "SELECT Id,TenantId,PrincipalId,GuestSessionId,Version,Epoch,Language FROM dbo.Conversation WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id AND TenantId=@tenant", ("@id", id), ("@tenant", actor.TenantId)))
        await using (var row = await command.ExecuteReaderAsync(ct))
        {
            if (!await row.ReadAsync(ct)) return null;
            resource = new ConversationResource(row.GetGuid(0), row.GetString(1), NullableGuid(row, 2), NullableGuid(row, 3), row.GetInt64(4), row.GetInt64(5)); language = row.GetString(6);
        }
        ActiveAssignment? assignment = null;
        if (actor.PrincipalId is { } principal && (actor.Roles & (ActorRoles.Agent | ActorRoles.Supervisor)) != 0)
        {
            await using var command = Command(db, tx, "SELECT TOP(1) StartsAt,EndsAt,Revoked FROM dbo.Assignment WHERE PrincipalId=@principal AND ConversationId=@id AND TenantId=@tenant AND Revoked=0 AND StartsAt<=@now AND EndsAt>@now",
                ("@principal", principal), ("@id", id), ("@tenant", actor.TenantId), ("@now", now));
            await using var row = await command.ExecuteReaderAsync(ct);
            if (await row.ReadAsync(ct)) assignment = new ActiveAssignment(principal, id, actor.TenantId, row.GetFieldValue<DateTimeOffset>(0), row.GetFieldValue<DateTimeOffset>(1), row.GetBoolean(2));
        }
        return ResourceAccess.CanAccess(actor, resource, assignment, now) ? (resource, language) : null;
    }
    public Task<ConversationSnapshot?> ReadConversationAsync(Actor supplied, Guid id, DateTimeOffset now, CancellationToken cancellationToken) => Guard(async () =>
    {
        await using var db = await Open(cancellationToken);
        await using var tx = (SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var actor = await RequireSession(db, tx, supplied, now, cancellationToken);
        var found = await Resource(db, tx, actor, id, now, cancellationToken);
        if (found is null) return null;
        var messages = new List<StoredMessage>();
        await using (var command = Command(db, tx, "SELECT TOP(100) Id,ClientMessageId,SanitizedText,TurnId FROM dbo.Message WHERE ConversationId=@id ORDER BY AcceptedVersion", ("@id", id)))
        await using (var row = await command.ExecuteReaderAsync(cancellationToken))
            while (await row.ReadAsync(cancellationToken)) messages.Add(new StoredMessage(row.GetGuid(0), row.GetGuid(1), row.GetString(2), row.GetGuid(3)));
        await tx.CommitAsync(cancellationToken);
        return new ConversationSnapshot(found.Value.Resource, found.Value.Language, messages);
    });
    public Task<MessageReceipt> SubmitMessageAsync(Actor supplied, Guid id, Guid clientMessageId, string sanitizedText, string payloadHash, long expectedVersion, DateTimeOffset now, CancellationToken cancellationToken) => Guard(async () =>
    {
        await using var db = await Open(cancellationToken);
        await using var tx = (SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var actor = await RequireSession(db, tx, supplied, now, cancellationToken);
        var found = await Resource(db, tx, actor, id, now, cancellationToken) ?? throw new RequestRejected(404, "RESOURCE_NOT_FOUND");
        await using (var lookup = Command(db, tx, "SELECT PayloadHash,Id,TurnId,AcceptedVersion FROM dbo.Message WHERE ConversationId=@id AND ClientMessageId=@client", ("@id", id), ("@client", clientMessageId)))
        await using (var row = await lookup.ExecuteReaderAsync(cancellationToken))
        {
            if (await row.ReadAsync(cancellationToken))
            {
                if (row.GetString(0) != payloadHash) throw new RequestRejected(409, "MESSAGE_ID_CONFLICT");
                return new MessageReceipt(row.GetGuid(1), row.GetGuid(2), row.GetInt64(3));
            }
        }
        if (found.Resource.Version != expectedVersion) throw new RequestRejected(409, "VERSION_CONFLICT");
        var version = expectedVersion + 1; var messageId = Guid.NewGuid(); var turnId = Guid.NewGuid();
        await using (var command = Command(db, tx, """
            UPDATE dbo.Conversation SET Version=@version WHERE Id=@id;
            INSERT dbo.Message VALUES(@message,@id,@client,@text,@hash,@turn,@version,@now);
            INSERT dbo.Inbox VALUES(@turn,@id,@message,'Pending',@now);
            """, ("@version", version), ("@id", id), ("@message", messageId), ("@client", clientMessageId),
            ("@text", sanitizedText), ("@hash", payloadHash), ("@turn", turnId), ("@now", now)))
            await command.ExecuteNonQueryAsync(cancellationToken);
        await Audit(db, tx, actor, id, "MESSAGE_ACCEPTED", now, cancellationToken);
        await tx.CommitAsync(cancellationToken); return new MessageReceipt(messageId, turnId, version);
    });
    public async ValueTask<RuntimeReadiness> CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await Open(cancellationToken);
            await using var command = Command(db, null, "SELECT Version FROM dbo.SchemaVersion WHERE Version=1");
            return (await command.ExecuteScalarAsync(cancellationToken)) is 1 ? new(true, "OPERATIONAL_STORE_READY") : new(false, "OPERATIONAL_SCHEMA_REQUIRED");
        }
        catch (SqlException) { return new(false, "OPERATIONAL_STORE_UNAVAILABLE"); }
    }
}
