using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContactCenterAI.Application;
using ContactCenterAI.Channels;
using ContactCenterAI.Domain;
using ContactCenterAI.Infrastructure;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

try
{
var repo = Path.GetFullPath(args.Single()); var checks = new List<string>();
void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks.Add(name); Console.WriteLine("PASS: " + name); }
void Denied(Action action, string name) { try { action(); } catch (RequestRejected) { Check(true, name); return; } throw new InvalidOperationException(name); }
var root = Path.Combine(repo, ".local", "channel-checks", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
var now = new DateTimeOffset(2026, 10, 1, 14, 0, 0, TimeSpan.Zero); var clock = new FixtureClock(now);
var tg = new EndpointOptions("telegram", "1234567", new HashSet<string> { "42" }, "1234567:" + new string('x', 35));
var wa = new EndpointOptions("whatsapp", "987", new HashSet<string> { "18095550001" }, "fixture-access", new string('a', 32), new string('v', 32), "v99.0", true);
ChannelStore Fresh(string name) => new(Path.Combine(root, name + ".db"), SHA256.HashData(Encoding.UTF8.GetBytes("synthetic-test-hashing-key")));
ChannelText Text(string id, string text = "Política de cancelación", EndpointOptions? endpoint = null, DateTimeOffset? at = null) { var e = endpoint ?? tg; return new(e.Channel, e.EndpointId, id, e.Recipients.Single(), at ?? now, at ?? now, text); }
PreparedOutput Plain(string text = "Fixture reply") => new(new(text, "TEST_REPLY", []));
string Meta(string body = "Política de cancelación", string id = "wamid.fixture", string phone = "987", string sender = "18095550001") => JsonSerializer.Serialize(new { @object = "whatsapp_business_account", entry = new[] { new { changes = new[] { new { field = "messages", value = new { metadata = new { phone_number_id = phone }, messages = new[] { new { id, from = sender, timestamp = now.ToUnixTimeSeconds().ToString(), type = "text", text = new { body } } } } } } } } });
string Telegram(string type = "private", long sender = 42, long chat = 42) => JsonSerializer.Serialize(new { ok = true, result = new[] { new { update_id = 9, message = new { date = now.ToUnixTimeSeconds(), chat = new { id = chat, type }, from = new { id = sender, is_bot = false }, text = "Política de cancelación" } } } });
string Signature(byte[] bytes) => "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(wa.AppSecret), bytes));
long Scalar(string name, string sql)
{
    using var db = new SqliteConnection("Data Source=" + Path.Combine(root, name + ".db")); db.Open(); using var query = db.CreateCommand(); query.CommandText = sql; return (long)query.ExecuteScalar()!;
}
using var meta = ProviderInput.Parse(Encoding.UTF8.GetBytes(Meta())); var raw = Encoding.UTF8.GetBytes(Meta());
Check(ProviderInput.VerifyMeta(raw, Signature(raw), wa.AppSecret), "Meta HMAC accepts the original signed bytes");
Check(!ProviderInput.VerifyMeta(raw.Concat(new byte[] { 32 }).ToArray(), Signature(raw), wa.AppSecret), "A changed raw byte invalidates the signature");
Check(!ProviderInput.VerifyMeta(raw, wa.VerifyToken, wa.AppSecret), "GET verification token cannot authorize POST");
Check(!ProviderInput.VerifyMeta(raw, "sha256=" + new string('z', 64), wa.AppSecret), "Malformed signature rejected without secret output");
Check(ProviderInput.VerifyChallenge(wa.VerifyToken, wa.VerifyToken) && !ProviderInput.VerifyChallenge("wrong", wa.VerifyToken), "Challenge uses a distinct exact token");
Check(ProviderInput.Meta(meta.RootElement, "987", now).Single().SenderId == wa.Recipients.Single(), "Meta text becomes a neutral envelope");
Check(ProviderInput.Meta(meta.RootElement, "000", now).Count == 0, "Wrong phone-number ID cannot create input");
using var nullBody = ProviderInput.Parse(Encoding.UTF8.GetBytes(Meta().Replace("\"id\":\"wamid.fixture\"", "\"id\":null")));
Denied(() => ProviderInput.Meta(nullBody.RootElement, "987", now), "Null provider ID rejected as bounded input error");
Denied(() => ProviderInput.Parse(new byte[ProviderInput.MaximumBody + 1]), "Oversized body rejected");
Denied(() => ProviderInput.Parse(Encoding.UTF8.GetBytes(new string('[', 40) + "0" + new string(']', 40))), "Excess JSON depth rejected");
using var batch = ProviderInput.Parse(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { @object = "whatsapp_business_account", entry = new[] { new { changes = new[] { new { field = "messages", value = new { metadata = new { phone_number_id = "987" }, messages = Enumerable.Range(0, 101).Select(_ => new { type = "image" }).ToArray() } } } } } })));
Denied(() => ProviderInput.Meta(batch.RootElement, "987", now), "Unsupported events still count toward the batch cap");
using var telegram = ProviderInput.Parse(Encoding.UTF8.GetBytes(Telegram()));
Check(ProviderInput.Telegram(telegram.RootElement, tg.EndpointId, now).Single().Text!.SenderId == "42", "Private Telegram chat is scoped to its sender");
using var group = ProviderInput.Parse(Encoding.UTF8.GetBytes(Telegram("group", 42, -99)));
Check(ProviderInput.Telegram(group.RootElement, tg.EndpointId, now).Single().Text is null, "Group update becomes an ignored tombstone");
using var spoof = ProviderInput.Parse(Encoding.UTF8.GetBytes(Telegram(sender: 99)));
Check(ProviderInput.Telegram(spoof.RootElement, tg.EndpointId, now).Single().Text is null, "Private chat and sender mismatch cannot grant access");
using var invalid = ProviderInput.Parse(Encoding.UTF8.GetBytes("[]"));
Denied(() => ProviderInput.Telegram(invalid.RootElement, tg.EndpointId, now), "Nonobject polling payload rejected");
Denied(() => (wa with { TestResourcesConfirmed = false }).Validate(), "WhatsApp requires explicit test-resource confirmation");
Denied(() => (tg with { Recipients = new HashSet<string>() }).Validate(), "Live endpoints require recipient allowlists");

