using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContactCenterAI.Application;
using ContactCenterAI.Domain;
using Microsoft.Data.Sqlite;
namespace ContactCenterAI.Simulator;

public sealed record CancelSourceCommand(Guid CommandId, string ReservationId, string ExpectedReservationVersion, string PolicyVersion);
public sealed record SourceCommandReceipt(Guid CommandId, string ReservationId, string Status, string CurrentVersion, string ReasonCode, string SourceReference);
public sealed class SourceRejected(int status, string code) : Exception(code)
{ public int Status { get; } = status; public string Code { get; } = code; }

public sealed class ReservationStore
{
    private readonly string connectionString;
    private readonly TimeProvider clock;
    public ReservationStore(string path, TimeProvider clock)
    {
        this.clock = clock; Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = Path.GetFullPath(path), ForeignKeys = true, DefaultTimeout = 5 }.ToString();
        using var db = Open(); using var schema = Command(db, null, """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS Reservation(Id TEXT PRIMARY KEY,MemberRef TEXT NOT NULL,PropertyName TEXT NOT NULL,CheckInUtc INTEGER NOT NULL,Status TEXT NOT NULL CHECK(Status IN ('Confirmed','Cancelled')),Version INTEGER NOT NULL CHECK(Version>0));
            CREATE TABLE IF NOT EXISTS Receipt(CommandId TEXT PRIMARY KEY,MemberRef TEXT NOT NULL,PayloadHash TEXT NOT NULL,ReservationId TEXT NOT NULL,ResultJson TEXT NOT NULL,OccurredAt INTEGER NOT NULL,FOREIGN KEY(ReservationId) REFERENCES Reservation(Id));
            PRAGMA user_version=1;
            """); schema.ExecuteNonQuery();
        using var tx = db.BeginTransaction(deferred: false);
        var now = clock.GetUtcNow();
        foreach (var (id, member, hours, status) in new[] { ("RES-001","MEM-001",96,"Confirmed"), ("RES-002","MEM-001",48,"Confirmed"), ("RES-003","MEM-002",120,"Confirmed"), ("RES-004","MEM-002",96,"Cancelled") })
        {
            using var seed = Command(db, tx, "INSERT OR IGNORE INTO Reservation VALUES(@id,@member,'Caribbean Horizon — Punta Cana',@date,@status,1)",
                ("@id", id), ("@member", member), ("@date", now.AddHours(hours)), ("@status", status)); seed.ExecuteNonQuery();
        }
        tx.Commit();
    }
    private SqliteConnection Open() { var db = new SqliteConnection(connectionString); db.Open(); return db; }
    private static SqliteCommand Command(SqliteConnection db, SqliteTransaction? tx, string sql, params (string Name, object? Value)[] parameters)
    {
        var command = db.CreateCommand(); command.Transaction = tx; command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value switch
        { Guid id => id.ToString(), DateTimeOffset time => time.ToUnixTimeMilliseconds(), null => DBNull.Value, _ => value });
        return command;
    }
    private static SourceReservation Row(SqliteDataReader row) => new(row.GetString(0), row.GetString(1), row.GetString(2),
        DateTimeOffset.FromUnixTimeMilliseconds(row.GetInt64(3)), Enum.Parse<ReservationStatus>(row.GetString(4)), row.GetInt64(5).ToString(CultureInfo.InvariantCulture));
    private static SourceReservation? Find(SqliteConnection db, SqliteTransaction? tx, string member, string id)
    {
        using var command = Command(db, tx, "SELECT * FROM Reservation WHERE Id=@id AND MemberRef=@member", ("@id", id), ("@member", member));
        using var reader = command.ExecuteReader(); return reader.Read() ? Row(reader) : null;
    }
    public IReadOnlyList<SourceReservation> List(string member)
    {
        using var db = Open(); using var command = Command(db, null, "SELECT * FROM Reservation WHERE MemberRef=@member ORDER BY Id LIMIT 20", ("@member", member));
        using var reader = command.ExecuteReader(); var items = new List<SourceReservation>();
        while (reader.Read()) items.Add(Row(reader)); return items;
    }
    public SourceReservation Get(string member, string id)
    { using var db = Open(); return Find(db, null, member, id) ?? throw new SourceRejected(404, "RESOURCE_NOT_FOUND"); }
    public SourceCommandReceipt? Receipt(string member, Guid id)
    {
        using var db = Open(); using var command = Command(db, null, "SELECT ResultJson FROM Receipt WHERE CommandId=@id AND MemberRef=@member", ("@id", id), ("@member", member));
        return command.ExecuteScalar() is string json ? JsonSerializer.Deserialize<SourceCommandReceipt>(json)! : null;
    }
    public SourceCommandReceipt Cancel(string member, CancelSourceCommand request)
    {
        if (request.CommandId == Guid.Empty || string.IsNullOrWhiteSpace(request.ReservationId) || request.ReservationId.Length > 40 ||
            string.IsNullOrWhiteSpace(request.ExpectedReservationVersion) || request.ExpectedReservationVersion.Length > 40 ||
            string.IsNullOrWhiteSpace(request.PolicyVersion) || request.PolicyVersion.Length > 40) throw new SourceRejected(400, "INVALID_REQUEST");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(member + "\n" + JsonSerializer.Serialize(request))));
        using var db = Open(); using var tx = db.BeginTransaction(deferred: false);
        var reservation = Find(db, tx, member, request.ReservationId) ?? throw new SourceRejected(404, "RESOURCE_NOT_FOUND");
        using (var replay = Command(db, tx, "SELECT MemberRef,PayloadHash,ResultJson FROM Receipt WHERE CommandId=@id", ("@id", request.CommandId)))
        using (var row = replay.ExecuteReader())
            if (row.Read())
            {
                if (row.GetString(0) != member || row.GetString(1) != hash) throw new SourceRejected(409, "IDEMPOTENCY_CONFLICT");
                var result = JsonSerializer.Deserialize<SourceCommandReceipt>(row.GetString(2))!; return result;
            }
        if (request.PolicyVersion != CancellationPolicy.Version) throw new SourceRejected(409, "POLICY_CONFLICT");
        if (request.ExpectedReservationVersion != reservation.Version) throw new SourceRejected(409, "VERSION_CONFLICT");
        var now = clock.GetUtcNow(); var rule = CancellationPolicy.Evaluate(reservation.Status, reservation.CheckInUtc, now);
        if (!rule.Eligible) throw new SourceRejected(422, rule.ReasonCode);
        using var update = Command(db, tx, "UPDATE Reservation SET Status='Cancelled',Version=Version+1 WHERE Id=@id AND MemberRef=@member AND Version=@version AND Status='Confirmed'", ("@id", request.ReservationId), ("@member", member), ("@version", long.Parse(reservation.Version, CultureInfo.InvariantCulture)));
        if (update.ExecuteNonQuery() != 1) throw new SourceRejected(409, "VERSION_CONFLICT");
        var current = Find(db, tx, member, request.ReservationId)!;
        var receipt = new SourceCommandReceipt(request.CommandId, current.ReservationId, "Completed", current.Version, "CANCELLED_FREE", "source:" + request.CommandId);
        using var insert = Command(db, tx, "INSERT INTO Receipt VALUES(@id,@member,@hash,@reservation,@json,@now)",
            ("@id", request.CommandId), ("@member", member), ("@hash", hash), ("@reservation", current.ReservationId), ("@json", JsonSerializer.Serialize(receipt)), ("@now", now));
        insert.ExecuteNonQuery(); tx.Commit(); return receipt;
    }
}
