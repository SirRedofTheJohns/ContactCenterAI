using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ContactCenterAI.Api;
using ContactCenterAI.Application;
using ContactCenterAI.Domain;
using ContactCenterAI.IngressChecks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

try
{
if (args.Length != 1) throw new ArgumentException("Expected repository root.");
var root = Path.GetFullPath(args[0]); var checks = new List<string>();
void Check(bool result, string label)
{ if (!result) throw new InvalidOperationException("FAIL: " + label); checks.Add(label); Console.WriteLine("PASS: " + label); }
var now = DateTimeOffset.UtcNow; var fixture = new FixtureStore();
Actor Seed(ActorRoles roles, string? member, Guid? principal = null, string tenant = "tenant-demo")
    => fixture.Seed(new Actor(Guid.NewGuid(), tenant, principal ?? Guid.NewGuid(), member, roles, now.AddMinutes(15)));
var a = Seed(ActorRoles.Customer, "MEM-001"); var b = Seed(ActorRoles.Customer, "MEM-002");
var resource = new ConversationResource(Guid.NewGuid(), "tenant-demo", a.PrincipalId, null, 1, 1);
var agent = Seed(ActorRoles.Agent, null); var assignment = new ActiveAssignment(agent.PrincipalId!.Value, resource.Id, "tenant-demo", now.AddMinutes(-1), now.AddMinutes(1), false);
Check(ResourceAccess.CanAccess(a, resource, null, now), "Customer can access own conversation");
Check(!ResourceAccess.CanAccess(b, resource, null, now), "Customer cannot access another member conversation");
Check(!ResourceAccess.CanAccess(agent, resource, null, now), "Agent role alone grants no conversation access");
Check(ResourceAccess.CanAccess(agent, resource, assignment, now), "Agent active exact assignment grants access");
Check(!ResourceAccess.CanAccess(agent, resource, assignment with { Revoked = true }, now), "Assignment revocation denies immediately");
Check(!ResourceAccess.CanAccess(agent, resource, assignment, assignment.EndsAt), "Assignment expiry is exclusive");
Check(!ResourceAccess.CanAccess(agent, resource, assignment with { ConversationId = Guid.NewGuid() }, now), "Assignment cannot authorize another resource");
Check(!ResourceAccess.CanAccess(a with { TenantId = "another-tenant" }, resource, null, now), "Cross-tenant access denied");
Check(!ResourceAccess.CanAccess(a with { ExpiresAt = now }, resource, null, now), "Expired session denies access");
Check(!ResourceAccess.CanAccess(Seed(ActorRoles.OperationsAdmin, null), resource, null, now), "OperationsAdmin has no private-data override");

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production", ContentRootPath = root });
builder.WebHost.UseTestServer();
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["CCAI_BFF_CLIENT_SECRET"] = "fixture-only-unusable-client-secret" });
builder.Services.AddSingleton<IOperationalStore>(fixture);
builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
await using var app = ProductApi.Build(builder); await app.StartAsync();
using var http = app.GetTestClient(); http.BaseAddress = new Uri(ProductApi.Origin);
var cookieOptions = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(ProductApi.CookieScheme);
var oidc = app.Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(ProductApi.OidcScheme);
Check(oidc.UsePkce && oidc.ResponseType == "code" && !oidc.SaveTokens && !oidc.MapInboundClaims, "Product OIDC uses Code+PKCE with no stored tokens");
Check(oidc.TokenValidationParameters.ValidIssuer == ProductApi.Issuer && oidc.TokenValidationParameters.ValidAudience == "contactcenterai-bff" &&
    oidc.TokenValidationParameters.ValidAlgorithms!.SequenceEqual(new[] { "RS256" }), "Product token issuer/audience/algorithm pinned");
