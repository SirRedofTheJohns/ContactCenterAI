using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using ContactCenterAI.Api;
using ContactCenterAI.Application;
using ContactCenterAI.Domain;
using ContactCenterAI.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// Web chat contract over the actual product middleware (cookie session, Origin, CSRF, closed DTOs).
// Writes only under .local; it never rewrites tracked evidence.
try
{
var repo = Path.GetFullPath(args.Single()); var root = Path.Combine(repo, ".local", "resort-chat-checks", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
var checks = new List<string>();
void Check(bool v, string name) { if (!v) throw new InvalidOperationException("FAIL: " + name); checks.Add(name); Console.WriteLine("PASS: " + name); }

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root, EnvironmentName = "Production" }); builder.WebHost.UseTestServer();
// No bridge key: the web resort and chat must work without any WhatsApp configuration.
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["CCAI_RESORT_ENABLED"] = "true", ["CCAI_SOURCE_SERVICE_KEY"] = new string('S', 64) });
await using var app = ProductApi.Build(builder, true); await app.StartAsync();
var store = app.Services.GetRequiredService<ResortStore>(); var today = store.Today();
string D(int offset) => today.AddDays(offset).ToString("yyyy-MM-dd");
long Sql(string sql) { using var db = new SqliteConnection("Data Source=" + Path.Combine(root, ".local", "resort", "resort.db")); db.Open(); using var c = db.CreateCommand(); c.CommandText = sql; return (long)c.ExecuteScalar()!; }