var inbox = Fresh("inbox");
Check(inbox.Accept(Text("1"), tg) == "Pending", "Allowed input is durably pending");
Check(inbox.Accept(Text("1") with { ReceivedAtUtc = now.AddMinutes(1) }, tg) == "DUPLICATE", "Redelivery time does not change deduplication hash");
Check(inbox.Accept(Text("1", "different content"), tg) == "EVENT_CONFLICT" && inbox.Claim(now) is null, "Conflicting event is quarantined before inference");
Check(inbox.Accept(Text("2") with { SenderId = "99" }, tg) == "Ignored", "Unlisted sender invokes no model job");
Check(inbox.Accept(Text("3") with { OccurredAtUtc = now.AddMinutes(6) }, tg) == "Ignored", "Future timestamps cannot extend the service window");
Check(inbox.Accept(Text("4", string.Concat(Enumerable.Repeat("😀", 2001))), tg) == "Rejected", "Input limit counts Unicode scalars");
Check(inbox.Accept(Text("5", "4111111111111111"), tg) == "Rejected", "Payment card input is rejected before persistence");
inbox.Accept(Text("6", "Mi email es guest@example.invalid"), tg);
Check(!inbox.Claim(now)!.Text.Contains("guest@example.invalid"), "Email is masked in durable worker input");
var poll = Fresh("poll"); var updates = ProviderInput.Telegram(telegram.RootElement, tg.EndpointId, now);
poll.AcceptTelegram(updates.Concat(ProviderInput.Telegram(group.RootElement, tg.EndpointId, now).Select(item => item with { EventId = "10" })).ToArray(), tg, now);
Check(Fresh("poll").ReadOffset(tg.EndpointId) == 11 && Scalar("poll", "SELECT COUNT(*) FROM ChannelInbox") == 2, "Restart preserves the full batch and offset, including ignored updates");
poll.AcceptTelegram(updates, tg, now);
Check(poll.ReadOffset(tg.EndpointId) == 11 && Scalar("poll", "SELECT COUNT(*) FROM ChannelInbox") == 2, "Crash replay neither duplicates work nor rewinds the checkpoint");
var quota = Fresh("quota"); for (var i = 0; i < 100; i++) quota.Accept(Text(i.ToString(), "/start"), tg);
Check(quota.Accept(Text("100", "/start"), tg) == "Rejected", "Durable recipient daily cap stops further questions");
var globalQuota = Fresh("global-quota"); var many = tg with { Recipients = Enumerable.Range(100, 11).Select(i => i.ToString()).ToHashSet() };
for (var i = 0; i < 1000; i++) globalQuota.Accept(Text(i.ToString(), "/start") with { SenderId = (100 + i / 100).ToString() }, many);
Check(globalQuota.Accept(Text("1000", "/start") with { SenderId = "110" }, many) == "Rejected", "Global daily cap also applies across otherwise allowed recipients");