Check(!cookieOptions.SlidingExpiration && cookieOptions.ExpireTimeSpan == TimeSpan.FromMinutes(15), "Session cookie has absolute fifteen-minute lifetime");
var cookies = new Dictionary<string, string>(); string? csrfToken = null;
void UseActor(Actor actor)
{
    cookies.Clear(); csrfToken = null;
    var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("ccai_session_id", actor.SessionId.ToString()), new Claim(ClaimTypes.NameIdentifier, actor.SessionId.ToString())], ProductApi.CookieScheme));
    cookies["__Host-ccai.session"] = cookieOptions.TicketDataFormat.Protect(new AuthenticationTicket(principal,
        new AuthenticationProperties { IssuedUtc = now, ExpiresUtc = actor.ExpiresAt }, ProductApi.CookieScheme));
}
async Task<(HttpStatusCode Status, JsonNode? Body, HttpResponseMessage Response)> Send(string method, string path, object? payload = null, string? origin = ProductApi.Origin, bool csrf = true, string? key = null, string? etag = null)
{
    var request = new HttpRequestMessage(new HttpMethod(method), path);
    if (payload is not null) request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    if (cookies.Count > 0) request.Headers.Add("Cookie", string.Join("; ", cookies.Select(row => row.Key + "=" + row.Value)));
    if (origin is not null) request.Headers.Add("Origin", origin);
    if (csrf && csrfToken is not null) request.Headers.Add("X-CSRF-TOKEN", csrfToken);
    if (key is not null) request.Headers.Add("Idempotency-Key", key);
    if (etag is not null) request.Headers.Add("If-Match", etag);
    var response = await http.SendAsync(request);
    if (response.Headers.TryGetValues("Set-Cookie", out var headers))
        foreach (var value in headers)
        { var pair = value.Split(';')[0].Split('=', 2); cookies[pair[0]] = pair[1]; }
    var body = await response.Content.ReadAsStringAsync();
    return (response.StatusCode, string.IsNullOrEmpty(body) ? null : JsonNode.Parse(body), response);
}
async Task Bootstrap()
{
    var result = await Send("GET", "/v1/session/csrf");
    if (result.Status != HttpStatusCode.OK) throw new InvalidOperationException("HTTPS antiforgery bootstrap failed.");
    csrfToken = result.Body!["token"]!.GetValue<string>();
}

