using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContactCenterAI.Application;
using ContactCenterAI.Domain;
using Microsoft.Data.Sqlite;

namespace ContactCenterAI.Infrastructure;

// Isolated synthetic source and journal. Nothing mutates the historical v1 stores.
public sealed class ResortStore
{
    private readonly string connection;
    private readonly TimeProvider clock;
    public static readonly RoomCategory[] InitialCatalog = [
        new("standard", "Estándar", "Standard", 2, 12000, ["Wi-Fi", "Desayuno", "Aire acondicionado"], ["Wi-Fi", "Breakfast", "Air conditioning"]),
        new("deluxe", "Deluxe", "Deluxe", 2, 18000, ["Wi-Fi", "Desayuno", "Balcón", "Vista a piscina"], ["Wi-Fi", "Breakfast", "Balcony", "Pool view"]),
        new("suite", "Suite", "Suite", 4, 28000, ["Wi-Fi", "Desayuno", "Balcón", "Vista a piscina", "Jacuzzi privado", "Terraza"], ["Wi-Fi", "Breakfast", "Balcony", "Pool view", "Private jacuzzi", "Terrace"])
    ];
    public ResortStore(string path, TimeProvider clock)
    {
        this.clock = clock; Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        connection = new SqliteConnectionStringBuilder { DataSource = Path.GetFullPath(path), ForeignKeys = true, DefaultTimeout = 10, Pooling = false }.ToString();
        using var db = Open();
        Exec(db, null, """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS ResortMeta(Key TEXT PRIMARY KEY,Value TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS ResortCategory(Id TEXT PRIMARY KEY,Json TEXT NOT NULL,Version INTEGER NOT NULL CHECK(Version>0));
            CREATE TABLE IF NOT EXISTS ResortStay(Id TEXT PRIMARY KEY,Member TEXT NOT NULL,Type TEXT NOT NULL,Arrival TEXT NOT NULL,Departure TEXT NOT NULL,Guests INTEGER NOT NULL CHECK(Guests>0),Total INTEGER NOT NULL CHECK(Total>=0),Status TEXT NOT NULL CHECK(Status IN ('Confirmed','Cancelled')),Version INTEGER NOT NULL CHECK(Version>0));
            CREATE TABLE IF NOT EXISTS ResortBlock(Id TEXT PRIMARY KEY,Unit TEXT NOT NULL,Arrival TEXT NOT NULL,Departure TEXT NOT NULL,Version INTEGER NOT NULL CHECK(Version>0));
            CREATE TABLE IF NOT EXISTS ResortNight(Unit TEXT NOT NULL,Night TEXT NOT NULL,StayId TEXT REFERENCES ResortStay(Id),BlockId TEXT REFERENCES ResortBlock(Id),PRIMARY KEY(Unit,Night),CHECK((StayId IS NULL)!=(BlockId IS NULL)));
            CREATE TABLE IF NOT EXISTS ResortOffer(Id TEXT PRIMARY KEY,Session TEXT NOT NULL,Member TEXT NOT NULL,Route TEXT NOT NULL,Epoch INTEGER NOT NULL,RequestKey TEXT NOT NULL,PayloadHash TEXT NOT NULL,Json TEXT NOT NULL,StayVersion INTEGER NOT NULL,CatalogVersion INTEGER NOT NULL,Expires INTEGER NOT NULL,State TEXT NOT NULL CHECK(State IN ('Pending','Queued','Completed','Rejected','Invalidated')),UNIQUE(Session,Route,Epoch,RequestKey));
            CREATE TABLE IF NOT EXISTS ResortReceipt(CommandId TEXT PRIMARY KEY,Json TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS ResortLinkCode(Hash TEXT PRIMARY KEY,Session TEXT NOT NULL,Expires INTEGER NOT NULL,Route TEXT,Epoch INTEGER,Used INTEGER NOT NULL DEFAULT 0 CHECK(Used IN (0,1)));
            CREATE TABLE IF NOT EXISTS ResortLink(Route TEXT PRIMARY KEY,Session TEXT NOT NULL,Epoch INTEGER NOT NULL,Expires INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS ResortLinkAttempt(Route TEXT NOT NULL,Bucket INTEGER NOT NULL,Count INTEGER NOT NULL,PRIMARY KEY(Route,Bucket));
            CREATE TABLE IF NOT EXISTS ResortAudit(Id INTEGER PRIMARY KEY AUTOINCREMENT,At INTEGER NOT NULL,Code TEXT NOT NULL);
            """);
        using var tx = db.BeginTransaction(deferred: false);
        if (Scalar(db, tx, "SELECT Value FROM ResortMeta WHERE Key='seed'") is null)
        {
            Exec(db, tx, "INSERT INTO ResortMeta VALUES('seed',@date)", ("@date", Today().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
            foreach (var category in InitialCatalog) Exec(db, tx, "INSERT INTO ResortCategory VALUES(@id,@json,1)", ("@id", category.Id), ("@json", JsonSerializer.Serialize(category)));
            SeedStay(db, tx, "STAY-A0000001", "MEM-001", "standard", Today().AddDays(7), 2);
            SeedStay(db, tx, "STAY-B0000001", "MEM-002", "suite", Today().AddDays(10), 3);
            var start = Today().AddDays(4); var end = start.AddDays(2);
            Exec(db, tx, "INSERT INTO ResortBlock VALUES('seed-maintenance','deluxe',@a,@d,1)", ("@a", Iso(start)), ("@d", Iso(end)));
            foreach (var night in Nights(start, end)) Exec(db, tx, "INSERT INTO ResortNight VALUES('deluxe',@n,NULL,'seed-maintenance')", ("@n", Iso(night)));
        }
        tx.Commit();
    }
    private SqliteConnection Open() { var db = new SqliteConnection(connection); db.Open(); return db; }
    private static SqliteCommand Cmd(SqliteConnection db, SqliteTransaction? tx, string sql, params (string, object?)[] args)
    { var c = db.CreateCommand(); c.Transaction = tx; c.CommandText = sql; foreach (var (key, value) in args) c.Parameters.AddWithValue(key, value ?? DBNull.Value); return c; }
    private static int Exec(SqliteConnection db, SqliteTransaction? tx, string sql, params (string, object?)[] args) { using var c = Cmd(db, tx, sql, args); return c.ExecuteNonQuery(); }
    private static object? Scalar(SqliteConnection db, SqliteTransaction? tx, string sql, params (string, object?)[] args) { using var c = Cmd(db, tx, sql, args); var v = c.ExecuteScalar(); return v is DBNull ? null : v; }
    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/Santo_Domingo")).DateTime);
    private static DateOnly Date(string? value) => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : throw new RequestRejected(400, "FULL_DATES_REQUIRED");
    private static IEnumerable<DateOnly> Nights(DateOnly a, DateOnly d) { for (var n = a; n < d; n = n.AddDays(1)) yield return n; }
    private long Now => clock.GetUtcNow().ToUnixTimeMilliseconds();
    private static void Customer(Actor a, DateTimeOffset now) { if (a.TenantId != "tenant-demo" || a.PrincipalId is null || string.IsNullOrEmpty(a.MemberRef) || !a.Roles.HasFlag(ActorRoles.Customer) || a.ExpiresAt <= now) throw new RequestRejected(403, "VERIFIED_CUSTOMER_REQUIRED"); }
    private void Admin(Actor a) { if (a.TenantId != "tenant-demo" || a.PrincipalId is null || !a.Roles.HasFlag(ActorRoles.OperationsAdmin) || a.ExpiresAt <= clock.GetUtcNow()) throw new RequestRejected(403, "OPERATIONS_ADMIN_REQUIRED"); }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private void Audit(SqliteConnection db, SqliteTransaction tx, string code) => Exec(db, tx, "INSERT INTO ResortAudit(At,Code) VALUES(@at,@code)", ("@at", Now), ("@code", code));
    private static (RoomCategory Category, long Version) Category(SqliteConnection db, SqliteTransaction? tx, string? type)
    {
        using var cmd = Cmd(db, tx, "SELECT Json,Version FROM ResortCategory WHERE Id=@id", ("@id", type)); using var row = cmd.ExecuteReader();
        if (!row.Read()) throw new RequestRejected(400, "ROOM_TYPE_REQUIRED"); return (JsonSerializer.Deserialize<RoomCategory>(row.GetString(0))!, row.GetInt64(1));
    }
    private static ResortStay Row(SqliteDataReader row) => new(row.GetString(0), row.GetString(2), row.GetString(3), row.GetString(4), row.GetInt32(5), row.GetInt64(6), row.GetString(7), row.GetInt64(8));
    private static ResortStay Find(SqliteConnection db, SqliteTransaction? tx, string member, string? id)
    { using var cmd = Cmd(db, tx, "SELECT * FROM ResortStay WHERE Id=@id AND Member=@member", ("@id", id), ("@member", member)); using var row = cmd.ExecuteReader(); return row.Read() ? Row(row) : throw new RequestRejected(404, "RESOURCE_NOT_FOUND"); }
    private void SeedStay(SqliteConnection db, SqliteTransaction tx, string id, string member, string type, DateOnly a, int nights)
    {
        var c = Category(db, tx, type).Category;
        Exec(db, tx, "INSERT INTO ResortStay VALUES(@id,@member,@type,@a,@d,2,@total,'Confirmed',1)", ("@id", id), ("@member", member), ("@type", type), ("@a", Iso(a)), ("@d", Iso(a.AddDays(nights))), ("@total", c.NightlyCents * nights));
        foreach (var n in Nights(a, a.AddDays(nights))) Exec(db, tx, "INSERT INTO ResortNight VALUES(@type,@n,@id,NULL)", ("@type", type), ("@n", Iso(n)), ("@id", id));
    }
    public IReadOnlyList<RoomCategory> Catalog()
    { using var db = Open(); using var cmd = Cmd(db, null, "SELECT Json FROM ResortCategory ORDER BY CASE Id WHEN 'standard' THEN 0 WHEN 'deluxe' THEN 1 ELSE 2 END"); using var r = cmd.ExecuteReader(); var result = new List<RoomCategory>(); while (r.Read()) result.Add(JsonSerializer.Deserialize<RoomCategory>(r.GetString(0))!); return result; }
    public object Calendar(string from, int days)
    {
        var a = Date(from); if (days is < 1 or > 90 || a < Today() || a.AddDays(days) > Today().AddDays(90)) throw new RequestRejected(400, "CALENDAR_RANGE_INVALID");
        using var db = Open(); using var tx = db.BeginTransaction(deferred: true); var items = new List<object>();
        foreach (var type in Catalog()) foreach (var n in Nights(a, a.AddDays(days)))
        {
            var state = Scalar(db, tx, "SELECT CASE WHEN StayId IS NOT NULL THEN 'Occupied' ELSE 'Maintenance' END FROM ResortNight WHERE Unit=@u AND Night=@n", ("@u", type.Id), ("@n", Iso(n))) as string ?? "Available";
            items.Add(new { unit = type.Id, date = Iso(n), state });
        }
        tx.Commit();
        return new { property = "Caribbean Horizon", timezone = "America/Santo_Domingo", currency = "USD", fictional = true, observedAt = clock.GetUtcNow(), days = items };
    }
    public string[] Available(string type, int nights, int guests, string? from = null)
    {
        using var db = Open(); var c = Category(db, null, type).Category; var a = from is null ? Today().AddDays(1) : Date(from);
        if (nights is < 1 or > 14 || guests < 1 || guests > c.Capacity || a < Today() || a > Today().AddDays(89)) throw new RequestRejected(400, "STAY_INPUT_INVALID");
        return Enumerable.Range(0, Math.Max(0, Math.Min(30, Today().AddDays(90).DayNumber - a.DayNumber - nights + 1))).Select(i => a.AddDays(i))
            .Where(start => Free(db, null, type, start, start.AddDays(nights), null)).Take(5).Select(start => Iso(start) + " → " + Iso(start.AddDays(nights))).ToArray();
    }
    private static bool Free(SqliteConnection db, SqliteTransaction? tx, string type, DateOnly a, DateOnly d, string? own) =>
        Convert.ToInt64(Scalar(db, tx, "SELECT COUNT(*) FROM ResortNight WHERE Unit=@u AND Night>=@a AND Night<@d AND (StayId IS NULL OR StayId<>@own)", ("@u", type), ("@a", Iso(a)), ("@d", Iso(d)), ("@own", own ?? "")), CultureInfo.InvariantCulture) == 0;
    public ResortStay[] MyStays(Actor actor)
    { Customer(actor, clock.GetUtcNow()); using var db = Open(); using var cmd = Cmd(db, null, "SELECT * FROM ResortStay WHERE Member=@m ORDER BY Arrival LIMIT 30", ("@m", actor.MemberRef)); using var row = cmd.ExecuteReader(); var list = new List<ResortStay>(); while (row.Read()) list.Add(Row(row)); return list.ToArray(); }
    private void Cutoff(ResortStay stay)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Santo_Domingo"); var local = Date(stay.Arrival).ToDateTime(new TimeOnly(15, 0), DateTimeKind.Unspecified);
        var start = new DateTimeOffset(local, zone.GetUtcOffset(local));
        if (stay.Status != "Confirmed") throw new RequestRejected(422, "STAY_NOT_CONFIRMED");
        if (start - clock.GetUtcNow() < TimeSpan.FromHours(72)) throw new RequestRejected(422, "OUTSIDE_FREE_WINDOW");
    }
    private (ResortRequest Request, long Total, long Previous, long StayVersion, long CatalogVersion) Validate(SqliteConnection db, SqliteTransaction tx, Actor actor, ResortRequest request)
    {
        if (request.Action is not ("create" or "change" or "cancel")) throw new RequestRejected(400, "ACTION_INVALID");
        ResortStay? old = null; if (request.Action != "create") { old = Find(db, tx, actor.MemberRef!, request.StayId); Cutoff(old); }
        if (request.Action == "cancel") return (request with { Type = old!.Type, Arrival = old.Arrival, Departure = old.Departure, Guests = old.Guests }, 0, old.TotalCents, old.Version, Category(db, tx, old.Type).Version);
        var type = request.Type ?? old?.Type; var (category, version) = Category(db, tx, type); var a = Date(request.Arrival); var d = Date(request.Departure); var nights = d.DayNumber - a.DayNumber;
        if (nights is < 1 or > 14 || a < Today() || d > Today().AddDays(90) || request.Guests < 1 || request.Guests > category.Capacity) throw new RequestRejected(400, "STAY_INPUT_INVALID");
        if (!Free(db, tx, type!, a, d, old?.Id)) throw new RequestRejected(409, "DATES_OCCUPIED");
        return (request with { Type = type }, checked(category.NightlyCents * nights), old?.TotalCents ?? 0, old?.Version ?? 0, version);
    }
    public ResortOffer Offer(Actor actor, ResortRequest request, string key, string route = "web", long epoch = 1)
    {
        Customer(actor, clock.GetUtcNow()); if (key.Length is < 1 or > 128 || route.Length is < 1 or > 128 || epoch < 1) throw new RequestRejected(400, "REQUEST_KEY_REQUIRED");
        var hash = Hash(JsonSerializer.Serialize(request)); using var db = Open(); using var tx = db.BeginTransaction(deferred: false);
        using (var cmd = Cmd(db, tx, "SELECT PayloadHash,Json FROM ResortOffer WHERE Session=@s AND Route=@r AND Epoch=@e AND RequestKey=@k", ("@s", actor.SessionId.ToString()), ("@r", route), ("@e", epoch), ("@k", key)))
        using (var row = cmd.ExecuteReader()) if (row.Read()) { if (row.GetString(0) != hash) throw new RequestRejected(409, "IDEMPOTENCY_CONFLICT"); return JsonSerializer.Deserialize<ResortOffer>(row.GetString(1))!; }
        var v = Validate(db, tx, actor, request); var expires = clock.GetUtcNow().AddMinutes(5); if (expires > actor.ExpiresAt) expires = actor.ExpiresAt;
        var offer = new ResortOffer(Guid.NewGuid(), v.Request, v.Previous, v.Total, "USD", expires, "Pending");
        Exec(db, tx, "INSERT INTO ResortOffer VALUES(@id,@s,@m,@r,@e,@k,@hash,@json,@sv,@cv,@expires,'Pending')", ("@id", offer.Id.ToString()), ("@s", actor.SessionId.ToString()), ("@m", actor.MemberRef), ("@r", route), ("@e", epoch), ("@k", key), ("@hash", hash), ("@json", JsonSerializer.Serialize(offer)), ("@sv", v.StayVersion), ("@cv", v.CatalogVersion), ("@expires", expires.ToUnixTimeMilliseconds()));
        Audit(db, tx, "OFFER_CREATED"); tx.Commit(); return offer;
    }
    public ResortReceipt Confirm(Actor actor, Guid id, string route = "web", long epoch = 1)
    {
        Customer(actor, clock.GetUtcNow()); using var db = Open(); using (var tx = db.BeginTransaction(deferred: false))
        {
            using var cmd = Cmd(db, tx, "SELECT State,Expires FROM ResortOffer WHERE Id=@id AND Session=@s AND Member=@m AND Route=@r AND Epoch=@e", ("@id", id.ToString()), ("@s", actor.SessionId.ToString()), ("@m", actor.MemberRef), ("@r", route), ("@e", epoch)); using var row = cmd.ExecuteReader();
            if (!row.Read()) throw new RequestRejected(404, "RESOURCE_NOT_FOUND"); var state = row.GetString(0); var expires = row.GetInt64(1); row.Close();
            if (route != "web" && Scalar(db, tx, "SELECT Session FROM ResortLink WHERE Route=@r AND Epoch=@e AND Expires>@now", ("@r", route), ("@e", epoch), ("@now", Now)) as string != actor.SessionId.ToString()) throw new RequestRejected(403, "OFFER_AUTHORITY_CHANGED");
            if (state == "Invalidated") throw new RequestRejected(403, "OFFER_AUTHORITY_CHANGED");
            if (state == "Pending" && expires <= Now) throw new RequestRejected(409, "OFFER_EXPIRED");
            Exec(db, tx, "UPDATE ResortOffer SET State='Queued' WHERE Id=@id AND State='Pending'", ("@id", id.ToString())); tx.Commit();
        }
        return Execute(actor, id);
    }
    private ResortReceipt Execute(Actor actor, Guid id)
    {
        Customer(actor, clock.GetUtcNow());
        using var db = Open(); using var tx = db.BeginTransaction(deferred: false);
        if (Scalar(db, tx, "SELECT Json FROM ResortReceipt WHERE CommandId=@id", ("@id", id.ToString())) is string receiptJson) return JsonSerializer.Deserialize<ResortReceipt>(receiptJson)!;
        using var cmd = Cmd(db, tx, "SELECT Json,StayVersion,CatalogVersion,State FROM ResortOffer WHERE Id=@id AND Session=@s AND Member=@m", ("@id", id.ToString()), ("@s", actor.SessionId.ToString()), ("@m", actor.MemberRef)); using var row = cmd.ExecuteReader();
        if (!row.Read()) throw new RequestRejected(404, "RESOURCE_NOT_FOUND"); var offer = JsonSerializer.Deserialize<ResortOffer>(row.GetString(0))!; var sv = row.GetInt64(1); var cv = row.GetInt64(2); var state = row.GetString(3); row.Close();
        if (state != "Queued") throw new RequestRejected(409, "COMMAND_NOT_QUEUED");
        ResortReceipt receipt;
        // Validation runs before any source mutation. Storage failures roll back everything.
        Exec(db, tx, "SAVEPOINT source_command");
        try
        {
            var v = Validate(db, tx, actor, offer.Request);
            if (v.StayVersion != sv) throw new RequestRejected(409, "VERSION_CONFLICT");
            if (v.CatalogVersion != cv || v.Total != offer.TotalCents) throw new RequestRejected(409, "PRICE_CHANGED");
            var r = v.Request; var stayId = r.Action == "create" ? "STAY-" + id.ToString("N")[..8].ToUpperInvariant() : r.StayId!;
            if (r.Action == "create") Exec(db, tx, "INSERT INTO ResortStay VALUES(@id,@m,@type,@a,@d,@g,@total,'Confirmed',1)", ("@id", stayId), ("@m", actor.MemberRef), ("@type", r.Type), ("@a", r.Arrival), ("@d", r.Departure), ("@g", r.Guests), ("@total", v.Total));
            else
            {
                Exec(db, tx, "DELETE FROM ResortNight WHERE StayId=@id", ("@id", stayId));
                if (r.Action == "cancel") Exec(db, tx, "UPDATE ResortStay SET Status='Cancelled',Version=Version+1 WHERE Id=@id", ("@id", stayId));
                else Exec(db, tx, "UPDATE ResortStay SET Type=@type,Arrival=@a,Departure=@d,Guests=@g,Total=@t,Version=Version+1 WHERE Id=@id", ("@type", r.Type), ("@a", r.Arrival), ("@d", r.Departure), ("@g", r.Guests), ("@t", v.Total), ("@id", stayId));
            }
            if (r.Action != "cancel") foreach (var night in Nights(Date(r.Arrival), Date(r.Departure))) Exec(db, tx, "INSERT INTO ResortNight VALUES(@u,@n,@s,NULL)", ("@u", r.Type), ("@n", Iso(night)), ("@s", stayId));
            receipt = new(id, "Completed", r.Action.ToUpperInvariant() + "_COMPLETED", Find(db, tx, actor.MemberRef!, stayId));
        }
        catch (RequestRejected rejected) { Exec(db, tx, "ROLLBACK TO source_command"); receipt = new(id, "Rejected", rejected.Code, null); }
        Exec(db, tx, "RELEASE source_command");
        Exec(db, tx, "INSERT INTO ResortReceipt VALUES(@id,@json); UPDATE ResortOffer SET State=@state WHERE Id=@id", ("@id", id.ToString()), ("@json", JsonSerializer.Serialize(receipt)), ("@state", receipt.Status)); Audit(db, tx, receipt.ReasonCode); tx.Commit(); return receipt;
    }
    public ResortReceipt? Receipt(Actor actor, Guid id)
    {
        Customer(actor, clock.GetUtcNow()); using var db = Open();
        if (Scalar(db, null, "SELECT Id FROM ResortOffer WHERE Id=@id AND Session=@s AND Member=@m", ("@id", id.ToString()), ("@s", actor.SessionId.ToString()), ("@m", actor.MemberRef)) is null) throw new RequestRejected(404, "RESOURCE_NOT_FOUND");
        return Scalar(db, null, "SELECT Json FROM ResortReceipt WHERE CommandId=@id", ("@id", id.ToString())) is string json ? JsonSerializer.Deserialize<ResortReceipt>(json) : null;
    }
    public object Block(Actor actor, ResortBlockRequest request)
    {
        Admin(actor); using var db = Open(); using var tx = db.BeginTransaction(deferred: false); _ = Category(db, tx, request.Unit); var a = Date(request.Arrival); var d = Date(request.Departure);
        if (a < Today() || d <= a || d > Today().AddDays(90)) throw new RequestRejected(400, "CALENDAR_RANGE_INVALID");
        if (!Free(db, tx, request.Unit, a, d, null)) throw new RequestRejected(409, "DATES_OCCUPIED"); var id = Guid.NewGuid().ToString();
        Exec(db, tx, "INSERT INTO ResortBlock VALUES(@id,@u,@a,@d,1)", ("@id", id), ("@u", request.Unit), ("@a", Iso(a)), ("@d", Iso(d)));
        foreach (var n in Nights(a, d)) Exec(db, tx, "INSERT INTO ResortNight VALUES(@u,@n,NULL,@id)", ("@u", request.Unit), ("@n", Iso(n)), ("@id", id)); Audit(db, tx, "MAINTENANCE_CREATED"); tx.Commit(); return new { id, version = 1, request.Unit, request.Arrival, request.Departure };
    }
    public object[] Blocks(Actor actor)
    { Admin(actor); using var db = Open(); using var cmd = Cmd(db, null, "SELECT Id,Unit,Arrival,Departure,Version FROM ResortBlock ORDER BY Arrival"); using var r = cmd.ExecuteReader(); var result = new List<object>(); while (r.Read()) result.Add(new { id = r.GetString(0), unit = r.GetString(1), arrival = r.GetString(2), departure = r.GetString(3), version = r.GetInt64(4) }); return result.ToArray(); }
    public void RemoveBlock(Actor actor, string id, long version)
    { Admin(actor); using var db = Open(); using var tx = db.BeginTransaction(deferred: false); if (Scalar(db, tx, "SELECT Id FROM ResortBlock WHERE Id=@id AND Version=@v", ("@id", id), ("@v", version)) is null) throw new RequestRejected(409, "VERSION_CONFLICT"); Exec(db, tx, "DELETE FROM ResortNight WHERE BlockId=@id; DELETE FROM ResortBlock WHERE Id=@id", ("@id", id)); Audit(db, tx, "MAINTENANCE_REMOVED"); tx.Commit(); }
    public void UpdateRate(Actor actor, string type, long cents)
    { Admin(actor); if (cents is < 100 or > 1000000) throw new RequestRejected(400, "RATE_INVALID"); using var db = Open(); using var tx = db.BeginTransaction(deferred: false); var c = Category(db, tx, type).Category with { NightlyCents = cents }; Exec(db, tx, "UPDATE ResortCategory SET Json=@j,Version=Version+1 WHERE Id=@id", ("@j", JsonSerializer.Serialize(c)), ("@id", type)); Audit(db, tx, "RATE_CHANGED"); tx.Commit(); }
    public string CreateLinkCode(Actor actor)
    {
        Customer(actor, clock.GetUtcNow()); var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)); using var db = Open(); using var tx = db.BeginTransaction(deferred: false);
        Exec(db, tx, "DELETE FROM ResortLinkCode WHERE Session=@s OR Expires<=@now", ("@s", actor.SessionId.ToString()), ("@now", Now));
        Exec(db, tx, "INSERT INTO ResortLinkCode(Hash,Session,Expires) VALUES(@h,@s,@e)", ("@h", Hash(code)), ("@s", actor.SessionId.ToString()), ("@e", Math.Min(actor.ExpiresAt.ToUnixTimeMilliseconds(), Now + 300000))); tx.Commit(); return code;
    }
    public void SubmitLinkCode(string code, string route, long epoch)
    {
        if (code.Length != 32 || !code.All(Uri.IsHexDigit) || route.Length is < 1 or > 128 || epoch < 1) throw new RequestRejected(400, "LINK_CODE_INVALID");
        using var db = Open(); using var tx = db.BeginTransaction(deferred: false); var bucket = Now / 300000;
        Exec(db, tx, "DELETE FROM ResortLinkAttempt WHERE Bucket<@old; INSERT INTO ResortLinkAttempt VALUES(@r,@b,1) ON CONFLICT(Route,Bucket) DO UPDATE SET Count=Count+1", ("@old", bucket - 1), ("@r", route), ("@b", bucket));
        var attempts = Convert.ToInt64(Scalar(db, tx, "SELECT Count FROM ResortLinkAttempt WHERE Route=@r AND Bucket=@b", ("@r", route), ("@b", bucket)), CultureInfo.InvariantCulture);
        if (attempts > 5) { tx.Commit(); throw new RequestRejected(429, "LINK_RATE_LIMITED"); }
        var changed = Exec(db, tx, "UPDATE ResortLinkCode SET Used=1,Route=@r,Epoch=@e WHERE Hash=@h AND Used=0 AND Expires>@now", ("@r", route), ("@e", epoch), ("@h", Hash(code.ToUpperInvariant())), ("@now", Now));
        Audit(db, tx, changed == 1 ? "LINK_PENDING" : "LINK_CODE_REJECTED"); tx.Commit(); if (changed != 1) throw new RequestRejected(403, "LINK_CODE_INVALID");
    }
    public object LinkStatus(Actor actor)
    {
        Customer(actor, clock.GetUtcNow()); using var db = Open();
        using var cmd = Cmd(db, null, "SELECT Route FROM ResortLinkCode WHERE Session=@s AND Used=1 AND Expires>@now", ("@s", actor.SessionId.ToString()), ("@now", Now)); var pending = cmd.ExecuteScalar() as string;
        var active = Scalar(db, null, "SELECT Route FROM ResortLink WHERE Session=@s AND Expires>@now", ("@s", actor.SessionId.ToString()), ("@now", Now)) as string;
        return new { pending = pending is not null, active = active is not null, route = pending is null ? null : "WhatsApp …" + pending[^Math.Min(6, pending.Length)..], expiresAt = actor.ExpiresAt };
    }
    public void ConfirmLink(Actor actor)
    {
        Customer(actor, clock.GetUtcNow()); using var db = Open(); using var tx = db.BeginTransaction(deferred: false);
        using var cmd = Cmd(db, tx, "SELECT Hash,Route,Epoch FROM ResortLinkCode WHERE Session=@s AND Used=1 AND Expires>@now", ("@s", actor.SessionId.ToString()), ("@now", Now)); using var row = cmd.ExecuteReader();
        if (!row.Read()) throw new RequestRejected(403, "LINK_CONFIRMATION_REQUIRED"); var hash = row.GetString(0); var route = row.GetString(1); var epoch = row.GetInt64(2); row.Close();
        if (Scalar(db, tx, "SELECT Session FROM ResortLink WHERE Route=@r", ("@r", route)) is string previous) Invalidate(db, tx, previous);
        Invalidate(db, tx, actor.SessionId.ToString());
        Exec(db, tx, "INSERT INTO ResortLink VALUES(@r,@s,@e,@expires) ON CONFLICT(Route) DO UPDATE SET Session=excluded.Session,Epoch=excluded.Epoch,Expires=excluded.Expires", ("@r", route), ("@s", actor.SessionId.ToString()), ("@e", epoch), ("@expires", Math.Min(actor.ExpiresAt.ToUnixTimeMilliseconds(), Now + 1800000)));
        Exec(db, tx, "DELETE FROM ResortLinkCode WHERE Hash=@h", ("@h", hash)); Audit(db, tx, "LINK_ACTIVATED"); tx.Commit();
    }
    private static void Invalidate(SqliteConnection db, SqliteTransaction tx, string session)
    { Exec(db, tx, "DELETE FROM ResortLink WHERE Session=@s; UPDATE ResortOffer SET State='Invalidated' WHERE Session=@s AND State='Pending'", ("@s", session)); }
    public void Unlink(Actor actor) { using var db = Open(); using var tx = db.BeginTransaction(deferred: false); Invalidate(db, tx, actor.SessionId.ToString()); Exec(db, tx, "DELETE FROM ResortLinkCode WHERE Session=@s", ("@s", actor.SessionId.ToString())); Audit(db, tx, "LINK_REVOKED"); tx.Commit(); }
    public Guid? LinkedSession(string route, long epoch)
    {
        using var db = Open(); var id = Scalar(db, null, "SELECT Session FROM ResortLink WHERE Route=@r AND Epoch=@e AND Expires>@now", ("@r", route), ("@e", epoch), ("@now", Now)) as string;
        return Guid.TryParse(id, out var session) ? session : null;
    }
    public Guid[] QueuedSessions()
    { using var db = Open(); using var cmd = Cmd(db, null, "SELECT DISTINCT Session FROM ResortOffer WHERE State='Queued' LIMIT 100"); using var row = cmd.ExecuteReader(); var ids = new List<Guid>(); while (row.Read()) ids.Add(Guid.Parse(row.GetString(0))); return ids.ToArray(); }
    public void RejectRevokedQueue(Guid session)
    {
        using var db = Open(); using var tx = db.BeginTransaction(deferred: false);
        using var cmd = Cmd(db, tx, "SELECT Id FROM ResortOffer WHERE Session=@s AND State='Queued'", ("@s", session.ToString())); using var row = cmd.ExecuteReader();
        var ids = new List<Guid>(); while (row.Read()) ids.Add(Guid.Parse(row.GetString(0))); row.Close();
        foreach (var id in ids) { var receipt = new ResortReceipt(id, "Rejected", "SESSION_REVOKED", null); Exec(db, tx, "INSERT OR IGNORE INTO ResortReceipt VALUES(@id,@j); UPDATE ResortOffer SET State='Rejected' WHERE Id=@id", ("@id", id.ToString()), ("@j", JsonSerializer.Serialize(receipt))); }
        if (ids.Count > 0) Audit(db, tx, "SESSION_REVOKED"); tx.Commit();
    }
    public void Recover(Actor actor)
    {
        Customer(actor, clock.GetUtcNow()); using var db = Open(); using var cmd = Cmd(db, null, "SELECT Id FROM ResortOffer WHERE Session=@s AND State='Queued' LIMIT 100", ("@s", actor.SessionId.ToString())); using var row = cmd.ExecuteReader(); var ids = new List<Guid>(); while (row.Read()) ids.Add(Guid.Parse(row.GetString(0))); row.Close(); foreach (var id in ids) _ = Execute(actor, id);
    }
}