var outbox = Fresh("outbox"); outbox.Accept(Text("1"), tg); var job = outbox.Claim(now)!;
outbox.Complete(job, Plain(), now); outbox.Complete(job, Plain(), now);
Check(Scalar("outbox", "SELECT COUNT(*) FROM ChannelOutbox") == 1, "Repeated completion creates only one outbound message");
var output = outbox.NextOutput(now)!; Check(outbox.BeginSend(output, now), "Sending is committed before transport");
Check(outbox.NextOutput(now.AddSeconds(41)) is null && Scalar("outbox", "SELECT COUNT(*) FROM ChannelOutbox WHERE Status='Unknown'") == 1, "Restart after an ambiguous send produces Unknown with no automatic resend");
var leases = Fresh("leases"); leases.Accept(Text("1"), tg); var expired = leases.Claim(now)!; var reclaimed = leases.Claim(now.AddSeconds(21))!;
leases.Complete(expired, Plain("old worker"), now.AddSeconds(22));
Check(leases.NextOutput(now.AddSeconds(22)) is null, "Expired worker cannot commit after a new lease");
leases.Complete(reclaimed, Plain("new worker"), now.AddSeconds(22)); Check(leases.NextOutput(now.AddSeconds(22))!.Output.Reply.Text == "new worker", "Current lease alone may commit");
var human = Fresh("human"); human.Accept(Text("1"), tg); var normal = human.Claim(now)!; human.Complete(normal, Plain(), now);
human.Accept(Text("2", "/human", at: now.AddSeconds(1)), tg);
Check(!human.BeginSend(human.NextOutput(now.AddSeconds(2))!, now.AddSeconds(2)), "Handoff suppresses an already queued bot answer");
var handoffJob = human.Claim(now.AddSeconds(2))!; var request = human.RequestHandoff(handoffJob.Route.Key);
human.Acknowledge(new(request.RequestId, request.ConversationId, request.Epoch - 1, true, Guid.NewGuid()));
Check(Scalar("human", "SELECT COUNT(*) FROM ChannelRoute WHERE Ownership='HandoffPending'") == 1, "Stale human acknowledgment cannot change ownership");
human.Acknowledge(await new LocalContactCenterMock().RequestHandoffAsync(request, default));
Check(human.Accept(Text("3", "/start", at: now.AddSeconds(3)), tg) == "HumanPending", "Start command cannot take ownership back from a human");
var window = Fresh("window"); window.Accept(Text("1", endpoint: wa), wa); var windowJob = window.Claim(now)!; window.Complete(windowJob, Plain(), now);
Check(!window.BeginSend(window.NextOutput(now.AddHours(24))!, now.AddHours(24)), "Expired WhatsApp customer window cannot send or fall back to a template");