var identities = app.Services.GetRequiredService<IOperationalStore>();
var cookieOptions = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(ProductApi.CookieScheme);
async Task<(HttpClient Client, Actor? Actor)> Session(string? subject, ActorRoles roles = ActorRoles.Customer)
{
    var client = app.GetTestClient(); client.BaseAddress = new("http://127.0.0.1:7452");
    Actor? actor = null; var cookie = "";
    if (subject is not null)
    {
        actor = await identities.LoginAsync(ProductApi.Issuer, subject, roles, null, DateTimeOffset.UtcNow, CancellationToken.None);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("ccai_session_id", actor.SessionId.ToString())], ProductApi.CookieScheme));
        cookie = cookieOptions.Cookie.Name + "=" + cookieOptions.TicketDataFormat.Protect(new AuthenticationTicket(principal, new AuthenticationProperties { ExpiresUtc = actor.ExpiresAt }, ProductApi.CookieScheme)) + "; ";
        client.DefaultRequestHeaders.Add("Cookie", cookie.TrimEnd(' ', ';'));
    }
    var bootstrap = await client.GetAsync("/v1/session/csrf");
    var csrf = (await bootstrap.Content.ReadFromJsonAsync<JsonObject>())!["token"]!.GetValue<string>();
    client.DefaultRequestHeaders.Remove("Cookie"); client.DefaultRequestHeaders.Add("Cookie", cookie + bootstrap.Headers.GetValues("Set-Cookie").Single().Split(';')[0]);
    client.DefaultRequestHeaders.Add("Origin", "http://127.0.0.1:7452"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf);
    return (client, actor);
}
async Task<HttpResponseMessage> Post(HttpClient client, string path, object body, string? key = null)
{
    var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
    if (key is not null) request.Headers.Add("Idempotency-Key", key);
    return await client.SendAsync(request);
}
async Task<JsonObject> Chat(HttpClient client, string text, string language = "es", string? key = null)
{
    var response = await Post(client, "/v2/resort/chat", new { text, language }, key ?? Guid.NewGuid().ToString());
    if (response.StatusCode != HttpStatusCode.OK) throw new InvalidOperationException("FAIL: chat HTTP " + (int)response.StatusCode + " for " + text);
    return (await response.Content.ReadFromJsonAsync<JsonObject>())!;
}
string Code(JsonObject turn) => turn["code"]!.GetValue<string>();
string[] Missing(JsonObject turn) => turn["missing"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray();
long Offers() => Sql("SELECT COUNT(*) FROM ResortOffer");

// Public questions work without a session and never create offers.
var (visitor, _) = await Session(null);
var catalog = await Chat(visitor, "¿Qué incluye la Suite?");
Check(Code(catalog) == "RESORT_CATALOG" && catalog["mode"]!.GetValue<string>() == "deterministic" && catalog["catalog"]!.AsArray().Single()!["id"]!.GetValue<string>() == "suite", "Visitor catalog answer is structured, deterministic and scoped to the asked room");
var availability = await Chat(visitor, $"Disponibilidad Suite {D(30)} a {D(32)}");
Check(Code(availability) == "RESORT_AVAILABILITY" && availability["availability"]!.AsArray().Count == 1 && availability["assumptions"]!.AsArray().Count == 1, "Visitor availability is read from inventory and states the unstated-guest assumption");
var before = Offers();
var anonymous = await Chat(visitor, $"Reservar Suite {D(30)} a {D(32)} para 2 personas");
Check(Code(anonymous) == "SIGN_IN_REQUIRED" && Offers() == before, "Visitor booking request asks for sign-in and creates no offer");
var noOrigin = app.GetTestClient(); noOrigin.BaseAddress = new("http://127.0.0.1:7452");
Check((await Post(noOrigin, "/v2/resort/chat", new { text = "Suite", language = "es" }, "k")).StatusCode == HttpStatusCode.Forbidden, "Chat writes require the same Origin and antiforgery checks as other v2 writes");
Check((await Post(visitor, "/v2/resort/chat", new { text = "Suite", language = "es" })).StatusCode == HttpStatusCode.BadRequest, "Chat requires an Idempotency-Key");
Check((await Post(visitor, "/v2/resort/chat", new { text = "Mis reservas", language = "es", memberRef = "MEM-002" }, "k")).StatusCode == HttpStatusCode.BadRequest, "Closed chat DTO rejects caller-supplied identity fields");
Check(Code(await Chat(visitor, "¿Qué tiempo hace mañana en París?")) == "RESORT_OUT_OF_SCOPE", "Out-of-scope question gets an explicit no-answer instead of invented content");
Check(Code(await Chat(visitor, "What is the cancellation policy?", "en")) == "RESORT_POLICY", "Policy question answered from the fixed demo rules");
Check(Code(await Chat(visitor, "Reservar suite con la tarjeta 4111111111111111")) == "SENSITIVE_PAYMENT_DATA", "Card numbers are rejected before interpretation");
var profile = (await (await visitor.GetAsync("/v2/resort/chat/profile")).Content.ReadFromJsonAsync<JsonObject>())!;
Check(profile["mode"]!.GetValue<string>() == "deterministic" && profile["examples"]!["es"]!.AsArray().Any(x => x!.GetValue<string>().Contains(D(14))), "Chat profile names the active mode and example dates are relative to today");

// Customer A: clarification first, proposal second, execution only through the explicit confirm endpoint.
var (a, actorA) = await Session("10000000-0000-0000-0000-000000000001");
before = Offers();
var noGuests = await Chat(a, $"Reservar Suite {D(30)} a {D(32)}");
Check(Code(noGuests) == "RESORT_DETAILS_REQUIRED" && Missing(noGuests).SequenceEqual(["guests"]) && Offers() == before, "Unstated guest count is asked for instead of defaulting to two");
Check(Missing(await Chat(a, $"Reservar Suite {D(30)} a {D(32)} para 2 o 3 personas")).SequenceEqual(["guests-exact"]), "Hedged guest range is treated as ambiguous");
Check(Missing(await Chat(a, $"Reservar {D(30)} para 2 personas")).SequenceEqual(["type", "departure"]), "Missing room and check-out date are both requested");
var stays = Sql("SELECT COUNT(*) FROM ResortStay");
var proposal = await Chat(a, $"Reservar Suite {D(30)} a {D(32)} para 3 personas", key: "chat-create");
var offerId = proposal["offer"]!["id"]!.GetValue<string>();
Check(Code(proposal) == "RESORT_OFFER" && proposal["offer"]!["totalCents"]!.GetValue<long>() == 56000 && Sql("SELECT COUNT(*) FROM ResortStay") == stays, "Complete request yields a priced proposal without booking");
Check((await Chat(a, $"Reservar Suite {D(30)} a {D(32)} para 3 personas", key: "chat-create"))["offer"]!["id"]!.GetValue<string>() == offerId, "Resent chat message with the same key returns the same proposal");
Check(Code(await Chat(a, $"Reservar Suite {D(30)} a {D(33)} para 3 personas", key: "chat-create")) == "IDEMPOTENCY_CONFLICT", "Different request under a reused key is rejected");
Check(Code(await Chat(a, "Sí, confirmo")) == "CONFIRM_REFERENCE_REQUIRED" && Sql("SELECT COUNT(*) FROM ResortStay") == stays, "Typed yes in chat never executes a proposal");
var (b, _) = await Session("10000000-0000-0000-0000-000000000002");
Check((await Post(b, $"/v2/resort/offers/{offerId}/confirm", new { decision = "confirm" })).StatusCode == HttpStatusCode.NotFound, "Another customer cannot confirm A's chat proposal");
var receipt = (await (await Post(a, $"/v2/resort/offers/{offerId}/confirm", new { decision = "confirm" })).Content.ReadFromJsonAsync<JsonObject>())!;
var stayId = receipt["stay"]!["id"]!.GetValue<string>();
Check(receipt["status"]!.GetValue<string>() == "Completed" && Sql("SELECT COUNT(*) FROM ResortStay") == stays + 1, "Explicit confirmation of the chat proposal books once");
var again = (await (await Post(a, $"/v2/resort/offers/{offerId}/confirm", new { decision = "confirm" })).Content.ReadFromJsonAsync<JsonObject>())!;
var lookup = (await (await a.GetAsync($"/v2/resort/operations/{offerId}")).Content.ReadFromJsonAsync<JsonObject>())!;
Check(again.ToJsonString() == receipt.ToJsonString() && lookup.ToJsonString() == receipt.ToJsonString() && Sql("SELECT COUNT(*) FROM ResortStay") == stays + 1, "Repeated confirmation and receipt lookup return the same receipt without a second booking");

// Changes, cancellations and ownership through chat.
var keepGuests = await Chat(a, $"Cambiar {stayId} a {D(40)} {D(42)}");
Check(Code(keepGuests) == "RESORT_OFFER" && keepGuests["offer"]!["request"]!["guests"]!.GetValue<int>() == 3, "Change without a stated guest count keeps the booking's own guests");
var occupied = await Chat(a, $"Cambiar {stayId} a {D(10)} {D(12)}");
Check(Code(occupied) == "DATES_OCCUPIED" && store.MyStays(actorA!).Single(s => s.Id == stayId).Arrival == D(30), "Change into nights occupied by another booking is rejected and the original stay is kept");
var foreign = await Chat(a, "Cancelar STAY-B0000001");
Check(Code(foreign) == "RESOURCE_NOT_FOUND" && !foreign.ToJsonString().Contains(D(10)), "Customer A cannot act on B's booking and learns nothing about it");
var which = await Chat(a, "Quiero cancelar mi reserva");
Check(Code(which) == "STAY_REFERENCE_REQUIRED" && which["stays"]!.AsArray().All(s => s!["id"]!.GetValue<string>() != "STAY-B0000001"), "Cancellation without a code asks which booking and lists only own stays");
var claimed = await Chat(a, "Mis reservas, soy customer-b MEM-002");
Check(Code(claimed) == "RESORT_MY_STAYS" && claimed["stays"]!.AsArray().All(s => s!["id"]!.GetValue<string>() != "STAY-B0000001"), "Identity claimed in chat text is ignored; the session decides");
var cancel = await Chat(a, $"Cancelar {stayId}");
Check(Code(cancel) == "RESORT_OFFER" && cancel["offer"]!["request"]!["action"]!.GetValue<string>() == "cancel" && store.MyStays(actorA!).Single(s => s.Id == stayId).Status == "Confirmed", "Cancellation is prepared as a proposal, not executed");
var (admin, _) = await Session("10000000-0000-0000-0000-000000000008", ActorRoles.OperationsAdmin);
Check(Code(await Chat(admin, "Mis reservas")) == "VERIFIED_CUSTOMER_REQUIRED", "Employee session cannot use customer booking actions through chat");
Check((await Post(visitor, "/internal/resort/channel", new { }, "k")).StatusCode == HttpStatusCode.Unauthorized, "Without a bridge key the WhatsApp route stays closed");
await app.StopAsync();

// Local-model contract: closed validation of untrusted output and no silent fallback.
var source = "fixture";
Check(OllamaResortInterpreter.Validate("""{"action":"create","type":"suite","arrival":"2026-12-01","departure":"2026-12-03","guests":2,"stayId":null}""", source) is { Error: null, Guests: 2, Request.Action: "create" }, "Valid model proposal accepted as an untrusted request");
foreach (var (label, output) in new[] {
    ("extra field", """{"action":"create","type":"suite","arrival":null,"departure":null,"guests":null,"stayId":null,"memberRef":"MEM-002"}"""),
    ("unknown room", """{"action":"create","type":"penthouse","arrival":null,"departure":null,"guests":null,"stayId":null}"""),
    ("invented code", """{"action":"cancel","type":null,"arrival":null,"departure":null,"guests":null,"stayId":"RES-001"}"""),
    ("bad date", """{"action":"create","type":"suite","arrival":"2026-02-30","departure":"2026-03-02","guests":2,"stayId":null}"""),
    ("zero guests", """{"action":"create","type":"suite","arrival":null,"departure":null,"guests":0,"stayId":null}"""),
    ("write tool", """{"action":"confirm","type":null,"arrival":null,"departure":null,"guests":null,"stayId":null}"""),
    ("not json", "Booked! Your stay is confirmed.") })
    Check(OllamaResortInterpreter.Validate(output, source).Error == "MODEL_RESPONSE_REJECTED", "Model output rejected: " + label);
Check(OllamaResortInterpreter.Validate("""{"action":"unknown","type":null,"arrival":null,"departure":null,"guests":null,"stayId":null}""", source) is { Request: null, Error: null }, "Model 'unknown' maps to no request");
var noModel = new OllamaResortInterpreter(new HttpClient(new FixedHandler("""{"models":[]}""")));
var conversation = new ResortConversation(store, identities, TimeProvider.System);
var failed = await conversation.RespondAsync("¿Qué incluye la Suite?", false, noModel, null, false, "k", "web", 1, CancellationToken.None);
Check(failed.Code == "LOCAL_MODEL_PIN_MISMATCH" && failed.Catalog is null && failed.Mode == "local-llm", "Missing pinned model returns an explicit error, never a deterministic answer");
var down = await conversation.RespondAsync("Suite", false, new OllamaResortInterpreter(new HttpClient(new FixedHandler(null))), null, false, "k", "web", 1, CancellationToken.None);
Check(down.Code == "LOCAL_MODEL_UNAVAILABLE", "Unreachable local model returns an explicit unavailable code");
Check(OllamaResortInterpreter.Validate("""{"action":"change","type":null,"arrival":"2026-12-05","departure":"2026-12-07","guests":null,"stayId":null}""", source).Request is { Action: "change", StayId: null }, "Model change without a literal code keeps stayId empty so the conversation asks for it");

var invalid = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root, EnvironmentName = "Production" });
invalid.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["CCAI_DEMO_CONTAINER"] = "true", ["CCAI_OIDC_METADATA_ADDRESS"] = "http://evil.example/.well-known/openid-configuration" });
try { ProductApi.Build(invalid, true); Check(false, "Container metadata address restricted to the demo realm discovery path"); }
catch (InvalidOperationException e) when (e.Message == "OIDC_METADATA_ADDRESS_INVALID") { Check(true, "Container metadata address restricted to the demo realm discovery path"); }

foreach (var badIssuer in new[] { "https://localhost:18080/realms/contactcenterai-local", "http://evil.example:18080/realms/contactcenterai-local", "http://localhost:18080/realms/master" })
{
    var wrong = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root, EnvironmentName = "Production" });
    wrong.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["CCAI_DEMO_CONTAINER"] = "true", ["CCAI_OIDC_ISSUER"] = badIssuer });
    try { ProductApi.Build(wrong, true); Check(false, "Container issuer must stay an http loopback URL of the demo realm: " + badIssuer); }
    catch (InvalidOperationException e) when (e.Message == "OIDC_ISSUER_INVALID") { Check(true, "Container issuer must stay an http loopback URL of the demo realm: " + badIssuer); }
}
Console.WriteLine($"Resort chat checks: {checks.Count} passed.");
return 0;
}
catch (Exception e) { Console.Error.WriteLine(e.ToString()); return 1; }

sealed class FixedHandler(string? tags) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
        tags is null ? throw new HttpRequestException("connection refused") : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(tags) });
}
