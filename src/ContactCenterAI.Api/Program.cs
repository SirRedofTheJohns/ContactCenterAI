using ContactCenterAI.Api;

var localDemo = args.Contains("--demo-local", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args.Where(value => value != "--demo-local").ToArray(), EnvironmentName = Environments.Production,
    WebRootPath = localDemo ? Path.Combine(Directory.GetCurrentDirectory(), "src", "ContactCenterAI.Api", "wwwroot") : null
});
// Corporate routes require HTTPS; ADR-014 limits the separate demo profile to loopback.
builder.WebHost.UseUrls(localDemo ? "http://127.0.0.1:7452" : "http://127.0.0.1:7450");
var certificatePath = builder.Configuration["CCAI_TLS_CERTIFICATE_PATH"];
if (!localDemo && !string.IsNullOrEmpty(certificatePath))
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Listen(System.Net.IPAddress.Loopback, 7450);
        options.ListenLocalhost(7451, listener => listener.UseHttps(certificatePath, builder.Configuration["CCAI_TLS_CERTIFICATE_PASSWORD"]));
    });
await using var app = ProductApi.Build(builder, localDemo);
await app.RunAsync();