HttpResponseMessage Response(HttpStatusCode code, string data) => new(code) { Content = new StringContent(data, Encoding.UTF8, "application/json") };
var calls = new List<string>();
using var identity = new ProviderClient(tg, new FixtureHttp(async req =>
{
    calls.Add(req.RequestUri!.AbsolutePath.Split('/').Last()); await Task.CompletedTask;
    return Response(HttpStatusCode.OK, req.RequestUri.AbsolutePath.EndsWith("getMe") ? "{\"ok\":true,\"result\":{\"id\":1234567,\"is_bot\":true}}" : "{\"ok\":true,\"result\":{\"url\":\"\"}}");
}));
await identity.CheckTelegramAsync(default); Check(calls.SequenceEqual(new[] { "getMe", "getWebhookInfo" }), "Readiness checks identity and concurrent webhook without dropping updates");
using var conflicting = new ProviderClient(tg, new FixtureHttp(req => Task.FromResult(Response(HttpStatusCode.OK, req.RequestUri!.AbsolutePath.EndsWith("getMe") ? "{\"ok\":true,\"result\":{\"id\":1234567,\"is_bot\":true}}" : "{\"ok\":true,\"result\":{\"url\":\"https://example.invalid/hook\"}}"))));
try { await conflicting.CheckTelegramAsync(default); throw new InvalidOperationException(); } catch (RequestRejected error) { Check(error.Code == "TELEGRAM_WEBHOOK_CONFLICT", "Existing Telegram webhook stops polling without deleting it"); }
using var wrongBot = new ProviderClient(tg, new FixtureHttp(_ => Task.FromResult(Response(HttpStatusCode.OK, "{\"ok\":true,\"result\":{\"id\":999,\"is_bot\":true}}"))));
try { await wrongBot.CheckTelegramAsync(default); throw new InvalidOperationException(); } catch (RequestRejected error) { Check(error.Code == "TELEGRAM_IDENTITY_REJECTED", "Token for another bot fails fixed endpoint identity"); }
string? sentJson = null;
using var sender = new ProviderClient(tg, new FixtureHttp(async req => { sentJson = await req.Content!.ReadAsStringAsync(); return Response(HttpStatusCode.OK, "{\"ok\":true,\"result\":{\"message_id\":7,\"chat\":{\"id\":42}}}"); }));
Check((await sender.SendAsync(output, now, default)).Status == "Sent", "Validated Telegram response records Sent, not Delivered");
using var sent = JsonDocument.Parse(sentJson!);
Check(!sent.RootElement.GetProperty("allow_paid_broadcast").GetBoolean() && !sent.RootElement.TryGetProperty("parse_mode", out _), "Telegram reply disables paid broadcast and markup parsing");
using var timeout = new ProviderClient(tg, new FixtureHttp(_ => throw new HttpRequestException("fixture credential must not be exported")));
Check((await timeout.SendAsync(output, now, default)).Status == "Unknown", "Network exception becomes Unknown without exposing its message");
using var serverError = new ProviderClient(tg, new FixtureHttp(_ => Task.FromResult(Response(HttpStatusCode.InternalServerError, "{}"))));
Check((await serverError.SendAsync(output, now, default)).Status == "Unknown", "Provider 500 is uncertain and never a blind retry");
using var rate = new ProviderClient(tg, new FixtureHttp(_ => Task.FromResult(Response(HttpStatusCode.TooManyRequests, "{\"parameters\":{\"retry_after\":9}}"))));
Check((await rate.SendAsync(output, now, default)) is { Status: "Pending", RetryAfterSeconds: 9 }, "Definitive 429 observes provider retry delay");
Check((await rate.SendAsync(output with { Attempts = 2 }, now, default)).Status == "Failed", "Third rate-limited send exhausts the bounded retry budget");
using var redirect = new ProviderClient(tg, new FixtureHttp(_ => Task.FromResult(Response(HttpStatusCode.TemporaryRedirect, "{}"))));
Check((await redirect.SendAsync(output, now, default)).Status == "Unknown", "Redirect is not treated as delivery or replay permission");
var metaLedger = Fresh("meta-status"); metaLedger.Accept(Text("1", endpoint: wa), wa); metaLedger.Complete(metaLedger.Claim(now)!, Plain(), now); var metaOutput = metaLedger.NextOutput(now)!;
string? metaBody = null; string? authorization = null;
using var metaSender = new ProviderClient(wa, new FixtureHttp(async req => { metaBody = await req.Content!.ReadAsStringAsync(); authorization = req.Headers.Authorization?.Scheme; return Response(HttpStatusCode.OK, "{\"messages\":[{\"id\":\"wamid.sent\"}]}"); }));
var metaResult = await metaSender.SendAsync(metaOutput, now, default);
using var metaSent = JsonDocument.Parse(metaBody!);
Check(metaResult.Status == "Sent" && authorization == "Bearer" && metaSent.RootElement.GetProperty("type").GetString() == "text" && !metaSent.RootElement.TryGetProperty("template", out _), "Meta dispatch uses authenticated plain text with no template branch");
var rejectionDiagnostics = new List<string>();
using var rejectedMeta = new ProviderClient(wa, new FixtureHttp(_ => Task.FromResult(Response(HttpStatusCode.BadRequest,
    "{\"error\":{\"code\":100,\"error_subcode\":33,\"message\":\"fixture-private-token fixture-phone\",\"error_data\":{\"details\":\"private reply text\"},\"fbtrace_id\":\"private-trace\"}}"))), rejectionDiagnostics.Add);
