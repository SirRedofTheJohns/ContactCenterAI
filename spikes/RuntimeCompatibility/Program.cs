using System.IdentityModel.Tokens.Jwt;
using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

// B01 diagnostic only. No product endpoints or business state.
CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US"); // Stable diagnostic codes/marker matching only.
var managedNetworkDiagnostic = args.SequenceEqual(["--live-managed"]);
if (managedNetworkDiagnostic)
    AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);
const string issuer = "http://localhost:8080/realms/contactcenterai-local";
const string audience = "contactcenterai-bff";
const string subject = "10000000-0000-0000-0000-000000000001";
int passed = 0;
void Check(string name, bool condition)
{
    if (!condition) throw new InvalidOperationException($"FAIL: {name}");
    passed++;
    Console.WriteLine($"PASS: {name}");
}

Check("runtime .NET 10", Environment.Version.Major == 10);
var sqlOptions = new SqlConnectionStringBuilder
{
    DataSource = "tcp:127.0.0.1,14333", InitialCatalog = "master", Encrypt = true,
    TrustServerCertificate = true, ConnectTimeout = 10
};
Check("SQL driver loads; encryption enabled", sqlOptions.Encrypt == SqlConnectionEncryptOption.Mandatory);

var services = new ServiceCollection();
services.AddLogging();
services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.HttpOnly = true;
        o.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
        o.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    })
    .AddOpenIdConnect(o =>
    {
        o.Authority = issuer;
        o.RequireHttpsMetadata = false; // Fixed loopback issuer in this diagnostic only.
        o.ClientId = audience;
        o.ClientSecret = "diagnostic-only-never-used-for-login";
        o.ResponseType = OpenIdConnectResponseType.Code;
        o.UsePkce = true;
        o.SaveTokens = false;
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = issuer,
            ValidateAudience = true, ValidAudience = audience,
            ValidateLifetime = true, RequireExpirationTime = true,
            RequireSignedTokens = true, ValidateIssuerSigningKey = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            RoleClaimType = "ccai_role", NameClaimType = "sub", ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
using var provider = services.BuildServiceProvider();
var oidc = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(OpenIdConnectDefaults.AuthenticationScheme);
var cookie = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);
Check("OIDC authorization code + PKCE", oidc.ResponseType == "code" && oidc.UsePkce);
Check("tokens excluded from authentication cookie", !oidc.SaveTokens);
Check("cookie HttpOnly, Secure, SameSite Lax", cookie.Cookie.HttpOnly && cookie.Cookie.SecurePolicy == Microsoft.AspNetCore.Http.CookieSecurePolicy.Always && cookie.Cookie.SameSite == Microsoft.AspNetCore.Http.SameSiteMode.Lax);
Check("explicit subject and trusted role claim mapping", !oidc.MapInboundClaims && oidc.TokenValidationParameters.RoleClaimType == "ccai_role" && oidc.TokenValidationParameters.NameClaimType == "sub");

using var rsa = RSA.Create(2048);
using var untrustedRsa = RSA.Create(2048);
var signingKey = new RsaSecurityKey(rsa) { KeyId = "b01-ephemeral-key" };
var validator = new JwtSecurityTokenHandler { MapInboundClaims = false };
var validation = oidc.TokenValidationParameters.Clone();
validation.IssuerSigningKey = signingKey;
var now = DateTime.UtcNow;
string Token(string tokenIssuer, string tokenAudience, DateTime expires, SecurityKey? key)
{
    var jwt = new JwtSecurityToken(tokenIssuer, tokenAudience,
        [new Claim("sub", subject), new Claim("ccai_role", "Customer")],
        now.AddMinutes(-30), expires, key is null ? null : new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
    return validator.WriteToken(jwt);
}
var principal = validator.ValidateToken(Token(issuer, audience, now.AddMinutes(5), signingKey), validation, out _);
Check("valid RS256 token preserves subject and Customer role", principal.FindFirstValue("sub") == subject && principal.IsInRole("Customer"));
void Denies(string name, string token)
{
    bool denied = false;
    try { validator.ValidateToken(token, validation, out _); }
    catch (SecurityTokenException) { denied = true; }
    Check(name, denied);
}
Denies("wrong issuer rejected", Token("https://untrusted.example.invalid", audience, now.AddMinutes(5), signingKey));
Denies("wrong audience rejected", Token(issuer, "another-client", now.AddMinutes(5), signingKey));
Denies("expired token rejected", Token(issuer, audience, now.AddMinutes(-5), signingKey));
Denies("unsigned token rejected", Token(issuer, audience, now.AddMinutes(5), null));
Denies("untrusted signing key rejected", Token(issuer, audience, now.AddMinutes(5), new RsaSecurityKey(untrustedRsa)));

Console.WriteLine($"Offline compatibility checks: {passed} passed. These are library/configuration checks, not product authorization tests.");
if (args.SequenceEqual(["--offline"])) return 0;
if (args.SequenceEqual(["--serve-login"]))
{
    var clientSecret = Environment.GetEnvironmentVariable("CCAI_BFF_CLIENT_SECRET");
    if (string.IsNullOrWhiteSpace(clientSecret) || clientSecret.Length < 32)
    {
        Console.WriteLine("BLOCKED: local diagnostic client secret is missing.");
        return 2;
    }
    X509Certificate2 certificate;
    try { certificate = DiagnosticCertificate.Create(); }
    catch (Exception error) when (error is CryptographicException or UnauthorizedAccessException or IOException)
    {
        for (var current = error; current is not null; current = current.InnerException)
            Console.WriteLine($"TLS_CERTIFICATE Type={current.GetType().Name}; HResult=0x{current.HResult:X8}");
        return 2;
    }
    using var tlsCertificate = certificate;
    Console.WriteLine($"TLS_KEY Mode={(OperatingSystem.IsWindows() ? "TemporaryWindowsUserKeySet" : "EphemeralKeySet")}; HasPrivateKey={tlsCertificate.HasPrivateKey}");
    var publicCertificatePath = Path.Combine(Directory.GetCurrentDirectory(), ".local", "diagnostic-certs", "localhost.cer");
    Directory.CreateDirectory(Path.GetDirectoryName(publicCertificatePath)!);
    await File.WriteAllBytesAsync(publicCertificatePath, tlsCertificate.Export(X509ContentType.Cert));
    var loginRun = Environment.GetEnvironmentVariable("CCAI_B01_LOGIN_RUN_ID") ?? Guid.NewGuid().ToString("D");
    if (!Guid.TryParseExact(loginRun, "D", out _)) return 2;
    var loginEvidence = new LoginEvidence(loginRun, Path.Combine(Directory.GetCurrentDirectory(), ".local", "b01-login"));
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
    builder.WebHost.ConfigureKestrel(server => server.ListenLocalhost(7443, listen => listen.UseHttps(tlsCertificate)));
    builder.Logging.ClearProviders(); // No request bodies, claims, credentials or tokens in diagnostic logs.
    builder.Logging.AddProvider(new TlsDiagnosticLogProvider()).SetMinimumLevel(LogLevel.Debug);
    builder.Services.AddDataProtection().SetApplicationName("ContactCenterAI.B01.Diagnostic")
        .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Directory.GetCurrentDirectory(), ".local", "diagnostic-keys")));
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(o =>
        {
            o.Cookie.Name = "ccai.b01.diagnostic";
            o.Cookie.HttpOnly = true;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            o.Cookie.SameSite = SameSiteMode.Lax;
            o.ExpireTimeSpan = TimeSpan.FromMinutes(10);
            o.SlidingExpiration = false;
            o.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            o.Events.OnSigningIn = context =>
            {
                context.HttpContext.Items["b01.cookie-account"] = LoginEvidence.Property(context.Properties, "b01.account");
                return Task.CompletedTask;
            };
        })
        .AddOpenIdConnect(o =>
        {
            o.Authority = issuer;
            o.RequireHttpsMetadata = false;
            o.ClientId = audience;
            o.ClientSecret = clientSecret;
            o.ResponseType = oidc.ResponseType;
            o.UsePkce = oidc.UsePkce;
            o.SaveTokens = false;
            o.MapInboundClaims = false;
            o.Scope.Clear();
            o.Scope.Add("openid");
            o.TokenValidationParameters = oidc.TokenValidationParameters.Clone();
            o.Events.OnRedirectToIdentityProvider = context =>
            {
                var account = LoginEvidence.Property(context.Properties, "b01.account");
                if (LoginEvidence.ExpectedSubject(account) is null) throw new InvalidOperationException("B01_ACCOUNT_INVALID");
                context.ProtocolMessage.LoginHint = account;
                context.ProtocolMessage.Prompt = "login";
                loginEvidence.Challenge(account!, context.ProtocolMessage.ResponseType == "code",
                    context.ProtocolMessage.GetParameter("code_challenge_method") == "S256" && !string.IsNullOrEmpty(context.ProtocolMessage.GetParameter("code_challenge")),
                    context.ProtocolMessage.RedirectUri == "https://localhost:7443/signin-oidc");
                return Task.CompletedTask;
            };
            o.Events.OnAuthorizationCodeReceived = context =>
            {
                loginEvidence.CodeReceived(LoginEvidence.Property(context.Properties, "b01.account"),
                    !string.IsNullOrEmpty(context.ProtocolMessage.Code), context.Request.Path == "/signin-oidc");
                return Task.CompletedTask;
            };
            o.Events.OnTokenValidated = context =>
            {
                var account = LoginEvidence.Property(context.Properties, "b01.account");
                if (LoginEvidence.Property(context.Properties, "b01.run") != loginRun ||
                    !loginEvidence.Validated(account, context.Principal, context.SecurityToken.Issuer == issuer))
                    context.Fail("B01_EXPECTED_ACCOUNT_MISMATCH");
                return Task.CompletedTask;
            };
            o.Events.OnRemoteFailure = async context =>
            {
                loginEvidence.Failure(LoginEvidence.Property(context.Properties, "b01.account"));
                context.HandleResponse();
                context.Response.StatusCode = 400;
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync("<h1>B01: el login no pasó</h1><p>Vuelve a la terminal. No compartas contraseñas, tokens ni la dirección completa de Keycloak.</p>");
            };
        });
    builder.Services.AddAuthorization();
    builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(5));
    await using var app = builder.Build();
    app.Use(async (context, next) =>
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; base-uri 'none'; frame-ancestors 'none'";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.OnStarting(() =>
        {
            if (context.Items.TryGetValue("b01.cookie-account", out var account))
                loginEvidence.Cookie(account as string, context.Response.Headers.SetCookie);
            return Task.CompletedTask;
        });
        await next(context);
    });
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapGet("/", () => Results.Content("<h1>B01: prueba de acceso</h1><p>Inicia sesión con una cuenta sintética:</p><a href='/login?account=customer-a'>customer-a</a><br><a href='/login?account=customer-b'>customer-b</a><p>Solo se verifica el login. Este diagnóstico no gestiona reservas.</p>", "text/html; charset=utf-8"));
    app.MapGet("/login", (string? account) =>
    {
        account ??= "customer-a";
        if (LoginEvidence.ExpectedSubject(account) is null) return Results.BadRequest();
        var properties = new AuthenticationProperties { RedirectUri = "/proof" };
        properties.Items["b01.account"] = account;
        properties.Items["b01.run"] = loginRun;
        return Results.Challenge(properties, [OpenIdConnectDefaults.AuthenticationScheme]);
    });
    app.MapGet("/proof", async (HttpContext context) =>
    {
        var session = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var account = LoginEvidence.Property(session.Properties, "b01.account");
        if (LoginEvidence.Property(session.Properties, "b01.run") != loginRun || session.Principal is null || session.Properties is null) return Results.Unauthorized();
        var complete = loginEvidence.Proof(account, session.Principal, !session.Properties.GetTokens().Any());
        return Results.Content(complete
            ? $"<h1>PASS: {account}</h1><p>Identidad, rol Customer, Code + PKCE y cookie HTTPS verificados. Vuelve a la terminal para continuar.</p>"
            : "<h1>B01: verificación incompleta</h1><p>Vuelve a la terminal. Se conserva únicamente evidencia sin secretos.</p>", "text/html; charset=utf-8");
    }).RequireAuthorization();
    Console.WriteLine("Diagnostic HTTPS listener: https://localhost:7443. Certificate: .local/diagnostic-certs/localhost.cer (new each run). See local README for temporary trust. Ctrl+C stops the diagnostic.");
    await app.StartAsync();
    loginEvidence.Ready(Environment.ProcessId, tlsCertificate.Thumbprint);
    var stopMarker = Path.Combine(Directory.GetCurrentDirectory(), ".local", "b01-login", "stop-" + loginRun);
    using var monitorCancellation = new CancellationTokenSource();
    var monitor = Task.Run(async () =>
    {
        while (!monitorCancellation.IsCancellationRequested)
        {
            if (File.Exists(stopMarker)) { app.Lifetime.StopApplication(); return; }
            await Task.Delay(250, monitorCancellation.Token);
        }
    });
    try { await app.WaitForShutdownAsync(); }
    finally
    {
        monitorCancellation.Cancel();
        try { await monitor; } catch (OperationCanceledException) { }
    }
    return 0;
}
if (!args.SequenceEqual(["--live"]) && !managedNetworkDiagnostic)
{
    Console.WriteLine("Usage: --offline, --live, --live-managed (debug only) or --serve-login. Missing services fail the gate.");
    return 2;
}

