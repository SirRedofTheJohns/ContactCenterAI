using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Serialization;
using ContactCenterAI.Application;
using ContactCenterAI.Domain;
using ContactCenterAI.Infrastructure;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Net;

namespace ContactCenterAI.Api;

public static class ProductApi
{
    public const string CookieScheme = "ccai-cookie", OidcScheme = "ccai-oidc";
    public const string Origin = "https://localhost:7451";
    public const string Issuer = "http://localhost:8080/realms/contactcenterai-local";
    private const string SessionClaim = "ccai_session_id";
    public static WebApplication Build(WebApplicationBuilder builder, bool localDemo = false)
    {
        var origin = localDemo ? "http://127.0.0.1:7452" : Origin;
        var securePolicy = localDemo ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
        var traceExporter=localDemo?new LocalTraceExporter(Path.Combine(builder.Environment.ContentRootPath,".local","telemetry")):null;
        if (localDemo)
        {
            var retrievalMode=builder.Configuration["CCAI_RETRIEVAL_PROVIDER"]??"topic";
            if(retrievalMode is not("topic" or "semantic"))throw new InvalidOperationException("RETRIEVAL_PROVIDER_MODE_INVALID");
            var embedding=retrievalMode=="semantic"?new OllamaEmbeddingProvider():null;
            if(embedding is not null)builder.Services.AddSingleton(embedding);
            var reranker=embedding is not null?new OllamaKnowledgeReranker():null;
            if(reranker is not null)builder.Services.AddSingleton(reranker);
            var store = new SqliteOperationalStore(Path.Combine(builder.Environment.ContentRootPath, ".local", "demo", "operations.db"),embedding,reranker);
            builder.Services.AddSingleton<IOperationalStore>(store); builder.Services.AddSingleton<IRuntimeReadiness>(store);
            builder.Services.AddSingleton<ICancellationStore>(store); builder.Services.AddSingleton<ICommandLedger>(store);
            builder.Services.AddSingleton(store);
            builder.Services.AddSingleton<IAssistantLedger>(store); builder.Services.AddSingleton<IKnowledgeStore>(store);
            builder.Services.AddSingleton<IHandoffStore>(store);
            var intentMode = builder.Configuration["CCAI_INTENT_PROVIDER"] ?? "simulated";
            if (intentMode == "local-llm")
            {
                builder.Services.AddSingleton(OllamaIntentProvider.CreateClient());
                builder.Services.AddSingleton<IIntentProvider, OllamaIntentProvider>();
            }
            else if (intentMode == "simulated") builder.Services.AddSingleton<IIntentProvider, SimulatedIntentProvider>();
            else throw new InvalidOperationException("INTENT_PROVIDER_MODE_INVALID");
            builder.Services.AddSingleton<IContactCenterAdapter, LocalContactCenterMock>();
            var source = new HttpReservationSource(builder.Configuration["CCAI_SOURCE_SERVICE_KEY"] ?? "");
            builder.Services.AddSingleton<IReservationSource>(source); builder.Services.AddSingleton<ISourceCommandPort>(source);
            builder.Services.AddSingleton(new TransactionGate(builder.Configuration["CCAI_TRANSACTIONS_DISABLED"] == "1"));
            builder.Services.AddSingleton<CancellationWorkflow>(); builder.Services.AddSingleton<CommandDispatcher>();
            builder.Services.AddSingleton<AssistantProcessor>(); builder.Services.AddHostedService<DemoAssistantWorker>();
            builder.Services.AddHostedService<DemoCommandWorker>();
            if(embedding is not null)builder.Services.AddHostedService<DemoKnowledgeWorker>();
        }
        builder.Logging.ClearProviders(); // Synthetic slice exports codes, never arbitrary HTTP/OIDC bodies or exception logs.
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
        builder.Services.AddProblemDetails(); builder.Services.AddContactCenterInfrastructure();
        builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        builder.Services.AddDataProtection().SetApplicationName(localDemo ? "ContactCenterAI.Demo" : "ContactCenterAI.Local")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, ".local", localDemo ? "demo-keys" : "product-keys")));
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN"; options.Cookie.Name = localDemo ? "ccai.demo.csrf" : "__Host-ccai.csrf";
            options.Cookie.SecurePolicy = securePolicy; options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict; options.Cookie.Path = "/";
        });
        var authentication = builder.Services.AddAuthentication(options =>
        { options.DefaultAuthenticateScheme = CookieScheme; options.DefaultSignInScheme = CookieScheme; })
        .AddCookie(CookieScheme, options =>
        {
            options.Cookie.Name = localDemo ? "ccai.demo.session" : "__Host-ccai.session"; options.Cookie.SecurePolicy = securePolicy;
            options.Cookie.HttpOnly = true; options.Cookie.SameSite = SameSiteMode.Lax; options.Cookie.Path = "/";
            options.ExpireTimeSpan = TimeSpan.FromMinutes(15); options.SlidingExpiration = false;
            options.Events.OnValidatePrincipal = async context =>
            {
                if (context.HttpContext.Request.Path.StartsWithSegments("/health"))
                { context.RejectPrincipal(); return; }
                var store = context.HttpContext.RequestServices.GetRequiredService<IOperationalStore>();
                var clock = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>();
                var actor = Guid.TryParse(context.Principal?.FindFirstValue(SessionClaim), out var id)
                    ? await store.FindSessionAsync(id, clock.GetUtcNow(), context.HttpContext.RequestAborted) : null;
                if (actor is null) { context.RejectPrincipal(); return; }
                context.HttpContext.Items["Actor"] = actor;
            };
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        var secret = builder.Configuration["CCAI_BFF_CLIENT_SECRET"];
        if (!string.IsNullOrWhiteSpace(secret)) authentication.AddOpenIdConnect(OidcScheme, options =>
        {
            options.Authority = Issuer; options.RequireHttpsMetadata = false; // Fixed loopback IdP only, ADR-011.
            options.ClientId = "contactcenterai-bff"; options.ClientSecret = secret;
            options.ResponseType = OpenIdConnectResponseType.Code; options.UsePkce = true;
            options.SaveTokens = false; options.MapInboundClaims = false; options.SignInScheme = CookieScheme;
            options.CallbackPath = "/signin-oidc"; options.Scope.Clear(); options.Scope.Add("openid");
            if (localDemo)
            {
                options.ResponseMode = OpenIdConnectResponseMode.Query;
                options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.None;
                options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                options.NonceCookie.SecurePolicy = CookieSecurePolicy.None;
                options.NonceCookie.SameSite = SameSiteMode.Lax;
            }
            options.Events.OnRedirectToIdentityProvider = context =>
            {
                context.ProtocolMessage.RedirectUri = origin + "/signin-oidc";
                if (localDemo) context.ProtocolMessage.Prompt = "login";
                return Task.CompletedTask;
            };
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = Issuer, ValidAudience = "contactcenterai-bff", ValidateIssuer = true, ValidateAudience = true,
                ValidateLifetime = true, RequireSignedTokens = true, ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                NameClaimType = "sub", RoleClaimType = "ccai_role", ClockSkew = TimeSpan.FromSeconds(30)
            };
            options.Events.OnTicketReceived = async context =>
            {
                var http = context.HttpContext; var store = http.RequestServices.GetRequiredService<IOperationalStore>();
                var clock = http.RequestServices.GetRequiredService<TimeProvider>();
                var oldTicket = await http.AuthenticateAsync(CookieScheme);
                Guid? previous = Guid.TryParse(oldTicket.Principal?.FindFirstValue(SessionClaim), out var oldId) ? oldId : null;
                var subject = context.Principal?.FindFirstValue("sub");
                if (string.IsNullOrEmpty(subject)) throw new RequestRejected(403, "BINDING_REQUIRED");
                var roles = ActorRoles.None;
                foreach (var role in context.Principal!.FindAll("ccai_role"))
                    if (Enum.TryParse<ActorRoles>(role.Value, out var parsed) && Enum.IsDefined(parsed)) roles |= parsed;
                var actor = await store.LoginAsync(Issuer, subject, roles, previous, clock.GetUtcNow(), http.RequestAborted);
                context.Principal = Principal(actor); context.Properties!.ExpiresUtc = actor.ExpiresAt;
                context.Properties.IsPersistent = false; context.ReturnUri = localDemo ? (context.Properties.Items.TryGetValue("resort-return", out var resortReturn) && resortReturn == "1" ? "/resort.html" : "/") : "/v1/session";
            };
            options.Events.OnRemoteFailure = async context =>
            {
                context.HandleResponse();
                await Problem(context.HttpContext, context.Failure is OperationalUnavailable ? 503 : 401,
                    context.Failure is OperationalUnavailable ? "OPERATIONAL_STORE_UNAVAILABLE" : "LOGIN_FAILED");
            };
        });
        if (localDemo && builder.Configuration["CCAI_RESORT_ENABLED"] == "true")
        {
            if ((builder.Configuration["CCAI_RESORT_BRIDGE_KEY"] ?? "").Length < 32) throw new RequestRejected(503, "RESORT_BRIDGE_CONFIG_REQUIRED");
            builder.Services.AddSingleton(sp => new ResortStore(Path.Combine(builder.Environment.ContentRootPath, ".local", "resort", "resort.db"), sp.GetRequiredService<TimeProvider>()));
            builder.Services.AddHostedService<ResortRecoveryWorker>();
        }
        var app = builder.Build();
        if(traceExporter is not null)app.Lifetime.ApplicationStopped.Register(traceExporter.Dispose);
        app.Use(async (http, next) =>
        {
            using var activity=localDemo?DemoTelemetry.Activities.StartActivity("api.request"):null;
            using var recorded=activity is null?null:new RecordStatus(activity,http);
            http.Response.Headers.CacheControl = "no-store"; http.Response.Headers.XContentTypeOptions = "nosniff";
            http.Response.Headers.ContentSecurityPolicy = localDemo
                ? "default-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self' http://localhost:8080"
                : "default-src 'none'; frame-ancestors 'none'";
            if (localDemo && (http.Request.Host.ToString() != "127.0.0.1:7452" ||
                http.Connection.RemoteIpAddress is { } address && !IPAddress.IsLoopback(address)))
            { await Problem(http, 403, "LOCAL_DEMO_ONLY"); return; }
            if (!localDemo && !http.Request.Path.StartsWithSegments("/health") && !http.Request.IsHttps)
            { await Problem(http, 426, "HTTPS_REQUIRED"); return; }
            try { await next(http); }
            catch (RequestRejected rejected) { await Problem(http, rejected.Status, rejected.Code); }
            catch (OperationalUnavailable) { await Problem(http, 503, "OPERATIONAL_STORE_UNAVAILABLE"); }
            catch (BadHttpRequestException) { await Problem(http, 400, "INVALID_REQUEST"); }
            catch (AntiforgeryValidationException) { await Problem(http, 403, "CSRF_REJECTED"); }
        });
        // Liveness must not depend on a cookie triggering an operational lookup.
        app.UseAuthentication();
        app.Use(async (http, next) =>
        {
            if ((http.Request.Path.StartsWithSegments("/v1") || http.Request.Path.StartsWithSegments("/v2")) && http.Request.Method is not ("GET" or "HEAD"))
            {
                if (http.Request.Headers.Origin.Count != 1 || http.Request.Headers.Origin[0] != origin)
                { await Problem(http, 403, "ORIGIN_REJECTED"); return; }
                await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http);
            }
            await next(http);
        });
        if (localDemo) { app.UseDefaultFiles(); app.UseStaticFiles(); }
        app.MapGet("/health/live", () => Results.Ok(new { status = "Alive" }));
        app.MapGet("/health/ready", async (IRuntimeReadiness readiness, HttpContext http) =>
        {
            var result = await readiness.CheckAsync(http.RequestAborted);
            if (localDemo && result.IsReady) return (IResult)Results.Ok(new { status = "Ready", profile = "demo-assistant-v0.10", modelProvider = http.RequestServices.GetRequiredService<IIntentProvider>().ProviderId });
            return result.IsReady ? Results.Ok(new { status = "Ready" }) : ProblemResult(http, 503, result.Code);
        });
        app.MapGet("/v1/session/csrf", (HttpContext http, IAntiforgery csrf) => Results.Ok(new { token = csrf.GetAndStoreTokens(http).RequestToken }));
        if (localDemo) app.MapGet("/v1/demo/profile", (IIntentProvider provider) => Results.Ok(new { mode = provider is OllamaIntentProvider ? "local-llm" : "simulated", providerId = provider.ProviderId, factualAnswers = "governed-extracts-and-source-templates", contactCenter = "mock",retrieval=builder.Configuration["CCAI_RETRIEVAL_PROVIDER"]=="semantic"?"semantic-reranked-local":"topic-fixture" }));
        if(localDemo) app.MapGet("/v1/operations/status",async(HttpContext http,SqliteOperationalStore store,TimeProvider clock)=>
            Results.Ok(new{operational=await store.OperationsAsync(RequireActor(http),clock.GetUtcNow(),http.RequestAborted),telemetry=traceExporter!.Snapshot()}));
        app.MapGet("/v1/session", (HttpContext http) =>
        {
            if (http.Items["Actor"] is not Actor actor) return ProblemResult(http, 401, "SESSION_REQUIRED");
            if (localDemo) return Results.Ok(new { authenticated = actor.PrincipalId is not null, expiresAt = actor.ExpiresAt,
                isEmployee = (actor.Roles & (ActorRoles.Agent | ActorRoles.Supervisor)) != 0,
                isOperationsAdmin=actor.Roles.HasFlag(ActorRoles.OperationsAdmin),
                displayName = actor.MemberRef switch { "MEM-001" => "Cliente A", "MEM-002" => "Cliente B", _ => actor.Roles.HasFlag(ActorRoles.Agent) ? "Agente demo" : "Empleado demo" } });
            return Results.Ok(new { authenticated = actor.PrincipalId is not null, expiresAt = actor.ExpiresAt });
        });
        app.MapPost("/v1/session/anonymous", async (HttpContext http, IOperationalStore store, TimeProvider clock) =>
        {
            if (http.Items["Actor"] is Actor existing) return Results.Ok(new { authenticated = existing.PrincipalId is not null, expiresAt = existing.ExpiresAt });
            var actor = await store.CreateGuestAsync(clock.GetUtcNow(), http.RequestAborted);
            await http.SignInAsync(CookieScheme, Principal(actor), new AuthenticationProperties { ExpiresUtc = actor.ExpiresAt, IsPersistent = false });
            return Results.Created("/v1/session", new { authenticated = false, expiresAt = actor.ExpiresAt });
        });
        app.MapPost("/v1/session/login", async (HttpContext http, IAuthenticationSchemeProvider schemes) =>
        {
            if (await schemes.GetSchemeAsync(OidcScheme) is null) return ProblemResult(http, 503, "IDENTITY_NOT_CONFIGURED");
            var properties = new AuthenticationProperties { RedirectUri = localDemo ? "/" : "/v1/session" };
            if (localDemo && http.Request.Query["resort"] == "1") properties.Items["resort-return"] = "1";
            return Results.Challenge(properties, [OidcScheme]);
        });
        app.MapPost("/v1/session/logout", async (HttpContext http, IOperationalStore store, TimeProvider clock) =>
        {
            var actor = RequireActor(http); await store.RevokeSessionAsync(actor.SessionId, clock.GetUtcNow(), http.RequestAborted);
            await http.SignOutAsync(CookieScheme); return Results.NoContent();
        });
        app.MapPost("/v1/conversations", async (CreateConversation request, HttpContext http, ConversationIngress ingress) =>
        {
            var key = http.Request.Headers["Idempotency-Key"].Count == 1 ? http.Request.Headers["Idempotency-Key"].ToString() : "";
            var result = await ingress.CreateAsync(RequireActor(http), request.Language, key, http.RequestAborted);
            http.Response.Headers.ETag = $"\"{result.Version}\"";
            return Results.Created($"/v1/conversations/{result.ConversationId}", result);
        });
        app.MapGet("/v1/conversations/{id:guid}", async (Guid id, HttpContext http, IOperationalStore store, TimeProvider clock) =>
        {
            var result = await store.ReadConversationAsync(RequireActor(http), id, clock.GetUtcNow(), http.RequestAborted)
                ?? throw new RequestRejected(404, "RESOURCE_NOT_FOUND");
            http.Response.Headers.ETag = $"\"{result.Resource.Version}\"";
            return Results.Ok(new { conversationId = id, version = result.Resource.Version, result.Language, messages = result.Messages });
        });
        app.MapPost("/v1/conversations/{id:guid}/messages", async (Guid id, SubmitMessage request, HttpContext http, ConversationIngress ingress) =>
        {
            var etag = http.Request.Headers.IfMatch.ToString();
            if (etag.Length < 3 || etag[0] != '"' || etag[^1] != '"' || !long.TryParse(etag[1..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var version))
                throw new RequestRejected(400, "IF_MATCH_REQUIRED");
            var result = await ingress.SubmitAsync(RequireActor(http), id, request.ClientMessageId, request.Text, version, http.RequestAborted);
            http.Response.Headers.ETag = $"\"{result.Version}\"";
            return Results.Accepted(value: result);
        });
        if (localDemo) app.MapGet("/v1/conversations/{id:guid}/reservations", async (Guid id, HttpContext http,
            IOperationalStore store, IReservationSource source, TimeProvider clock) =>
        {
            var actor = RequireActor(http);
            _ = await store.ReadConversationAsync(actor, id, clock.GetUtcNow(), http.RequestAborted)
                ?? throw new RequestRejected(404, "RESOURCE_NOT_FOUND");
            if (actor.PrincipalId is null || actor.MemberRef is null || !actor.Roles.HasFlag(ActorRoles.Customer))
                throw new RequestRejected(403, "VERIFIED_CUSTOMER_REQUIRED");
            var reservations = await source.ListAsync(actor.MemberRef, http.RequestAborted);
            var now = clock.GetUtcNow();
            return Results.Ok(reservations.Select(item => new ReservationView(item.ReservationId, item.PropertyName,
                item.CheckInUtc, item.Status.ToString(), item.Version, CancellationPolicy.Evaluate(item.Status, item.CheckInUtc, now))));
        });
        if (localDemo)
        {
            app.MapGet("/v1/conversations/{id:guid}/assistant", async (Guid id, HttpContext http, IAssistantLedger store, TimeProvider clock) =>
                Results.Ok(await store.ReadAnswersAsync(RequireActor(http), id, clock.GetUtcNow(), http.RequestAborted)));
            app.MapPost("/v1/conversations/{id:guid}/handoff", async (Guid id, HttpContext http, IHandoffStore store, TimeProvider clock) =>
                Results.Accepted(value: await store.RequestAsync(RequireActor(http), id, null, "CUSTOMER_REQUEST", clock.GetUtcNow(), http.RequestAborted)));
            app.MapGet("/v1/conversations/{id:guid}/citations/{document}/{version:int}/{section}", async (Guid id, string document, int version, string section, HttpContext http, IKnowledgeStore store, TimeProvider clock) =>
                Results.Ok(await store.ResolveAsync(RequireActor(http), id, document, version, section, clock.GetUtcNow(), http.RequestAborted)
                    ?? throw new RequestRejected(404, "RESOURCE_NOT_FOUND")));
            app.MapPost("/v1/conversations/{id:guid}/feedback", async (Guid id, AnswerFeedback request, HttpContext http, IKnowledgeStore store, TimeProvider clock) =>
            { await store.RecordFeedbackAsync(RequireActor(http), id, request.TurnId, request.Reason, clock.GetUtcNow(), http.RequestAborted); return Results.Accepted(); });
            app.MapGet("/v1/agent/conversations", async (HttpContext http, SqliteOperationalStore store, TimeProvider clock) =>
                Results.Ok(await store.AssignedAsync(RequireActor(http), clock.GetUtcNow(), http.RequestAborted)));
            app.MapGet("/v1/agent/conversations/{id:guid}/context", async (Guid id, HttpContext http, SqliteOperationalStore store, TimeProvider clock) =>
                Results.Ok(await store.ContextAsync(RequireActor(http), id, clock.GetUtcNow(), http.RequestAborted)));
            app.MapPost("/v1/knowledge/drafts", async (KnowledgeDraft request, HttpContext http, SqliteOperationalStore store, TimeProvider clock) =>
            { await store.CreateDraftAsync(RequireActor(http), request, clock.GetUtcNow(), http.RequestAborted); return Results.Created("/v1/knowledge", new {status="PendingReview"}); });
            app.MapPost("/v1/knowledge/{document}/versions/{version:int}/publish", async (string document, int version, HttpContext http, SqliteOperationalStore store, TimeProvider clock) =>
            { await store.PublishAsync(RequireActor(http), document, version, clock.GetUtcNow(), http.RequestAborted); return Results.NoContent(); });
            app.MapPost("/v1/knowledge/{document}/versions/{version:int}/revoke", async (string document, int version, HttpContext http, SqliteOperationalStore store, TimeProvider clock) =>
            { await store.RevokeKnowledgeAsync(RequireActor(http), document, version, clock.GetUtcNow(), http.RequestAborted); return Results.NoContent(); });
            app.MapPost("/v1/conversations/{id:guid}/cancellation-offers", async (Guid id, PreviewCancellation request, HttpContext http, CancellationWorkflow workflow) =>
            {
                var offer = await workflow.PreviewAsync(RequireActor(http), id, request.ReservationId, ExpectedVersion(http), http.RequestAborted);
                http.Response.Headers.ETag = $"\"{offer.Version}\"";
                return Results.Created($"/v1/conversations/{id}/actions", offer);
            });
            app.MapPost("/v1/conversations/{id:guid}/confirmations", async (Guid id, ConfirmCancellation request, HttpContext http, CancellationWorkflow workflow) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].Count == 1 ? http.Request.Headers["Idempotency-Key"].ToString() : "";
                var receipt = await workflow.ConfirmAsync(RequireActor(http), id, request.OfferId, request.Decision, ExpectedVersion(http), key, http.RequestAborted);
                http.Response.Headers.ETag = $"\"{receipt.Version}\"";
                return receipt.OperationId is null ? Results.Ok(receipt) : Results.Accepted($"/v1/operations/{receipt.OperationId}", receipt);
            });
            app.MapGet("/v1/conversations/{id:guid}/actions", async (Guid id, HttpContext http, ICancellationStore store, TimeProvider clock) =>
                Results.Ok(await store.ActionsAsync(RequireActor(http), id, clock.GetUtcNow(), http.RequestAborted)));
            app.MapGet("/v1/operations/{id:guid}", async (Guid id, HttpContext http, ICancellationStore store, TimeProvider clock) =>
                Results.Ok(await store.OperationAsync(RequireActor(http), id, clock.GetUtcNow(), http.RequestAborted)));
        }
        if (localDemo && builder.Configuration["CCAI_RESORT_ENABLED"] == "true")
        {
            var resort = app.Services.GetRequiredService<ResortStore>();
            ResortApi.Map(app, resort, builder.Configuration["CCAI_RESORT_BRIDGE_KEY"] ?? "", builder.Configuration["CCAI_RESORT_META_ENDPOINT_ID"] ?? "");
        }
        return app;
    }
    private static ClaimsPrincipal Principal(Actor actor) => new(new ClaimsIdentity(
        [new Claim(SessionClaim, actor.SessionId.ToString()), new Claim(ClaimTypes.NameIdentifier, actor.SessionId.ToString())], CookieScheme));
    private static Actor RequireActor(HttpContext http) => http.Items["Actor"] as Actor ?? throw new RequestRejected(401, "SESSION_REQUIRED");
    private sealed class RecordStatus(System.Diagnostics.Activity activity,HttpContext http):IDisposable
    {public void Dispose()=>activity.SetTag("http.status",http.Response.StatusCode);}
    private static long ExpectedVersion(HttpContext http)
    {
        var value = http.Request.Headers.IfMatch.ToString();
        if (value.Length < 3 || value[0] != '"' || value[^1] != '"' || !long.TryParse(value[1..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var version) || version < 1)
            throw new RequestRejected(400, "IF_MATCH_REQUIRED");
        return version;
    }
    private static IResult ProblemResult(HttpContext http, int status, string code) => Results.Problem(statusCode: status,
        title: code, type: "urn:contactcenterai:problem:" + code.ToLowerInvariant().Replace('_', '-'),
        extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = http.TraceIdentifier, ["retryable"] = status == 503 });
    private static Task Problem(HttpContext http, int status, string code) => ProblemResult(http, status, code).ExecuteAsync(http);
}
public sealed record CreateConversation(string Language);
public sealed record SubmitMessage(Guid ClientMessageId, string Text);
public sealed record PreviewCancellation(string ReservationId);
public sealed record ConfirmCancellation(Guid OfferId, string Decision);
public sealed record AnswerFeedback(Guid TurnId, string Reason);