var rejectedResult = await rejectedMeta.SendAsync(metaOutput, now, default);
Check(rejectedResult is { Status: "Failed", ProviderId: null } && rejectionDiagnostics.Single() == "META_SEND_REJECTED HTTP=400 Code=100 Subcode=33", "Meta rejection diagnostic contains only numeric codes, never provider messages or personal details");
foreach (var (body, name) in new[] {
    ("not-json private-token", "Malformed diagnostic JSON preserves the definitive Failed outcome"),
    ("{\"error\":{\"code\":\"private-token\",\"error_subcode\":\"private-phone\"}}", "Non-numeric diagnostic fields cannot enter logs"),
    (new string('x', ProviderInput.MaximumBody + 1), "Oversized diagnostic body is bounded without changing Failed") })
{
    var diagnostics = new List<string>();
    using var rejection = new ProviderClient(wa, new FixtureHttp(_ => Task.FromResult(Response(HttpStatusCode.BadRequest, body))), diagnostics.Add);
    Check((await rejection.SendAsync(metaOutput, now, default)).Status == "Failed" && diagnostics.Single() == "META_SEND_REJECTED HTTP=400 Code=none Subcode=none", name);
}
var unreadableDiagnostics = new List<string>();
using var unreadableMeta = new ProviderClient(wa, new FixtureHttp(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new UnreadableContent() })), unreadableDiagnostics.Add);
Check((await unreadableMeta.SendAsync(metaOutput, now, default)).Status == "Failed" && unreadableDiagnostics.Single() == "META_SEND_REJECTED HTTP=400 Code=none Subcode=none", "Diagnostic body read failure cannot turn a definitive rejection into Unknown");
using var brokenLoggerMeta = new ProviderClient(wa, new FixtureHttp(_ => Task.FromResult(Response(HttpStatusCode.BadRequest, "{}"))), _ => throw new IOException("fixture-private-logger-error"));
Check((await brokenLoggerMeta.SendAsync(metaOutput, now, default)).Status == "Failed", "Diagnostic sink failure cannot change the transport outcome");
var ambiguousDiagnostics = new List<string>();
using var ambiguousMeta = new ProviderClient(wa, new FixtureHttp(_ => Task.FromResult(Response(HttpStatusCode.InternalServerError, "{\"error\":{\"code\":100}}"))), ambiguousDiagnostics.Add);
Check((await ambiguousMeta.SendAsync(metaOutput, now, default)).Status == "Unknown" && ambiguousDiagnostics.Count == 0, "Meta 500 remains Unknown and is not reported as a definitive rejection");
var failedMetaLedger = Fresh("failed-meta-diagnostic"); failedMetaLedger.Accept(Text("1", endpoint: wa), wa); failedMetaLedger.Complete(failedMetaLedger.Claim(now)!, Plain(), now);
var failedMetaRuntime = new ChannelRuntime(failedMetaLedger, new PublicResponder(new SqliteOperationalStore(Path.Combine(root, "failed-meta-knowledge.db")), new SimulatedIntentProvider(), clock), new Dictionary<string, ProviderClient> { ["whatsapp"] = rejectedMeta }, new LocalContactCenterMock(), clock);
Check(await failedMetaRuntime.SendOneAsync(default) && !await failedMetaRuntime.SendOneAsync(default) && Scalar("failed-meta-diagnostic", "SELECT COUNT(*) FROM ChannelOutbox WHERE Status='Failed'") == 1, "Definitive rejection is persisted and diagnostics create no automatic resend");
metaLedger.BeginSend(metaOutput, now); metaLedger.FinishSend(metaOutput.Id, "Unknown", null, now);
string Status(string state, string recipient = "18095550001") => JsonSerializer.Serialize(new { @object = "whatsapp_business_account", entry = new[] { new { changes = new[] { new { field = "messages", value = new { metadata = new { phone_number_id = "987" }, statuses = new[] { new { id = "wamid.sent", status = state, recipient_id = recipient, biz_opaque_callback_data = metaOutput.Id.ToString("D") } } } } } } } });
using (var wrongReceipt = JsonDocument.Parse(Status("read", "99"))) metaLedger.MetaStatuses(wrongReceipt.RootElement, "987");
Check(Scalar("meta-status", "SELECT COUNT(*) FROM ChannelOutbox WHERE Status='Unknown'") == 1, "Receipt for another recipient cannot reconcile an uncertain send");
using (var receipt = JsonDocument.Parse(Status("read"))) metaLedger.MetaStatuses(receipt.RootElement, "987");
using (var lateReceipt = JsonDocument.Parse(Status("sent"))) metaLedger.MetaStatuses(lateReceipt.RootElement, "987");
Check(Scalar("meta-status", "SELECT COUNT(*) FROM ChannelOutbox WHERE Status='Read'") == 1, "Matching callback resolves Unknown; reordered Sent cannot downgrade Read");