var livePhase = "SQL_CONFIGURATION";
Console.WriteLine($"SQL_NETWORKING {(managedNetworkDiagnostic ? "ManagedDiagnosticOnly" : "NativeDefault")}; Encrypt=Mandatory; Endpoint=127.0.0.1:14333");
try
{
    var password = Environment.GetEnvironmentVariable("CCAI_SQL_SA_PASSWORD");
    if (string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException("SQL_PASSWORD_MISSING");
    sqlOptions.UserID = "sa";
    sqlOptions.Password = password;
    await using var connection = new SqlConnection(sqlOptions.ConnectionString);
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    livePhase = "SQL_CONNECT";
    await connection.OpenAsync(timeout.Token);
    livePhase = "SQL_VERSION_QUERY";
    await using var command = new SqlCommand("SELECT CONVERT(varchar(128), SERVERPROPERTY('ProductVersion')), CONVERT(varchar(128), SERVERPROPERTY('Edition'))", connection);
    await using var reader = await command.ExecuteReaderAsync(timeout.Token);
    Check("live SQL Server 2022 Developer", await reader.ReadAsync(timeout.Token) && reader.GetString(0).StartsWith("16.", StringComparison.Ordinal) && reader.GetString(1).Contains("Developer", StringComparison.Ordinal));

    using var handler = new HttpClientHandler { AllowAutoRedirect = false };
    using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
    livePhase = "OIDC_DISCOVERY";
    using var discoveryResponse = await http.GetAsync(issuer + "/.well-known/openid-configuration", timeout.Token);
    Check("OIDC discovery returns HTTP 200", discoveryResponse.StatusCode == HttpStatusCode.OK);
    using var discovery = JsonDocument.Parse(await discoveryResponse.Content.ReadAsStringAsync(timeout.Token));
    var metadata = discovery.RootElement;
    Check("OIDC live issuer matches exactly", metadata.GetProperty("issuer").GetString() == issuer);
    Check("OIDC live PKCE S256 support", metadata.GetProperty("code_challenge_methods_supported").EnumerateArray().Any(e => e.GetString() == "S256"));
    var jwks = new Uri(metadata.GetProperty("jwks_uri").GetString()!);
    var expected = new Uri(issuer);
    Check("JWKS URL stays on fixed local issuer", jwks.Scheme == expected.Scheme && jwks.Host == expected.Host && jwks.Port == expected.Port && jwks.AbsolutePath.StartsWith(expected.AbsolutePath + "/", StringComparison.Ordinal));
    livePhase = "OIDC_JWKS";
    using var jwksResponse = await http.GetAsync(jwks, timeout.Token);
    Check("OIDC signing keys available", jwksResponse.StatusCode == HttpStatusCode.OK);
    using var keys = JsonDocument.Parse(await jwksResponse.Content.ReadAsStringAsync(timeout.Token));
    Check("OIDC signing keys contain RSA", keys.RootElement.GetProperty("keys").EnumerateArray().Any(k => k.GetProperty("kty").GetString() == "RSA"));
    Console.WriteLine("Live connectivity checks passed. Interactive Code+PKCE login/claim roundtrip remains required before B01 closes.");
    return 0;
}
catch (SqlException error)
{
    Console.WriteLine($"BLOCKED: {livePhase}; SqlException; HResult=0x{error.HResult:X8}");
    foreach (SqlError item in error.Errors)
        Console.WriteLine($"SQL_ERROR Number={item.Number}; State={item.State}; Class={item.Class}");
    var knownMarkers = new Dictionary<string, string>
    {
        ["does not support encryption"] = "SERVER_ENCRYPTION_NOT_NEGOTIATED",
        ["this machine does not support"] = "CLIENT_ENCRYPTION_UNAVAILABLE",
        ["encryption is not supported"] = "CLIENT_ENCRYPTION_UNAVAILABLE",
        ["pre-login handshake"] = "PRELOGIN_HANDSHAKE_FAILED",
        ["SSL Provider"] = "TLS_PROVIDER_FAILED",
        ["No credentials are available"] = "TLS_CREDENTIALS_UNAVAILABLE",
        ["certificate chain"] = "CERTIFICATE_CHAIN_FAILED",
        ["Login failed"] = "LOGIN_REJECTED",
        ["actively refused"] = "TCP_CONNECTION_REFUSED",
        ["timeout"] = "TIMEOUT"
    };
    foreach (var marker in knownMarkers)
        if (error.Message.Contains(marker.Key, StringComparison.OrdinalIgnoreCase))
            Console.WriteLine($"SQL_DIAGNOSTIC {marker.Value}");
    for (Exception? inner = error.InnerException; inner is not null; inner = inner.InnerException)
    {
        Console.WriteLine($"INNER Type={inner.GetType().Name}; HResult=0x{inner.HResult:X8}");
        if (inner is Win32Exception native)
            Console.WriteLine($"NATIVE_ERROR Code={native.NativeErrorCode}; Hex=0x{native.NativeErrorCode:X8}");
    }
    Console.WriteLine("Diagnostic fields exclude messages, passwords, connection strings and tokens.");
    return 2;
}
catch (Exception error) when (error is HttpRequestException or OperationCanceledException or InvalidOperationException or JsonException)
{
    // Do not print arbitrary exception messages: a provider may include connection details.
    Console.WriteLine($"BLOCKED: {livePhase} ({error.GetType().Name}); no credentials or tokens exported.");
    return 2;
}