var insecure = await Send("POST", "http://127.0.0.1:7450/v1/conversations", new { language = "es" });
Check((int)insecure.Status == 426 && insecure.Body!["code"]!.GetValue<string>() == "HTTPS_REQUIRED", "Insecure product ingress rejected before acceptance");
var absent = await Send("GET", "/v1/session"); Check(absent.Status == HttpStatusCode.Unauthorized, "No cookie creates no authenticated session");
await Bootstrap();
var badOrigin = await Send("POST", "/v1/session/anonymous", origin: "https://attacker.invalid");
Check(badOrigin.Status == HttpStatusCode.Forbidden && badOrigin.Body!["code"]!.GetValue<string>() == "ORIGIN_REJECTED", "Foreign origin denied before session creation");
var noCsrf = await Send("POST", "/v1/session/anonymous", csrf: false);
Check(noCsrf.Status == HttpStatusCode.Forbidden, "Missing antiforgery token denied");
var guest = await Send("POST", "/v1/session/anonymous");
Check(guest.Status == HttpStatusCode.Created && !guest.Body!["authenticated"]!.GetValue<bool>(), "Anonymous session does not become a verified customer");
var sessionCookie = guest.Response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("__Host-ccai.session=", StringComparison.Ordinal));
Check(sessionCookie.Contains("secure", StringComparison.OrdinalIgnoreCase) && sessionCookie.Contains("httponly", StringComparison.OrdinalIgnoreCase) && sessionCookie.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase), "Issued cookie is Secure HttpOnly SameSite Lax");
await Bootstrap();
var anonymousIdentity = await Send("POST", "/v1/conversations", new { language = "es", memberRef = "MEM-002", role = "Customer" }, key: "spoof");
Check(anonymousIdentity.Status == HttpStatusCode.BadRequest, "Closed DTO rejects caller identity and role fields");
UseActor(a); await Bootstrap();
var created = await Send("POST", "/v1/conversations", new { language = "es" }, key: "first");
Check(created.Status == HttpStatusCode.Created, "Valid customer can create conversation through actual product pipeline");
var conversationId = created.Body!["conversationId"]!.GetValue<string>();
for (var repeat = 0; repeat < 100; repeat++)
{
    var duplicate = await Send("POST", "/v1/conversations", new { language = "es" }, key: "first");
    if (duplicate.Body?["conversationId"]?.GetValue<string>() != conversationId) throw new InvalidOperationException("Duplicate receipt mismatch.");
}
Check(fixture.ConversationCount == 1, "HTTP idempotency contract survives one hundred repetitions against fixture port");
var changed = await Send("POST", "/v1/conversations", new { language = "en" }, key: "first");
Check(changed.Status == HttpStatusCode.Conflict, "Same creation key with another payload returns conflict");
var clientMessageId = Guid.NewGuid();
var accepted = await Send("POST", $"/v1/conversations/{conversationId}/messages", new { clientMessageId, text = "hola customer-a@example.invalid password=canarySecret" }, etag: "\"1\"");
Check(accepted.Status == HttpStatusCode.Accepted && accepted.Body!["status"]!.GetValue<string>() == "Pending", "Message acceptance says Pending without fabricated processing or success");
var duplicateMessage = await Send("POST", $"/v1/conversations/{conversationId}/messages", new { clientMessageId, text = "hola customer-a@example.invalid password=canarySecret" }, etag: "\"1\"");
Check(duplicateMessage.Body!["messageId"]!.GetValue<string>() == accepted.Body!["messageId"]!.GetValue<string>() && fixture.MessageCount == 1, "Duplicate message preserves receipt even with original ETag");
var read = await Send("GET", $"/v1/conversations/{conversationId}");
var sanitized = read.Body!["messages"]![0]!["text"]!.GetValue<string>();
Check(!sanitized.Contains("example.invalid") && !sanitized.Contains("canarySecret") && sanitized.Contains("[EMAIL]") && sanitized.Contains("[SECRET]"), "Persisted fixture message contains only sanitized email and secret markers");
var stale = await Send("POST", $"/v1/conversations/{conversationId}/messages", new { clientMessageId = Guid.NewGuid(), text = "otro" }, etag: "\"1\"");
Check(stale.Status == HttpStatusCode.Conflict, "Stale conversation version denied");
var payment = await Send("POST", $"/v1/conversations/{conversationId}/messages", new { clientMessageId = Guid.NewGuid(), text = "4111 1111 1111 1111 CVV 123" }, etag: "\"2\"");
Check(payment.Status == HttpStatusCode.BadRequest && fixture.MessageCount == 1, "Payment-like input rejected before storage");
UseActor(b); await Bootstrap();
var foreign = await Send("GET", $"/v1/conversations/{conversationId}");
var nonexistent = await Send("GET", $"/v1/conversations/{Guid.NewGuid()}");
Check(foreign.Status == HttpStatusCode.NotFound && nonexistent.Status == HttpStatusCode.NotFound && foreign.Body!["code"]!.GetValue<string>() == nonexistent.Body!["code"]!.GetValue<string>(), "Foreign and absent resource return same generic 404 code");
UseActor(a); await Bootstrap();
var oldCsrf = csrfToken;
var logout = await Send("POST", "/v1/session/logout"); Check(logout.Status == HttpStatusCode.NoContent, "Local logout revokes session and clears cookie");
UseActor(a); csrfToken = oldCsrf;
var replayCookie = await Send("GET", "/v1/session"); Check(replayCookie.Status == HttpStatusCode.Unauthorized, "Replaying a revoked cookie is denied by server lookup");
UseActor(b); await Bootstrap(); fixture.Unavailable = true;
var liveDuringOutage = await Send("GET", "/health/live");
Check(liveDuringOutage.Status == HttpStatusCode.OK, "Liveness stays available during store outage even with session cookie");
var outage = await Send("POST", "/v1/conversations", new { language = "en" }, key: "outage");
Check(outage.Status == HttpStatusCode.ServiceUnavailable && outage.Body!["code"]!.GetValue<string>() == "OPERATIONAL_STORE_UNAVAILABLE" && fixture.ConversationCount == 1, "Store outage returns sanitized 503 without acceptance");
fixture.Unavailable = false;
await app.StopAsync();
var report = new { observedAtUtc = DateTimeOffset.UtcNow, result = "PASS", checksPassed = checks.Count, checks,
    scope = "Pure resource policy and actual ASP.NET HTTPS TestServer pipeline; isolated fixture storage and preissued test tickets",
    sqlRepositoryLiveTested = false, productOidcRoundtripLiveTested = false, leasesOrWorkerRecoveryTested = false,
    aiEvaluationExecuted = false, liveGenesysValidated = false };
File.WriteAllText(Path.Combine(root, "docs/progress/b03-b04-ingress-evidence.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine($"Ingress checks: {checks.Count} passed; live product OIDC and SQL repository remain separate gates.");
return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine("Ingress checks failed: " + exception.GetType().Name);
    return 1;
}