var knowledgePath = Path.Combine(root, "knowledge.db"); var knowledge = new SqliteOperationalStore(knowledgePath); var publicResponder = new PublicResponder(knowledge, new SimulatedIntentProvider(), clock);
var publicJob = new ChannelJob("fixture", Guid.NewGuid(), output.Route, "Política de cancelación");
var answer = await publicResponder.AnswerAsync(publicJob, default);
Check(answer.Reply.ReasonCode == "GROUNDED_EXTRACT" && answer.Reply.Text.Contains("72 horas") && answer.Reply.Citations.Count == 1, "Public channel reuses governed Spanish evidence");
Check(answer.Guest is { Roles: ActorRoles.None, PrincipalId: null, MemberRef: null }, "Provider metadata grants neither member identity nor roles");
Check((await publicResponder.AnswerAsync(publicJob with { Text = "Mis reservas MEM-001" }, default)).Reply.ReasonCode == "VERIFIED_MEMBER_REQUIRED", "Typed member number cannot enable private reservation tools");
Check((await publicResponder.AnswerAsync(publicJob with { Text = "Cancelar RES-001" }, default)).Reply.ReasonCode == "VERIFIED_MEMBER_REQUIRED", "Cancellation proposal has no source-write capability in this slice");
var english = await publicResponder.AnswerAsync(publicJob with { Route = output.Route with { Language = "en" }, Text = "What is the cancellation policy?" }, default);
Check(english.Reply.Text.Contains("72 hours") && english.Reply.Citations.Single().DocumentId.EndsWith("-EN"), "English route uses the approved English section");
Check(await knowledge.ResolveAsync(answer.Guest!, answer.ConversationId!.Value, "KB-AGENT-ES", 1, "overview", now, default) is null, "Guest cannot resolve Agent-classified knowledge");
Check((await publicResponder.AnswerAsync(publicJob with { Text = "devolver mi dinero" }, default)).Reply.ReasonCode == "OUT_OF_SCOPE", "Existing deterministic scope guard still precedes channel inference");
Check(await publicResponder.StillValidAsync(answer, default), "Prepared public citation is valid at dispatch time");
var reviewer = await knowledge.LoginAsync("http://localhost:8080/realms/contactcenterai-local", "10000000-0000-0000-0000-000000000007", ActorRoles.KnowledgeReviewer, null, now, default);
await knowledge.RevokeKnowledgeAsync(reviewer, answer.Reply.Citations.Single().DocumentId, 1, now, default);
Check(!await publicResponder.StillValidAsync(answer, default), "Revoked citation cannot leave the outbox");
var runtimeStore = Fresh("runtime"); var runtimeKnowledge = new SqliteOperationalStore(Path.Combine(root, "runtime.db"));
var runtimeResponder = new PublicResponder(runtimeKnowledge, new SimulatedIntentProvider(), clock); var dispatchCount = 0;
using var runtimeSender = new ProviderClient(tg, new FixtureHttp(_ => { dispatchCount++; return Task.FromResult(Response(HttpStatusCode.OK, "{\"ok\":true,\"result\":{\"message_id\":8,\"chat\":{\"id\":42}}}")); }));
var runtime = new ChannelRuntime(runtimeStore, runtimeResponder, new Dictionary<string, ProviderClient> { ["telegram"] = runtimeSender }, new LocalContactCenterMock(), clock);
runtimeStore.Accept(Text("1", "/en"), tg); await runtime.ProcessOneAsync(default); await runtime.SendOneAsync(default);
Check(dispatchCount == 1 && Scalar("runtime", "SELECT COUNT(*) FROM ChannelRoute WHERE Language='en'") == 1, "Worker persists language control and delivers through the outbox");
runtimeStore.Accept(Text("2", "What is the cancellation policy?"), tg); await runtime.ProcessOneAsync(default);
var prepared = runtimeStore.NextOutput(now)!.Output;
var runtimeReviewer = await runtimeKnowledge.LoginAsync("http://localhost:8080/realms/contactcenterai-local", "10000000-0000-0000-0000-000000000007", ActorRoles.KnowledgeReviewer, null, now, default);
await runtimeKnowledge.RevokeKnowledgeAsync(runtimeReviewer, prepared.Reply.Citations.Single().DocumentId, 1, now, default);
await runtime.SendOneAsync(default); Check(dispatchCount == 1, "Dispatcher revalidates a revoked citation and makes zero provider calls");
runtimeStore.Accept(Text("3", "/human"), tg); await runtime.ProcessOneAsync(default); await runtime.SendOneAsync(default);
Check(dispatchCount == 2 && Scalar("runtime", "SELECT COUNT(*) FROM ChannelRoute WHERE Ownership='HumanOwned'") == 1, "End-to-end simulated handoff pauses the bot and emits only its queue notice");
runtimeStore.Accept(Text("4", "What services?"), tg);
Check(!await runtime.ProcessOneAsync(default) && !await runtime.SendOneAsync(default), "Human-owned route performs no further AI or dispatch work");
runtimeStore.Cleanup(now.AddDays(8));
Check(Scalar("runtime", "SELECT COUNT(*) FROM Conversation") == 0 && Scalar("runtime", "SELECT COUNT(*) FROM UserSession WHERE PrincipalId IS NULL") == 0, "Retention also removes isolated guest knowledge contexts");
var retention = Fresh("retention"); retention.Accept(Text("1", "old unique text"), tg); retention.Complete(retention.Claim(now)!, Plain("old outbound unique text"), now);
retention.Accept(Text("2", "recent", at: now.AddDays(6)), tg); retention.Cleanup(now.AddDays(8));
Check(Scalar("retention", "SELECT COUNT(*) FROM ChannelOutbox") == 0 && Scalar("retention", "SELECT COUNT(*) FROM ChannelInbox WHERE Text LIKE '%old%'") == 0, "Seven-day cleanup removes old outputs even when the route is still active");
Check(Scalar("retention", "SELECT COUNT(*) FROM ChannelInbox WHERE Status='Tombstone'") == 1, "Deduplication tombstone remains after text removal");
retention.Cleanup(now.AddDays(31)); Check(Scalar("retention", "SELECT COUNT(*) FROM ChannelInbox WHERE ReceivedAt=" + now.ToUnixTimeMilliseconds()) == 0, "Thirty-day cleanup removes expired tombstones");

