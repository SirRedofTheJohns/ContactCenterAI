using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ContactCenterAI.Domain;
using ContactCenterAI.Simulator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;

try
{
    if (args.Length != 1) throw new ArgumentException("Expected repository root.");
    var root = Path.GetFullPath(args[0]); var checks = new List<string>();
    void Check(bool passed, string label)
    { if (!passed) throw new InvalidOperationException(label); checks.Add(label); Console.WriteLine("PASS: " + label); }
    void Denied(Action call, int status, string label)
    { try { call(); } catch (SourceRejected error) { Check(error.Status == status, label); return; } throw new InvalidOperationException(label); }
    var now = new DateTimeOffset(2026,10,1,12,0,0,TimeSpan.Zero);
    Check(CancellationPolicy.Evaluate(ReservationStatus.Confirmed, now.AddHours(72), now).Eligible, "Exactly 72 hours eligible");
    Check(!CancellationPolicy.Evaluate(ReservationStatus.Confirmed, now.AddHours(72).AddSeconds(-1), now).Eligible, "One second below 72 hours denied");
    Check(!CancellationPolicy.Evaluate(ReservationStatus.Cancelled, now.AddDays(5), now).Eligible, "Non-confirmed status denied");
    Check(CancellationPolicy.Evaluate(ReservationStatus.Confirmed, now.AddHours(72).ToOffset(TimeSpan.FromHours(-4)), now).Eligible, "Offsets compare instants without changing boundary");
    var clock = new SourceClock(now);
    var path = Path.Combine(root,".local","tests","source-"+Guid.NewGuid().ToString("N"),"reservations.db");
    var store = new ReservationStore(path, clock);
    Check(store.List("MEM-001").Select(item=>item.ReservationId).SequenceEqual(new[]{"RES-001","RES-002"}) && store.List("MEM-002").Count==2, "Source queries return only server member reservations");
    Denied(()=>store.Get("MEM-002","RES-001"),404,"Foreign and missing reservations denied by source ownership");
    Denied(()=>store.Cancel("MEM-002",new(Guid.NewGuid(),"RES-001","1",CancellationPolicy.Version)),404,"Source mutation independently checks owner");
    Denied(()=>store.Cancel("MEM-001",new(Guid.NewGuid(),"RES-002","1",CancellationPolicy.Version)),422,"Source rejects outside-window mutation");
    Denied(()=>store.Cancel("MEM-001",new(Guid.NewGuid(),"RES-001","9",CancellationPolicy.Version)),409,"Stale source version denied");
    Denied(()=>store.Cancel("MEM-001",new(Guid.NewGuid(),"RES-001","1","CP-001:v2")),409,"Changed policy version denied");
    var command = new CancelSourceCommand(Guid.NewGuid(),"RES-001","1",CancellationPolicy.Version);
    var first = store.Cancel("MEM-001",command);
    Check(Enumerable.Range(0,100).All(_=>store.Cancel("MEM-001",command)==first) && store.Get("MEM-001","RES-001").Version=="2", "One hundred identical replays preserve receipt and one source transition");
    Denied(()=>store.Cancel("MEM-001",command with { ExpectedReservationVersion="2" }),409,"Same command ID with changed payload conflicts");
    var concurrent = await Task.WhenAll(Enumerable.Range(0,12).Select(_=>Task.Run(()=>
    { try { store.Cancel("MEM-002",new(Guid.NewGuid(),"RES-003","1",CancellationPolicy.Version)); return 200; } catch(SourceRejected failure) { return failure.Status; } })));
    Check(concurrent.Count(code=>code==200)==1 && concurrent.Count(code=>code==409)==11 && store.Get("MEM-002","RES-003").Version=="2", "Twelve concurrent distinct commands make one source transition");
    var reopened = new ReservationStore(path, clock);
    Check(reopened.Receipt("MEM-001",command.CommandId)==first && reopened.Get("MEM-001","RES-001").Status==ReservationStatus.Cancelled, "Reopened source recovers reservation and authoritative receipt");
    Check(reopened.Receipt("MEM-002",command.CommandId) is null && reopened.Receipt("MEM-001",Guid.NewGuid()) is null, "Foreign or never accepted receipt returns no information");
    using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=path }.ToString()))
    {
        db.Open(); using var query=db.CreateCommand(); query.CommandText="SELECT COUNT(*) FROM Receipt";
        Check((long)query.ExecuteScalar()! == 2,"Reservation updates and exactly two receipts committed atomically");
    }
    var timePath=Path.Combine(Path.GetDirectoryName(path)!,"clock.db"); var timeStore=new ReservationStore(timePath,clock);
    clock.Now=now.AddHours(25);
    Denied(()=>timeStore.Cancel("MEM-001",new(Guid.NewGuid(),"RES-001","1",CancellationPolicy.Version)),422,"Execution uses current source clock after window changes");

    var builder=WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0");
    builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> { ["CCAI_SOURCE_SERVICE_KEY"]="synthetic-test-service-key" });
    await using var app=SourceApi.Build(builder,store); await app.StartAsync();
    var address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    using var client=new HttpClient { BaseAddress=new Uri(address) }; client.DefaultRequestHeaders.Host="127.0.0.1:7453";
    using var noService=await client.GetAsync("/source/reservations"); Check(noService.StatusCode==HttpStatusCode.Unauthorized,"Actual HTTP source denies missing service credential");
    client.DefaultRequestHeaders.Add("X-Service-Key","synthetic-test-service-key");
    using var noMember=await client.GetAsync("/source/reservations"); Check(noMember.StatusCode==HttpStatusCode.Forbidden,"Actual HTTP source requires server member context");
    client.DefaultRequestHeaders.Add("X-Member-Ref","MEM-002");
    using var foreign=await client.GetAsync("/source/reservations/RES-001"); Check(foreign.StatusCode==HttpStatusCode.NotFound,"Actual authenticated HTTP denies foreign reservation");
    using var unknown=await client.PostAsJsonAsync("/source/commands/cancel",new { commandId=Guid.NewGuid(),reservationId="RES-003",expectedReservationVersion="2",policyVersion=CancellationPolicy.Version,eligible=true });
    Check(unknown.StatusCode==HttpStatusCode.BadRequest,"Closed command DTO rejects caller eligibility injection");
    client.DefaultRequestHeaders.Host="remote.invalid";
    using var remote=await client.GetAsync("/health/live"); Check(remote.StatusCode==HttpStatusCode.Forbidden,"Source Host restriction stays enabled in HTTP tests");
    await app.StopAsync();
    File.WriteAllText(Path.Combine(root,"docs/progress/b05-source-evidence.json"),JsonSerializer.Serialize(new
    { result="PASS",observedAtUtc=DateTimeOffset.UtcNow,checksPassed=checks.Count,checks,
      scope="Pure CP-001, real C# SQLite source transactions/concurrency/receipt persistence and actual Kestrel source HTTP with isolated synthetic DB",
      productCancellationEnabled=false,sqlServerSourceTested=false,middlewareRecoveryTested=false },new JsonSerializerOptions { WriteIndented=true })+"\n");
    Console.WriteLine($"Source checks: {checks.Count} passed."); return 0;
}
catch (Exception failure) { Console.Error.WriteLine("Source checks failed: "+failure.GetType().Name+" "+failure.Message); return 1; }
sealed class SourceClock(DateTimeOffset now) : TimeProvider
{ public DateTimeOffset Now { get; set; }=now; public override DateTimeOffset GetUtcNow()=>Now; }
