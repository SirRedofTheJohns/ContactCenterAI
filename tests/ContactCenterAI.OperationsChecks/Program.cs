using System.Text.Json;
using ContactCenterAI.Application;
using ContactCenterAI.Domain;
using ContactCenterAI.Infrastructure;

var repo=Path.GetFullPath(args.Single());var checks=new List<string>();
void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks.Add(name);Console.WriteLine("PASS: "+name);}
async Task Denied(Func<Task> action,string code,string name){try{await action();throw new InvalidOperationException(name);}catch(RequestRejected error){Check(error.Code==code,name);}}
var root=Path.Combine(repo,".local","operations-checks",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
var now=DateTimeOffset.UtcNow;var store=new SqliteOperationalStore(Path.Combine(root,"operations.db"));
const string issuer="http://localhost:8080/realms/contactcenterai-local";
var customer=await store.LoginAsync(issuer,"10000000-0000-0000-0000-000000000001",ActorRoles.Customer,null,now,default);
var admin=await store.LoginAsync(issuer,"10000000-0000-0000-0000-000000000008",ActorRoles.OperationsAdmin,null,now,default);
await Denied(()=>store.OperationsAsync(customer,now,default),"OPERATIONS_ADMIN_REQUIRED","Customer cannot read operational aggregate panel");
await Denied(()=>store.OperationsAsync(customer with{Roles=ActorRoles.OperationsAdmin},now,default),"OPERATIONS_ADMIN_REQUIRED","Supplied role cannot grant operational access");
var status=await store.OperationsAsync(admin,now,default);
Check(status.KnowledgeVariants==80&&!status.SemanticEnabled&&status.IndexedVariants==0,"Panel reflects actual registry and explicit non-semantic fixture mode");
Check(status.Commands.Completed==0&&status.Commands.Unknown==0&&status.Alerts.Count==0,"Fresh fixture does not invent completed operations or alerts");
await store.RevokeSessionAsync(admin.SessionId,now,default);await Denied(()=>store.OperationsAsync(admin,now,default),"SESSION_REQUIRED","Revoked admin cannot read panel");
using(var exporter=new LocalTraceExporter(Path.Combine(root,"trace")))
{
    using(var parent=DemoTelemetry.Activities.StartActivity("api.request"))
    {parent!.SetTag("http.status",403);parent.SetTag("request.body","PRIVATE-SENTINEL");parent.SetTag("session.id","PRIVATE-SENTINEL");using var child=DemoTelemetry.Activities.StartActivity("knowledge.search");child!.SetTag("language","es");child.SetTag("text","PRIVATE-SENTINEL");}
    var traces=File.ReadAllLines(Path.Combine(root,"trace/traces.ndjson"));var safe=string.Join('\n',traces);
    Check(traces.Length==2&&!safe.Contains("PRIVATE-SENTINEL")&&!safe.Contains("session.id")&&!safe.Contains("request.body"),"Trace exporter rejects arbitrary attributes and content");
    using var doc=JsonDocument.Parse(traces[0]);using var doc2=JsonDocument.Parse(traces[1]);
    Check(doc.RootElement.GetProperty("traceId").GetString()==doc2.RootElement.GetProperty("traceId").GetString()&&doc.RootElement.GetProperty("parentSpanId").GetString()==doc2.RootElement.GetProperty("spanId").GetString(),"Nested activities preserve W3C trace and parent relation");
    for(var i=0;i<80;i++){using var trace=DemoTelemetry.Activities.StartActivity("api.request");trace!.SetTag("http.status",200);}
    using var snap=JsonDocument.Parse(JsonSerializer.Serialize(exporter.Snapshot()));Check(snap.RootElement.GetProperty("recent").GetArrayLength()==20,"Dashboard recent activity has a bounded window");
    using(var ignored=DemoTelemetry.Activities.StartActivity("untrusted-operation-name"))ignored!.SetTag("text","PRIVATE-SENTINEL");
    Check(File.ReadAllLines(Path.Combine(root,"trace/traces.ndjson")).Length==82,"Unlisted activity names are not exported");
}
File.WriteAllText(Path.Combine(repo,"docs/progress/demo-v0.7-operations-checks.json"),JsonSerializer.Serialize(new{scope="real local aggregates/auth and trace exporter fixtures",passed=checks.Count,checks},new JsonSerializerOptions{WriteIndented=true}));