var httpStore = Fresh("http"); await using var host = ChannelHost.Build(httpStore, wa, 0); await host.StartAsync();
var address = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
using var http = new HttpClient { BaseAddress = new Uri(address) };
Check((await http.GetAsync("/health/live")).StatusCode == HttpStatusCode.OK && (await http.GetAsync("/login")).StatusCode == HttpStatusCode.NotFound && (await http.GetAsync("/api/reservations")).StatusCode == HttpStatusCode.NotFound, "Dedicated host exposes no login or private product routes");
Check(await http.GetStringAsync("/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=" + wa.VerifyToken + "&hub.challenge=12345") == "12345", "Real HTTP challenge returns exact plain text");
Check((await http.GetAsync("/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=wrong&hub.challenge=12345")).StatusCode == HttpStatusCode.Forbidden, "Real HTTP challenge rejects wrong token");
Check((await http.PostAsync("/webhooks/whatsapp", new ByteArrayContent(raw))).StatusCode == HttpStatusCode.Unauthorized && Scalar("http", "SELECT COUNT(*) FROM ChannelInbox") == 0, "Unsigned HTTP input creates no durable question");
async Task<HttpResponseMessage> Post(byte[] body)
{
    using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/whatsapp") { Content = new ByteArrayContent(body) }; request.Headers.Add("X-Hub-Signature-256", Signature(body)); return await http.SendAsync(request);
}
using var accepted = await Post(raw); using var duplicate = await Post(raw);
Check(accepted.StatusCode == HttpStatusCode.OK && duplicate.StatusCode == HttpStatusCode.OK && Scalar("http", "SELECT COUNT(*) FROM ChannelInbox") == 1, "HTTP signed replay acknowledges only one durable event");
using var malformed = await Post(Encoding.UTF8.GetBytes("{}"));
Check(malformed.StatusCode == HttpStatusCode.BadRequest && !(await malformed.Content.ReadAsStringAsync()).Contains(wa.AppSecret), "Malformed signed input returns a stable error without raw details");
HttpStatusCode limited = HttpStatusCode.OK; for (var i = 0; i < 60; i++) { using var response = await http.GetAsync("/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=wrong&hub.challenge=1"); limited = response.StatusCode; }
Check(limited == HttpStatusCode.TooManyRequests, "Public webhook ingress has a real per-host rate limit");
await host.StopAsync();
var report = new { version = "0.11", evidence = "MockValidated", checks = checks.Count, names = checks, providerCalls = "fake HTTP only; no real recipients or credentials", modelCalls = "deterministic intent fixture; existing inference reports preserved", retention = "logical SQLite cleanup; not secure erasure", generatedAtUtc = DateTimeOffset.UtcNow };
await File.WriteAllTextAsync(Path.Combine(repo, "docs/progress/channels-activation-checks.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("Channel checks completed: " + checks.Count);
}
catch (Exception error)
{
    Console.Error.WriteLine("CHANNEL_CHECK_FAILED: " + error.GetType().Name);
    Environment.ExitCode = 1;
}

sealed class FixtureClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
sealed class FixtureHttp(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
{ protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request); }
sealed class UnreadableContent : HttpContent
{
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.FromException(new IOException("fixture-private-body-error"));
    protected override bool TryComputeLength(out long length) { length = 0; return false; }
}
