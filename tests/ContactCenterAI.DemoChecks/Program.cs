using System.Text.Json;
using ContactCenterAI.Application;
using ContactCenterAI.Domain;
using ContactCenterAI.Infrastructure;
using Microsoft.Data.Sqlite;

try
{
    if (args.Length != 1) throw new ArgumentException("Expected repository root.");
    var root = Path.GetFullPath(args[0]);
    var path = Path.Combine(root, ".local", "tests", "demo-" + Guid.NewGuid().ToString("N"), "operations.db");
    var checks = new List<string>();
    void Check(bool value, string label)
    { if (!value) throw new InvalidOperationException("FAIL: " + label); checks.Add(label); Console.WriteLine("PASS: " + label); }
    async Task Denied(Func<Task> call, int expected, string label)
    {
        try { await call(); }
        catch (RequestRejected failure) { Check(failure.Status == expected, label); return; }
        throw new InvalidOperationException("FAIL: " + label);
    }
    long Count(string table)
    {
        using var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString()); db.Open();
        using var command = db.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM " + table;
        return (long)command.ExecuteScalar()!;
    }
    var store = new SqliteOperationalStore(path); var now = DateTimeOffset.UtcNow; var ct = CancellationToken.None;
    const string issuer = "http://localhost:8080/realms/contactcenterai-local";
    const string subjectA = "10000000-0000-0000-0000-000000000001", subjectB = "10000000-0000-0000-0000-000000000002";
    Check((await store.CheckAsync(ct)).IsReady, "SQLite native adapter and schema start without Windows certificates");
    var guest = await store.CreateGuestAsync(now, ct);
    var guestConversation = await store.CreateConversationAsync(guest, "es", ConversationIngress.Hash("guest"), ConversationIngress.Hash("es"), now, ct);
    var a = await store.LoginAsync(issuer, subjectA, ActorRoles.Customer, guest.SessionId, now, ct);
    Check(a.SessionId != guest.SessionId && a.MemberRef == "MEM-001", "Verified subject maps server member and rotates opaque session");
    Check(await store.FindSessionAsync(guest.SessionId, now, ct) is null, "Guest session revoked after login");
    var adopted = await store.ReadConversationAsync(a, guestConversation.ConversationId, now, ct);
    Check(adopted is not null && adopted.Resource.PrincipalId == a.PrincipalId && adopted.Resource.GuestSessionId is null && adopted.Resource.Version == 2 && adopted.Resource.Epoch == 2, "Guest conversation adoption increments version and ownership epoch atomically");
    var b = await store.LoginAsync(issuer, subjectB, ActorRoles.Customer, null, now, ct);
    Check(await store.ReadConversationAsync(b, guestConversation.ConversationId, now, ct) is null, "Persistent store prevents customer B reading customer A conversation");
    await Denied(() => store.LoginAsync(issuer, subjectA, ActorRoles.Agent, null, now, ct), 403, "Claim role inconsistent with server binding denied");
    await Denied(() => store.LoginAsync("http://untrusted.invalid", subjectA, ActorRoles.Customer, null, now, ct), 403, "Untrusted issuer cannot bind to member");
    var receipts = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() => store.CreateConversationAsync(a, "en", ConversationIngress.Hash("parallel"), ConversationIngress.Hash("en"), now, ct))));
    Check(receipts.Select(item => item.ConversationId).Distinct().Count() == 1 && Count("Conversation") == 2, "Twelve concurrent SQLite creates with same key commit one conversation");
    await Denied(() => store.CreateConversationAsync(a, "es", ConversationIngress.Hash("parallel"), ConversationIngress.Hash("es"), now, ct), 409, "Reused key with different payload conflicts in persistent store");
    var id = receipts[0].ConversationId; var client = Guid.NewGuid(); var ingress = new ConversationIngress(store, TimeProvider.System);
    var accepted = await ingress.SubmitAsync(a, id, client, "Correo demo@example.invalid password=privateCanary", 1, ct);
    var duplicate = await ingress.SubmitAsync(a, id, client, "Correo demo@example.invalid password=privateCanary", 1, ct);
    Check(accepted == duplicate && Count("Message") == 1 && Count("Inbox") == 1, "Repeated message atomically preserves one message/inbox receipt");
    await Denied(() => ingress.SubmitAsync(a, id, client, "changed", 2, ct), 409, "Client message replay with changed body denied");
    await Denied(() => ingress.SubmitAsync(a, id, Guid.NewGuid(), "stale", 1, ct), 409, "Persistent version check rejects stale write");
    var reopened = new SqliteOperationalStore(path);
    var transcript = await reopened.ReadConversationAsync(a, id, now, ct);
    Check(transcript?.Messages.Count == 1 && transcript.Resource.Version == 2 && transcript.Messages[0].Text == "Correo [EMAIL] [SECRET]", "New repository instance recovers committed sanitized conversation and version");
    Check(Count("Audit") >= 6, "Session/login/create/message events committed to audit");
    await reopened.RevokeSessionAsync(a.SessionId, now, ct);
    Check(await store.FindSessionAsync(a.SessionId, now, ct) is null, "Logout revocation visible to original and reopened adapter");
    await Denied(() => store.ReadConversationAsync(a, id, now, ct), 401, "Revoked session cannot use a cached Actor for private access");
    Check(await store.FindSessionAsync(b.SessionId, now.AddMinutes(16), ct) is null, "Session absolute expiry enforced in persistent store");
    File.WriteAllText(Path.Combine(root, "docs/progress/demo-storage-evidence.json"), JsonSerializer.Serialize(new
    {
        result = "PASS", observedAtUtc = DateTimeOffset.UtcNow, checksPassed = checks.Count, checks,
        scope = "Real C# SQLite adapter/transactions/concurrent writes/reopened repository; isolated synthetic DB",
        oidcBrowserLoginVerified = false, sqlServerNativeVerified = false
    }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    Console.WriteLine($"Demo storage checks: {checks.Count} passed."); return 0;
}
catch (Exception exception) { Console.Error.WriteLine("Demo storage checks failed: " + exception.GetType().Name); return 1; }
