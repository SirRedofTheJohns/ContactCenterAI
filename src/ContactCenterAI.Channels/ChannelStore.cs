using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContactCenterAI.Application;
using ContactCenterAI.Domain;
using Microsoft.Data.Sqlite;

namespace ContactCenterAI.Channels;

public sealed record ChannelRoute(string Key, Guid Id, string Channel, string EndpointId, string SenderId,
    string Language, string Ownership, long Epoch, DateTimeOffset LastInbound);
public sealed record ChannelJob(string Key, Guid Lease, ChannelRoute Route, string Text);
public sealed record PreparedOutput(ChannelReply Reply, Actor? Guest = null, Guid? ConversationId = null);
public sealed record ChannelOutput(Guid Id, string InboundKey, ChannelRoute Route, long Epoch, PreparedOutput Output, int Attempts);

public sealed class ChannelStore
{
    private readonly string connection;
    private readonly byte[] hashingKey;
    public ChannelStore(string path, byte[] hashingKey)
    {
        if (hashingKey.Length != 32) throw new ArgumentException("CHANNEL_HASH_KEY_REQUIRED");
        this.hashingKey = hashingKey.ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        connection = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, ForeignKeys = true }.ToString();
        using var db = Open();
        Execute(db, null, """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS ChannelRoute(Key TEXT PRIMARY KEY,Id TEXT NOT NULL,Channel TEXT NOT NULL,Endpoint TEXT NOT NULL,Sender TEXT NOT NULL,Language TEXT NOT NULL,Ownership TEXT NOT NULL,Epoch INTEGER NOT NULL,LastInbound INTEGER NOT NULL,LastReceived INTEGER NOT NULL,RequestId TEXT);
            CREATE TABLE IF NOT EXISTS ChannelInbox(Key TEXT PRIMARY KEY,RouteKey TEXT,Hash TEXT NOT NULL,Text TEXT NOT NULL,Status TEXT NOT NULL,ReceivedAt INTEGER NOT NULL,Lease TEXT,LeaseUntil INTEGER,Attempts INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE IF NOT EXISTS ChannelOutbox(Id TEXT PRIMARY KEY,InboundKey TEXT NOT NULL UNIQUE,RouteKey TEXT NOT NULL,Epoch INTEGER NOT NULL,Output TEXT NOT NULL,Status TEXT NOT NULL,ProviderId TEXT,Attempts INTEGER NOT NULL DEFAULT 0,RetryAt INTEGER NOT NULL,StartedAt INTEGER);
            CREATE TABLE IF NOT EXISTS ChannelPoll(Endpoint TEXT PRIMARY KEY,NextOffset INTEGER NOT NULL);
            """);
    }
    private SqliteConnection Open() { var db = new SqliteConnection(connection); db.Open(); return db; }
    private static SqliteCommand Command(SqliteConnection db, SqliteTransaction? tx, string sql, params (string, object?)[] args)
    {
        var command = db.CreateCommand(); command.Transaction = tx; command.CommandText = sql;
        foreach (var (name, value) in args) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }
    private static void Execute(SqliteConnection db, SqliteTransaction? tx, string sql, params (string, object?)[] args)
    { using var cmd = Command(db, tx, sql, args); cmd.ExecuteNonQuery(); }
    private static string S(Guid value) => value.ToString("D");
    private static long Ms(DateTimeOffset value) => value.ToUnixTimeMilliseconds();
    private string Hash(string value) => Convert.ToHexString(HMACSHA256.HashData(hashingKey, Encoding.UTF8.GetBytes(value)));
    private string Key(string channel, string endpoint, string id) => Hash(channel + "\n" + endpoint + "\n" + id);
    public string Accept(ChannelText input, EndpointOptions endpoint)
    {
        using var db = Open(); using var tx = db.BeginTransaction(deferred: false);
        var result = Accept(db, tx, input, endpoint); tx.Commit(); return result;
    }
    private string Accept(SqliteConnection db, SqliteTransaction tx, ChannelText input, EndpointOptions endpoint)
    {
        var key = Key(input.Channel, input.EndpointId, input.EventId);
        var hash = Hash(JsonSerializer.Serialize(new { input.Channel, input.EndpointId, input.SenderId, input.OccurredAtUtc, input.Text }));
        using (var seen = Command(db, tx, "SELECT Hash FROM ChannelInbox WHERE Key=@key", ("@key", key)))
        {
            var previous = seen.ExecuteScalar() as string;
            if (previous is not null)
            {
                if (previous == hash) return "DUPLICATE";
                Execute(db, tx, "UPDATE ChannelInbox SET Status='Conflict',Text='',Lease=NULL,LeaseUntil=NULL WHERE Key=@key; UPDATE ChannelOutbox SET Status='Suppressed' WHERE InboundKey=@key AND Status='Pending'", ("@key", key));
                return "EVENT_CONFLICT";
            }
        }
        var status = "Pending"; var text = "";
        if (!endpoint.Allows(input) || input.OccurredAtUtc > input.ReceivedAtUtc.AddMinutes(5)) status = "Ignored";
        else
        {
            try { text = ConversationIngress.SanitizeText(input.Text); }
            catch (RequestRejected) { status = "Rejected"; }
        }
        var routeKey = Key(input.Channel, input.EndpointId, input.SenderId);
        if (status == "Pending")
        {
            var day = Ms(new DateTimeOffset(input.ReceivedAtUtc.UtcDateTime.Date, TimeSpan.Zero));
            using var quota = Command(db, tx, "SELECT COUNT(*),COALESCE(SUM(CASE WHEN RouteKey=@route THEN 1 ELSE 0 END),0) FROM ChannelInbox WHERE ReceivedAt>=@day AND Status NOT IN ('Ignored','Rejected','Tombstone')", ("@route", routeKey), ("@day", day));
            using (var row = quota.ExecuteReader()) { row.Read(); if (row.GetInt64(0) >= 1000 || row.GetInt64(1) >= 100) status = "Rejected"; }
        }
        if (status == "Pending")
        {
            var occurred = Math.Min(Ms(input.OccurredAtUtc), Ms(input.ReceivedAtUtc));
            Execute(db, tx, "INSERT OR IGNORE INTO ChannelRoute VALUES(@key,@id,@channel,@endpoint,@sender,'es','BotOwned',1,@occurred,@now,NULL)",
                ("@key", routeKey), ("@id", S(Guid.NewGuid())), ("@channel", input.Channel), ("@endpoint", input.EndpointId), ("@sender", input.SenderId), ("@occurred", occurred), ("@now", Ms(input.ReceivedAtUtc)));
            Execute(db, tx, "UPDATE ChannelRoute SET LastInbound=MAX(LastInbound,@occurred),LastReceived=@now WHERE Key=@key", ("@key", routeKey), ("@occurred", occurred), ("@now", Ms(input.ReceivedAtUtc)));
            if (text is "/en" or "/es") Execute(db, tx, "UPDATE ChannelRoute SET Language=@language WHERE Key=@key AND Ownership='BotOwned'", ("@key", routeKey), ("@language", text[1..]));
            if (text == "/human") BeginHandoff(db, tx, routeKey);
            if (Route(db, tx, routeKey)!.Ownership != "BotOwned" && text != "/human") status = "HumanPending";
        }
        Execute(db, tx, "INSERT INTO ChannelInbox(Key,RouteKey,Hash,Text,Status,ReceivedAt) VALUES(@key,@route,@hash,@text,@status,@now)",
            ("@key", key), ("@route", status is "Ignored" or "Rejected" ? null : routeKey), ("@hash", hash), ("@text", status is "Ignored" or "Rejected" ? "" : text), ("@status", status), ("@now", Ms(input.ReceivedAtUtc)));
        return status;
    }
    public long ReadOffset(string botId)
    { using var db = Open(); using var cmd = Command(db, null, "SELECT NextOffset FROM ChannelPoll WHERE Endpoint=@bot", ("@bot", botId)); return cmd.ExecuteScalar() is long offset ? offset : 0; }
    public void AcceptTelegram(IReadOnlyList<ProviderUpdate> batch, EndpointOptions endpoint, DateTimeOffset now)
    {
        using var db = Open(); using var tx = db.BeginTransaction(deferred: false); long highest = -1;
        foreach (var update in batch)
        {
            highest = Math.Max(highest, long.Parse(update.EventId, CultureInfo.InvariantCulture));
            if (update.Text is not null) _ = Accept(db, tx, update.Text, endpoint);
            else Execute(db, tx, "INSERT OR IGNORE INTO ChannelInbox(Key,RouteKey,Hash,Text,Status,ReceivedAt) VALUES(@key,NULL,'IGNORED','', 'Ignored',@now)", ("@key", Key("telegram", endpoint.EndpointId, update.EventId)), ("@now", Ms(now)));
        }
        if (highest >= 0) Execute(db, tx, "INSERT INTO ChannelPoll VALUES(@bot,@offset) ON CONFLICT(Endpoint) DO UPDATE SET NextOffset=MAX(NextOffset,@offset)", ("@bot", endpoint.EndpointId), ("@offset", checked(highest + 1)));
        tx.Commit();
    }
    private static ChannelRoute? Route(SqliteConnection db, SqliteTransaction? tx, string key)
    {
        using var cmd = Command(db, tx, "SELECT Key,Id,Channel,Endpoint,Sender,Language,Ownership,Epoch,LastInbound FROM ChannelRoute WHERE Key=@key", ("@key", key)); using var row = cmd.ExecuteReader();
        return row.Read() ? new(row.GetString(0), Guid.Parse(row.GetString(1)), row.GetString(2), row.GetString(3), row.GetString(4), row.GetString(5), row.GetString(6), row.GetInt64(7), DateTimeOffset.FromUnixTimeMilliseconds(row.GetInt64(8))) : null;
    }
    private static void BeginHandoff(SqliteConnection db, SqliteTransaction tx, string routeKey)
    { Execute(db, tx, "UPDATE ChannelRoute SET Ownership='HandoffPending',Epoch=Epoch+1,RequestId=@request WHERE Key=@key AND Ownership='BotOwned'", ("@key", routeKey), ("@request", S(Guid.NewGuid()))); }
    public HandoffRequest RequestHandoff(string routeKey)
    {
        using var db = Open(); using var tx = db.BeginTransaction(deferred: false); BeginHandoff(db, tx, routeKey); var route = Route(db, tx, routeKey) ?? throw new RequestRejected(404, "CHANNEL_ROUTE_NOT_FOUND");
        using var cmd = Command(db, tx, "SELECT RequestId FROM ChannelRoute WHERE Key=@key", ("@key", routeKey)); var request = Guid.Parse((string)cmd.ExecuteScalar()!); tx.Commit();
        return new(request, route.Id, route.Epoch, route.Language, "DEMO_CHANNEL_REQUEST");
    }
    public void Acknowledge(HandoffAcknowledgment acknowledgment)
    {
        if (!acknowledgment.Accepted || acknowledgment.AssignedAgent is null) return;
        using var db = Open(); Execute(db, null, "UPDATE ChannelRoute SET Ownership='HumanOwned' WHERE Id=@id AND RequestId=@request AND Epoch=@epoch AND Ownership='HandoffPending'",
            ("@id", S(acknowledgment.ConversationId)), ("@request", S(acknowledgment.RequestId)), ("@epoch", acknowledgment.Epoch));
    }
    public ChannelJob? Claim(DateTimeOffset now)
    {
        using var db = Open(); using var tx = db.BeginTransaction(deferred: false);
        Execute(db, tx, "UPDATE ChannelInbox SET Status='Suppressed' WHERE Status IN ('Pending','Processing') AND Attempts>=2 AND (LeaseUntil IS NULL OR LeaseUntil<=@now)", ("@now", Ms(now)));
        using var cmd = Command(db, tx, "SELECT Key,RouteKey,Text FROM ChannelInbox WHERE Status IN ('Pending','Processing') AND (LeaseUntil IS NULL OR LeaseUntil<=@now) ORDER BY ReceivedAt,Key LIMIT 1", ("@now", Ms(now)));
        string key, routeKey, text;
        using (var row = cmd.ExecuteReader()) { if (!row.Read()) return null; key = row.GetString(0); routeKey = row.GetString(1); text = row.GetString(2); }
        var route = Route(db, tx, routeKey)!;
        if (route.Ownership != "BotOwned" && text != "/human") { Execute(db, tx, "UPDATE ChannelInbox SET Status='HumanPending' WHERE Key=@key", ("@key", key)); tx.Commit(); return null; }
        var lease = Guid.NewGuid(); Execute(db, tx, "UPDATE ChannelInbox SET Status='Processing',Lease=@lease,LeaseUntil=@until,Attempts=Attempts+1 WHERE Key=@key", ("@key", key), ("@lease", S(lease)), ("@until", Ms(now.AddSeconds(20)))); tx.Commit();
        return new(key, lease, route, text);
    }
    public void Complete(ChannelJob job, PreparedOutput output, DateTimeOffset now)
    {
        using var db = Open(); using var tx = db.BeginTransaction(deferred: false); var route = Route(db, tx, job.Route.Key)!;
        using var valid = Command(db, tx, "SELECT COUNT(*) FROM ChannelInbox WHERE Key=@key AND Lease=@lease AND Status='Processing' AND LeaseUntil>@now", ("@key", job.Key), ("@lease", S(job.Lease)), ("@now", Ms(now)));
        if ((long)valid.ExecuteScalar()! != 1) return;
        var isHandoff = output.Reply.ReasonCode == "HANDOFF_REQUESTED";
        if ((!isHandoff && (route.Ownership != "BotOwned" || route.Epoch != job.Route.Epoch)) || output.Reply.Text.EnumerateRunes().Count() > 3500)
        { Execute(db, tx, "UPDATE ChannelInbox SET Status='Suppressed',Lease=NULL,LeaseUntil=NULL WHERE Key=@key", ("@key", job.Key)); tx.Commit(); return; }
        Execute(db, tx, "INSERT OR IGNORE INTO ChannelOutbox(Id,InboundKey,RouteKey,Epoch,Output,Status,RetryAt) VALUES(@id,@inbound,@route,@epoch,@output,'Pending',@now)", ("@id", S(Guid.NewGuid())), ("@inbound", job.Key), ("@route", route.Key), ("@epoch", isHandoff ? route.Epoch : job.Route.Epoch), ("@output", JsonSerializer.Serialize(output)), ("@now", Ms(now)));
        Execute(db, tx, "UPDATE ChannelInbox SET Status='Completed',Lease=NULL,LeaseUntil=NULL WHERE Key=@key", ("@key", job.Key)); tx.Commit();
    }
    public ChannelOutput? NextOutput(DateTimeOffset now)
    {
        using var db = Open(); using var tx = db.BeginTransaction(deferred: false);
        Execute(db, tx, "UPDATE ChannelOutbox SET Status='Unknown' WHERE Status='Sending' AND StartedAt<=@cutoff", ("@cutoff", Ms(now.AddSeconds(-40))));
        using var cmd = Command(db, tx, "SELECT Id,InboundKey,RouteKey,Epoch,Output,Attempts FROM ChannelOutbox WHERE Status='Pending' AND RetryAt<=@now ORDER BY RetryAt,Id LIMIT 1", ("@now", Ms(now)));
        ChannelOutput? result = null;
        using (var row = cmd.ExecuteReader())
            if (row.Read()) result = new(Guid.Parse(row.GetString(0)), row.GetString(1), Route(db, tx, row.GetString(2))!, row.GetInt64(3), JsonSerializer.Deserialize<PreparedOutput>(row.GetString(4))!, row.GetInt32(5));
        tx.Commit(); return result;
    }
    public bool BeginSend(ChannelOutput output, DateTimeOffset now)
    {
        using var db = Open(); using var tx = db.BeginTransaction(deferred: false); var route = Route(db, tx, output.Route.Key)!;
        var allowed = route.Epoch == output.Epoch && (route.Ownership == "BotOwned" || output.Output.Reply.ReasonCode == "HANDOFF_REQUESTED") &&
            (route.Channel != "whatsapp" || now < route.LastInbound.AddHours(24));
        using var cmd = Command(db, tx, "UPDATE ChannelOutbox SET Status=@status,StartedAt=@now,Attempts=Attempts+1 WHERE Id=@id AND Status='Pending'", ("@status", allowed ? "Sending" : "Suppressed"), ("@now", Ms(now)), ("@id", S(output.Id)));
        var changed = cmd.ExecuteNonQuery(); tx.Commit(); return allowed && changed == 1;
    }
    public void FinishSend(Guid id, string status, string? providerId, DateTimeOffset retryAt)
    {
        if (status is not ("Sent" or "Unknown" or "Failed" or "Pending" or "Suppressed")) throw new ArgumentException("CHANNEL_STATE_REJECTED");
        using var db = Open(); Execute(db, null, "UPDATE ChannelOutbox SET Status=@status,ProviderId=@provider,RetryAt=@retry WHERE Id=@id AND Status IN ('Sending','Pending')", ("@id", S(id)), ("@status", status), ("@provider", providerId), ("@retry", Ms(retryAt)));
    }
    public void Cleanup(DateTimeOffset now)
    {
        using var db = Open(); using var tx = db.BeginTransaction(deferred: false);
        Execute(db, tx, "DELETE FROM ChannelOutbox WHERE InboundKey IN (SELECT Key FROM ChannelInbox WHERE ReceivedAt<@week) OR RouteKey IN (SELECT Key FROM ChannelRoute WHERE LastReceived<@week); UPDATE ChannelInbox SET Text='',RouteKey=NULL,Status='Tombstone',Lease=NULL,LeaseUntil=NULL WHERE ReceivedAt<@week; DELETE FROM ChannelRoute WHERE LastReceived<@week; DELETE FROM ChannelInbox WHERE ReceivedAt<@month",
            ("@week", Ms(now.AddDays(-7))), ("@month", Ms(now.AddDays(-30)))); tx.Commit();
        // The isolated channel knowledge store creates guest contexts, never product messages/accounts.
        using var schema = Command(db, null, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Conversation'");
        if ((long)schema.ExecuteScalar()! == 1)
        {
            const string expiredGuests = "SELECT Id FROM Conversation WHERE PrincipalId IS NULL AND CreatedAt<@week AND NOT EXISTS(SELECT 1 FROM Message m WHERE m.ConversationId=Conversation.Id)";
            using var guestTx = db.BeginTransaction(deferred: false);
            Execute(db, guestTx, "DELETE FROM ConversationControl WHERE ConversationId IN (" + expiredGuests + "); DELETE FROM Idempotency WHERE ConversationId IN (" + expiredGuests + "); DELETE FROM Conversation WHERE Id IN (" + expiredGuests + "); DELETE FROM UserSession WHERE PrincipalId IS NULL AND ExpiresAt<@week AND Id NOT IN (SELECT GuestSessionId FROM Conversation WHERE GuestSessionId IS NOT NULL)", ("@week", Ms(now.AddDays(-7))));
            guestTx.Commit();
        }
    }
    public void MetaStatuses(JsonElement root, string phoneId)
    {
        using var db = Open(); using var tx = db.BeginTransaction(deferred: false);
        foreach (var entry in root.GetProperty("entry").EnumerateArray())
        foreach (var change in entry.GetProperty("changes").EnumerateArray())
        {
            var value = change.GetProperty("value");
            if (change.GetProperty("field").GetString() != "messages" || value.GetProperty("metadata").GetProperty("phone_number_id").GetString() != phoneId || !value.TryGetProperty("statuses", out var statuses)) continue;
            if (statuses.GetArrayLength() > 100) throw new RequestRejected(400, "CHANNEL_BATCH_TOO_LARGE");
            foreach (var status in statuses.EnumerateArray())
            {
                var next = status.GetProperty("status").GetString(); var providerId = status.GetProperty("id").GetString(); var recipient = status.GetProperty("recipient_id").GetString();
                if (next is not ("sent" or "delivered" or "read" or "failed") || string.IsNullOrWhiteSpace(providerId) || providerId.Length > 200 || recipient is null) continue;
                var correlation = status.TryGetProperty("biz_opaque_callback_data", out var opaque) && Guid.TryParse(opaque.GetString(), out var parsed) ? S(parsed) : "";
                var state = next == "sent" ? "Sent" : next == "delivered" ? "Delivered" : next == "read" ? "Read" : "Failed";
                Execute(db, tx, """
                    UPDATE ChannelOutbox SET Status=@state,ProviderId=@provider
                    WHERE (ProviderId=@provider OR (Id=@id AND ProviderId IS NULL AND Status IN ('Sending','Unknown')))
                    AND RouteKey IN (SELECT Key FROM ChannelRoute WHERE Channel='whatsapp' AND Endpoint=@endpoint AND Sender=@recipient)
                    AND ((@state='Sent' AND Status IN ('Sending','Unknown'))
                      OR (@state='Delivered' AND Status IN ('Sending','Unknown','Sent'))
                      OR (@state='Read' AND Status IN ('Sending','Unknown','Sent','Delivered'))
                      OR (@state='Failed' AND Status IN ('Sending','Unknown','Sent')))
                    """, ("@state", state), ("@provider", providerId), ("@id", correlation), ("@endpoint", phoneId), ("@recipient", recipient));
            }
        }
        tx.Commit();
    }
    public object Status()
    {
        using var db = Open(); var summary = new Dictionary<string, Dictionary<string, long>>();
        foreach (var (name, table, column) in new[] { ("inbox", "ChannelInbox", "Status"), ("outbox", "ChannelOutbox", "Status"), ("ownership", "ChannelRoute", "Ownership") })
        {
            var counts = new Dictionary<string, long>(); using var cmd = Command(db, null, $"SELECT {column},COUNT(*) FROM {table} GROUP BY {column}"); using var row = cmd.ExecuteReader(); while (row.Read()) counts[row.GetString(0)] = row.GetInt64(1); summary[name] = counts;
        }
        return summary;
    }
}
