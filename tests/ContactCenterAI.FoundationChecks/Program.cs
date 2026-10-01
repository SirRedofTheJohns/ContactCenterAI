using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using ContactCenterAI.Application;
using ContactCenterAI.Domain;

try
{
if (args.Length != 3) throw new ArgumentException("Expected repository root, pinned dotnet executable and compiled API path.");
var root = Path.GetFullPath(args[0]);
var dotnet = Path.GetFullPath(args[1]);
var passed = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + label);
    Console.WriteLine("PASS: " + label);
    passed++;
}
var allowed = new Dictionary<string, string[]>
{
    ["Domain"] = [], ["Application"] = ["Domain"],
    ["Infrastructure"] = ["Domain", "Application"],
    ["Api"] = ["Application", "Infrastructure"],
    ["Worker"] = ["Application", "Infrastructure"]
};
foreach (var (project, permitted) in allowed)
{
    var path = Path.Combine(root, "src", "ContactCenterAI." + project, "ContactCenterAI." + project + ".csproj");
    var xml = XDocument.Load(path);
    var references = xml.Descendants("ProjectReference")
        .Select(node => Path.GetFileNameWithoutExtension((string?)node.Attribute("Include") ?? "")
            .Replace("ContactCenterAI.", "", StringComparison.Ordinal)).ToArray();
    Check(references.Order().SequenceEqual(permitted.Order()), project + " project dependency boundary");
    if (project is "Domain" or "Application")
        Check(!xml.Descendants("PackageReference").Any() && !xml.Descendants("FrameworkReference").Any(),
            project + " has no provider packages or ASP.NET framework dependency");
}
foreach (var assembly in new[] { typeof(ConversationLanguage).Assembly, typeof(IRuntimeReadiness).Assembly })
{
    Check(assembly.GetReferencedAssemblies().All(reference =>
        reference.Name is "ContactCenterAI.Domain" or "System.Runtime" || reference.Name?.StartsWith("System.", StringComparison.Ordinal) == true),
        assembly.GetName().Name + " compiled references contain no HTTP, EF, SQL, LLM or Genesys SDK");
}
JsonNode ReadContract(string name) => JsonNode.Parse(File.ReadAllText(Path.Combine(root, "docs", "contracts", name)))!;
void CheckReferences(JsonNode document, JsonNode? node)
{
    if (node is JsonObject obj)
    {
        if (obj["$ref"] is JsonValue reference)
        {
            var pointer = reference.GetValue<string>();
            if (!pointer.StartsWith("#/", StringComparison.Ordinal)) throw new InvalidOperationException("External contract reference not allowed.");
            JsonNode? target = document;
            foreach (var segment in pointer[2..].Split('/')) target = target?[segment.Replace("~1", "/").Replace("~0", "~")];
            if (target is null) throw new InvalidOperationException("Unresolved contract reference.");
        }
        foreach (var value in obj.Select(property => property.Value)) CheckReferences(document, value);
    }
    else if (node is JsonArray array) foreach (var value in array) CheckReferences(document, value);
}
var hostContract = ReadContract("host-v0.1.openapi.json");
var conversationContract = ReadContract("conversations-v0.1.openapi.json");
CheckReferences(hostContract, hostContract);
CheckReferences(conversationContract, conversationContract);
Check(true, "OpenAPI local references resolve (bounded structural check, not full OAS conformance)");
Check(conversationContract["x-implementation"]!.GetValue<string>() == "implemented-candidate", "Ingress contract declares candidate status without claiming live validation");
var createProperties = conversationContract["components"]!["schemas"]!["CreateConversation"]!["properties"]!.AsObject();
Check(createProperties.Count == 1 && createProperties.ContainsKey("language"), "Create payload cannot provide identity or authorization claims");

Check(!IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(endpoint => endpoint.Port == 7450),
    "Smoke port available; no pre-existing process stopped");
var start = new ProcessStartInfo(dotnet) { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
    RedirectStandardOutput = true, RedirectStandardError = true };
start.ArgumentList.Add(Path.GetFullPath(args[2]));
using var process = Process.Start(start) ?? throw new InvalidOperationException("API did not start.");
var outputDrain = process.StandardOutput.ReadToEndAsync();
var errorDrain = process.StandardError.ReadToEndAsync();
try
{
    using var handler = new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false };
    using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:7450"), Timeout = TimeSpan.FromSeconds(2) };
    var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
    var alive = false;
    while (DateTimeOffset.UtcNow < deadline && !process.HasExited)
    {
        try
        {
            using var response = await http.GetAsync("/health/live");
            alive = response.StatusCode == HttpStatusCode.OK;
            if (alive) break;
        }
        catch (HttpRequestException) { }
        await Task.Delay(100);
    }
    Check(alive, "API starts on loopback and liveness returns 200");
    using var live = await http.GetAsync("/health/live");
    var liveBody = JsonNode.Parse(await live.Content.ReadAsStringAsync())!;
    Check(liveBody["status"]!.GetValue<string>() == "Alive", "Live response matches contract");
    Check(live.Headers.CacheControl?.NoStore == true && live.Headers.GetValues("X-Content-Type-Options").Single() == "nosniff", "Health responses disable caching and content sniffing");
    using var ready = await http.GetAsync("/health/ready");
    var readyBody = JsonNode.Parse(await ready.Content.ReadAsStringAsync())!;
    Check(ready.StatusCode == HttpStatusCode.ServiceUnavailable, "No operational DB produces 503, never false readiness");
    Check(ready.Content.Headers.ContentType?.MediaType == "application/problem+json" &&
        readyBody["code"]!.GetValue<string>() == "OPERATIONAL_STORE_NOT_CONFIGURED" &&
        readyBody["status"]!.GetValue<int>() == 503 && readyBody["retryable"]!.GetValue<bool>() &&
        !string.IsNullOrEmpty(readyBody["correlationId"]!.GetValue<string>()), "Readiness emits sanitized Problem Details and correlation ID");
    Check(!readyBody.AsObject().ContainsKey("exception") && !ready.Headers.Contains("Set-Cookie"), "Health creates no identity session or exception payload");
    using var message = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
    using var business = await http.PostAsync("/v1/conversations", message);
    Check((int)business.StatusCode == 426, "HTTP business request requires HTTPS and cannot acknowledge acceptance");
}
finally
{
    if (!process.HasExited) process.Kill(entireProcessTree: true);
    await process.WaitForExitAsync();
    // Drain only; do not export arbitrary runtime logs into evidence.
    await Task.WhenAll(outputDrain, errorDrain);
}
// Windows can publish the listener table briefly after process exit. Bound
// teardown observation; never stop another process that acquires the port.
var stopDeadline = DateTimeOffset.UtcNow.AddSeconds(2);
while (DateTimeOffset.UtcNow < stopDeadline && IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(endpoint => endpoint.Port == 7450))
    await Task.Delay(50);
Check(!IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(endpoint => endpoint.Port == 7450), "Owned smoke host stopped");
Console.WriteLine($"Foundation checks: {passed} passed. These are architecture/host checks, not product authorization or AI evaluation.");
return 0;
}
catch (Exception exception)
{
    // A failed check returns a normal exit code; do not leave Windows WER holding the diagnostic DLL.
    Console.Error.WriteLine("Foundation checks failed: " + exception.GetType().Name);
    return 1;
}
