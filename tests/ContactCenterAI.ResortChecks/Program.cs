using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ContactCenterAI.Api;
using ContactCenterAI.Application;
using ContactCenterAI.Channels;
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

try
{
if (args.Length == 3 && args[1] == "--parse")
{
    var batch = File.ReadLines(args[2]).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => JsonNode.Parse(s)!).Select(c => new {
        id = c["id"]!.GetValue<string>(), parsed = ResortLanguage.Parse(c["text"]!.GetValue<string>(), DateTimeOffset.UtcNow),
        language = ResortLanguage.LanguageControl(c["text"]!.GetValue<string>()), confirmation = ResortLanguage.Confirmation(c["text"]!.GetValue<string>()) is not null,
        requiresAbstention = KnowledgeQueryScope.RequiresAbstention(c["text"]!.GetValue<string>()) });
    Console.WriteLine(JsonSerializer.Serialize(batch, new JsonSerializerOptions(JsonSerializerDefaults.Web))); return 0;
}
var repo=Path.GetFullPath(args.Single()); var root=Path.Combine(repo,".local","resort-checks",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
var checks=new List<string>();
void Check(bool v,string name){if(!v)throw new InvalidOperationException("FAIL: "+name);checks.Add(name);Console.WriteLine("PASS: "+name);}
void Deny(Action a,string code,string label){try{a();}catch(RequestRejected e){Check(e.Code==code,label);return;}throw new InvalidOperationException("FAIL: "+label);}
var now=DateTimeOffset.UtcNow;var clock=new TestClock(now);var path=Path.Combine(root,"source.db");var store=new ResortStore(path,clock);var today=store.Today();
string D(int offset)=>today.AddDays(offset).ToString("yyyy-MM-dd");
Actor A(string m="MEM-001",ActorRoles roles=ActorRoles.Customer)=>new(Guid.NewGuid(),"tenant-demo",Guid.NewGuid(),m,roles,clock.GetUtcNow().AddDays(100));
var a=A();var b=A("MEM-002");var admin=A("",ActorRoles.OperationsAdmin);
long Sql(string sql){using var db=new SqliteConnection("Data Source="+path);db.Open();using var c=db.CreateCommand();c.CommandText=sql;return (long)c.ExecuteScalar()!;}
void Raw(string sql){using var db=new SqliteConnection("Data Source="+path);db.Open();using var c=db.CreateCommand();c.CommandText=sql;c.ExecuteNonQuery();}
Check(store.Catalog().Count==3&&store.Catalog().Single(c=>c.Id=="suite").AmenitiesEn.Contains("Private jacuzzi"),"Three priced categories and Suite jacuzzi from structured source");
var json=JsonSerializer.Serialize(store.Calendar(D(0),14));Check(json.Contains("Occupied")&&json.Contains("Maintenance")&&!json.Contains("MEM-")&&!json.Contains("STAY-"),"Public calendar exposes aggregate occupancy without identities");
Check(store.MyStays(a).Single().Id=="STAY-A0000001"&&store.MyStays(b).Single().Id=="STAY-B0000001","Own stays filtered by verified member");
Deny(()=>store.Offer(b,new("cancel","STAY-A0000001"),"other"),"RESOURCE_NOT_FOUND","Member B cannot cancel member A");
Deny(()=>store.Offer(a with{Roles=ActorRoles.None},new("create",Type:"suite",Arrival:D(20),Departure:D(22)),"guest"),"VERIFIED_CUSTOMER_REQUIRED","No customer role cannot book");
Deny(()=>store.Block(a,new("standard",D(30),D(32))),"OPERATIONS_ADMIN_REQUIRED","Customer cannot change maintenance");
Deny(()=>store.Block(admin,new("standard",D(7),D(9))),"DATES_OCCUPIED","Maintenance cannot evict a confirmed stay");
var blockJson=JsonSerializer.SerializeToNode(store.Block(admin,new("deluxe",D(30),D(32))))!;
Deny(()=>store.RemoveBlock(admin,blockJson["id"]!.GetValue<string>(),2),"VERSION_CONFLICT","Stale maintenance version rejected");
store.RemoveBlock(admin,blockJson["id"]!.GetValue<string>(),1);Check(Sql("SELECT COUNT(*) FROM ResortNight WHERE Night>='"+D(30)+"'")==0,"Removing maintenance frees its nights");
foreach(var r in new[]{new ResortRequest("create",Type:"standard",Arrival:D(-1),Departure:D(1)),new("create",Type:"standard",Arrival:D(20),Departure:D(20)),new("create",Type:"standard",Arrival:D(20),Departure:D(35)),new("create",Type:"standard",Arrival:D(89),Departure:D(91)),new("create",Type:"standard",Arrival:D(20),Departure:D(22),Guests:3)})Deny(()=>store.Offer(a,r,Guid.NewGuid().ToString()),"STAY_INPUT_INVALID","Invalid date/capacity rejected: "+JsonSerializer.Serialize(r));
Check(store.Available("suite",14,2,D(89)).Length==0,"Horizon edge returns no options without range exception");
Deny(()=>store.Calendar(D(89),2),"CALENDAR_RANGE_INVALID","Calendar cannot display nights outside the bookable horizon");
Check(JsonSerializer.SerializeToNode(store.Calendar(D(89),1))!["days"]!.AsArray().Count==3,"Calendar includes the last bookable night for all three units");
var request=new ResortRequest("create",Type:"suite",Arrival:D(20),Departure:D(22),Guests:4);var offer=store.Offer(a,request,"same");
Check(offer.TotalCents==56000&&Sql("SELECT COUNT(*) FROM ResortStay")==2,"Quote computes exact minor-unit total without booking");
Check(store.Offer(a,request,"same").Id==offer.Id,"Duplicate quote uses one durable ID");
Deny(()=>store.Offer(a,request with{Departure=D(23)},"same"),"IDEMPOTENCY_CONFLICT","Changed payload under same key rejected");
Deny(()=>store.Confirm(b,offer.Id),"RESOURCE_NOT_FOUND","Offer bound to owning session and member");
Deny(()=>store.Confirm(a,offer.Id,"wrong",1),"RESOURCE_NOT_FOUND","Offer bound to route");
var done=store.Confirm(a,offer.Id);Check(done.Status=="Completed"&&done.Stay!.TotalCents==56000,"Explicit confirmation books all nights");
Check(store.Confirm(a,offer.Id)==done&&Sql("SELECT COUNT(*) FROM ResortStay")==3,"Repeated confirmation returns receipt without duplicate booking");
var original=done.Stay!;var overlap=store.Offer(a,new("change",original.Id,"suite",D(21),D(23),4),"overlap");var changed=store.Confirm(a,overlap.Id);
Check(changed.Status=="Completed"&&changed.Stay!.Version==2&&Sql("SELECT COUNT(*) FROM ResortNight WHERE Night='"+D(20)+"' AND Unit='suite'")==0,"Reschedule overlap excludes own nights and frees old-only night");
var failedOffer=store.Offer(a,new("change",original.Id,"deluxe",D(40),D(42)),"failed-change");store.Block(admin,new("deluxe",D(40),D(42)));var before=store.MyStays(a).Single(s=>s.Id==original.Id);var rejected=store.Confirm(a,failedOffer.Id);
Check(rejected.Status=="Rejected"&&rejected.ReasonCode=="DATES_OCCUPIED"&&store.MyStays(a).Single(s=>s.Id==original.Id)==before,"Occupied target at confirm preserves old row/version/nights");
var rate=store.Offer(a,new("create",Type:"standard",Arrival:D(45),Departure:D(47)),"rate");store.UpdateRate(admin,"standard",12100);
Check(store.Confirm(a,rate.Id).ReasonCode=="PRICE_CHANGED"&&Sql("SELECT COUNT(*) FROM ResortStay")==3,"Price change rejects stale quote with no source write");
var two=store.Offer(a,new("create",Type:"deluxe",Arrival:D(50),Departure:D(52)),"race-a");var three=store.Offer(b,new("create",Type:"deluxe",Arrival:D(50),Departure:D(52)),"race-b");
var raced=await Task.WhenAll(Task.Run(()=>store.Confirm(a,two.Id)),Task.Run(()=>store.Confirm(b,three.Id)));
Check(raced.Count(x=>x.Status=="Completed")==1&&raced.Count(x=>x.Status=="Rejected")==1&&Sql("SELECT COUNT(*) FROM ResortNight WHERE Unit='deluxe' AND Night>='"+D(50)+"' AND Night<'"+D(52)+"'")==2,"Two concurrent customers produce exactly one stay allocation");
var cancelled=store.Confirm(a,store.Offer(a,new("cancel",original.Id),"cancel").Id);Check(cancelled.Stay!.Status=="Cancelled"&&Sql("SELECT COUNT(*) FROM ResortNight WHERE StayId='"+original.Id+"'")==0,"Cancellation releases all nights with durable source receipt");
var expired=store.Offer(a,new("create",Type:"suite",Arrival:D(60),Departure:D(62)),"expiry");clock.Now=now.AddMinutes(6);
Deny(()=>store.Confirm(a,expired.Id),"OFFER_EXPIRED","Expired quote cannot execute");clock.Now=now;
var localArrival=today.AddDays(7).ToDateTime(new TimeOnly(15,0));var boundary=new DateTimeOffset(localArrival,TimeSpan.FromHours(-4)).AddHours(-72);clock.Now=boundary;
var cutoffActor=a with{ExpiresAt=boundary.AddDays(30)};Check(store.Offer(cutoffActor,new("cancel","STAY-A0000001"),"boundary").Request.Action=="cancel","Exactly 72 hours allows cancellation preview");clock.Now=boundary.AddMilliseconds(1);
Deny(()=>store.Offer(cutoffActor,new("change","STAY-A0000001","standard",D(65),D(67)),"late"),"OUTSIDE_FREE_WINDOW","Reschedule cutoff uses old arrival; one millisecond late denied");clock.Now=now;
var crash=store.Offer(a,new("create",Type:"suite",Arrival:D(70),Departure:D(72)),"crash");Raw("CREATE TRIGGER injected_failure BEFORE INSERT ON ResortNight WHEN NEW.Night='"+D(70)+"' BEGIN SELECT RAISE(ABORT,'fixture'); END;");
try{store.Confirm(a,crash.Id);throw new InvalidOperationException("Expected storage failure");}catch(SqliteException){Check(Sql("SELECT COUNT(*) FROM ResortStay WHERE Arrival='"+D(70)+"'")==0&&store.Receipt(a,crash.Id) is null,"Injected storage failure rolls back booking/allocation/receipt");}
Raw("DROP TRIGGER injected_failure;");var reopened=new ResortStore(path,clock);reopened.Recover(a);Check(reopened.Receipt(a,crash.Id)?.Status=="Completed"&&store.Confirm(a,crash.Id).Status=="Completed","Restart recovers queued command once and returns lost-response receipt");
var code=store.CreateLinkCode(a);store.SubmitLinkCode(code,"route-a",1);Check(store.LinkedSession("route-a",1) is null,"Submitting link code does not activate before same-session confirmation");
Deny(()=>store.ConfirmLink(b),"LINK_CONFIRMATION_REQUIRED","Other browser session cannot confirm pending link");store.ConfirmLink(a);Check(store.LinkedSession("route-a",1)==a.SessionId&&store.LinkedSession("route-a",2) is null,"Confirmed link bound to session and ownership epoch");
Deny(()=>store.SubmitLinkCode(code,"route-b",1),"LINK_CODE_INVALID","Consumed link code cannot be replayed");store.Unlink(a);Check(store.LinkedSession("route-a",1) is null,"Unlink immediately removes authority");
var expiredCode=store.CreateLinkCode(a);clock.Now=now.AddMinutes(6);Deny(()=>store.SubmitLinkCode(expiredCode,"route-exp",1),"LINK_CODE_INVALID","Expired link code rejected");clock.Now=now;
for(int i=0;i<5;i++)try{store.SubmitLinkCode(new string('F',32),"attack",1);}catch(RequestRejected){}Deny(()=>store.SubmitLinkCode(new string('F',32),"attack",1),"LINK_RATE_LIMITED","Persistent link attempt limit survives calls");
Check(ResortLanguage.Parse("Reservar Suite del 20 al 22 de octubre de 2026 para 4 personas",now) is{Action:"create",Arrival:"2026-10-20",Departure:"2026-10-22",Guests:4},"Spanish named-month date range interpreted without guessing year");
Check(ResortLanguage.Parse("Book Suite 2026-10-20 to 2026-10-22 for 4 guests",now) is{Action:"create",Guests:4},"English ISO booking parsed");
Check(ResortLanguage.Parse("Reservar suite mañana",now)?.Arrival is null&&ResortLanguage.Confirmation("sí") is null,"Ambiguous date and bare yes cannot execute");
Check(ResortLanguage.LanguageControl("que sea en espanol")=="es"&&ResortLanguage.LanguageControl(@"\es:")=="es","Natural Spanish language request and backslash alias supported");
Check(ResortLanguage.SanitizeChannelText("Book Suite 2026-10-20 to 2026-10-22 for 2 guests email guest@example.invalid").Contains("2026-10-20")&&!ResortLanguage.SanitizeChannelText("guest@example.invalid").Contains("guest@example.invalid"),"Booking dates preserved while email remains masked");
Deny(()=>ResortLanguage.SanitizeChannelText("Book suite 4111111111111111"),"SENSITIVE_PAYMENT_DATA","Payment guard still rejects cards in booking requests");
var ledgerPath=Path.Combine(root,"channels.db");var ledger=new ChannelStore(ledgerPath,RandomNumberGenerator.GetBytes(32));var ep=new EndpointOptions("whatsapp","987",new HashSet<string>{"18095550001"},"fixture-token",new string('a',32),new string('v',32),"v99.0",true);
ChannelText Msg(string id,string text)=>new(ep.Channel,ep.EndpointId,id,ep.Recipients.Single(),now,now,text);
var linkCode=new string('A',32);ledger.Accept(Msg("code","Vincular "+linkCode),ep);using(var db=new SqliteConnection("Data Source="+ledgerPath)){db.Open();using var c=db.CreateCommand();c.CommandText="SELECT Text FROM ChannelInbox";Check(!((string)c.ExecuteScalar()!).Contains(linkCode),"One-use channel link code encrypted at rest before processing");}
var job=ledger.Claim(now)!;Check(job.Text=="Vincular "+linkCode,"Authorized worker decrypts pending link command");ledger.Complete(job,new(new("Code received","LINK_PENDING",[])),now);using(var db=new SqliteConnection("Data Source="+ledgerPath)){db.Open();using var c=db.CreateCommand();c.CommandText="SELECT Text FROM ChannelInbox";Check((string)c.ExecuteScalar()! == "","Completed link input text cleared");}
ledger.Accept(Msg("booking","Reservar suite "+D(80)+" a "+D(82)),ep);var privateJob=ledger.Claim(now)!;ledger.RequestHandoff(privateJob.Route.Key);var transport=new CountingHandler();using(var bridge=new ResortBridgeClient(new string('K',64),ledger,transport)){Check((await bridge.AnswerAsync(privateJob,CancellationToken.None)).ReasonCode=="CHANNEL_AUTHORITY_CHANGED"&&transport.Count==0,"Handoff prevents new booking bridge call for old epoch");}

// Actual product middleware: cookie, origin, CSRF, closed DTOs, fixed bridge auth.
var hostRoot=Path.Combine(root,"http");Directory.CreateDirectory(hostRoot);var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ContentRootPath=hostRoot,EnvironmentName="Production"});builder.WebHost.UseTestServer();
builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{["CCAI_RESORT_ENABLED"]="true",["CCAI_RESORT_BRIDGE_KEY"]=new string('K',64),["CCAI_RESORT_META_ENDPOINT_ID"]="987",["CCAI_SOURCE_SERVICE_KEY"]=new string('S',64)});
await using var app=ProductApi.Build(builder,true);await app.StartAsync();using var http=app.GetTestClient();http.BaseAddress=new("http://127.0.0.1:7452");
Check((await http.GetAsync("/v2/resort/catalog")).StatusCode==HttpStatusCode.OK,"Actual product exposes anonymous aggregate catalog");
Check((await http.GetAsync("/v2/resort/stays")).StatusCode==HttpStatusCode.Unauthorized,"Actual product denies no-cookie private bookings");
var bridgeInput=new ResortChannelRequest(new string('A',64),1,"987","whatsapp","Suite","event-a","es");
Check((await http.PostAsJsonAsync("/internal/resort/channel",bridgeInput)).StatusCode==HttpStatusCode.Unauthorized,"Missing bridge key cannot invoke internal source");
async Task<HttpResponseMessage> Internal(ResortChannelRequest r){var q=new HttpRequestMessage(HttpMethod.Post,"/internal/resort/channel"){Content=JsonContent.Create(r)};q.Headers.Add("X-Resort-Service-Key",new string('K',64));return await http.SendAsync(q);}
Check((await Internal(bridgeInput with{Endpoint="other"})).StatusCode==HttpStatusCode.BadRequest,"Fixed provider endpoint validation enforced through HTTP");
Check((await (await Internal(bridgeInput with{Text="Mis reservas"})).Content.ReadFromJsonAsync<ResortChannelResult>())?.Code=="CHANNEL_LINK_REQUIRED","Service key alone does not authenticate a customer");
var identities=app.Services.GetRequiredService<IOperationalStore>();var realActor=await identities.LoginAsync(ProductApi.Issuer,"10000000-0000-0000-0000-000000000001",ActorRoles.Customer,null,DateTimeOffset.UtcNow,CancellationToken.None);
var options=app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(ProductApi.CookieScheme);var principal=new ClaimsPrincipal(new ClaimsIdentity([new Claim("ccai_session_id",realActor.SessionId.ToString())],ProductApi.CookieScheme));
var cookie=options.TicketDataFormat.Protect(new AuthenticationTicket(principal,new AuthenticationProperties{ExpiresUtc=realActor.ExpiresAt},ProductApi.CookieScheme));http.DefaultRequestHeaders.Add("Cookie",options.Cookie.Name+"="+cookie);
var bootstrap=await http.GetAsync("/v1/session/csrf");var csrf=(await bootstrap.Content.ReadFromJsonAsync<JsonObject>())!["token"]!.GetValue<string>();var csrfCookie=bootstrap.Headers.GetValues("Set-Cookie").Single().Split(';')[0];http.DefaultRequestHeaders.Remove("Cookie");http.DefaultRequestHeaders.Add("Cookie",options.Cookie.Name+"="+cookie+"; "+csrfCookie);
Check((await http.PostAsJsonAsync("/v2/resort/offers",request)).StatusCode==HttpStatusCode.Forbidden,"Actual v2 writes require correct Origin");http.DefaultRequestHeaders.Add("Origin","http://127.0.0.1:7452");
Check((await http.PostAsJsonAsync("/v2/resort/offers",request)).StatusCode==HttpStatusCode.Forbidden,"Actual v2 writes require antiforgery token");http.DefaultRequestHeaders.Add("X-CSRF-TOKEN",csrf);http.DefaultRequestHeaders.Add("Idempotency-Key","http-quote");
Check((await http.PostAsJsonAsync("/v2/resort/offers",new{action="create",type="suite",arrival=D(20),departure=D(22),memberRef="MEM-002"})).StatusCode==HttpStatusCode.BadRequest,"Closed product DTO rejects caller-supplied member identity");
Check((await http.PostAsJsonAsync("/v2/resort/offers",request)).StatusCode==HttpStatusCode.OK,"Verified customer creates quote through cookie and CSRF middleware");
Check((await http.PostAsJsonAsync("/v2/resort/blocks",new ResortBlockRequest("suite",D(30),D(32)))).StatusCode==HttpStatusCode.Forbidden,"Actual customer HTTP request cannot gain admin inventory role");
var productStore=app.Services.GetRequiredService<ResortStore>();var pc=productStore.CreateLinkCode(realActor);await Internal(bridgeInput with{Text="Vincular "+pc});
Check((await http.PostAsJsonAsync("/v2/resort/link-confirm",new{})).StatusCode==HttpStatusCode.OK,"Real persisted actor session confirms WhatsApp linkage over HTTP");
Check((await (await Internal(bridgeInput with{Text="Mis reservas"})).Content.ReadFromJsonAsync<ResortChannelResult>())?.Code=="RESORT_MY_STAYS","Linked channel resolves current persisted own account");
async Task<ResortChannelResult> Channel(string text,string eventId="flow",long epoch=1)=> (await (await Internal(bridgeInput with{Text=text,EventKey=eventId,Epoch=epoch})).Content.ReadFromJsonAsync<ResortChannelResult>())!;
var channelOffer=await Channel("Reservar Suite "+D(35)+" a "+D(37)+" para 4 personas","create-channel");
var channelOfferId=System.Text.RegularExpressions.Regex.Match(channelOffer.Text,@"[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}").Value;
Check(channelOffer.Code=="RESORT_OFFER"&&channelOffer.Text.Contains("560.00 USD")&&Guid.TryParse(channelOfferId,out _),"Linked Spanish booking yields precise unexecuted quote over real HTTP bridge");
Check((await Channel("Sí")).Code=="CONFIRM_REFERENCE_REQUIRED","Bare yes through channel cannot confirm an offer");
Check((await Channel("Confirmar "+channelOfferId,epoch:2)).Code=="CHANNEL_LINK_REQUIRED","Wrong ownership epoch cannot confirm via internal bridge");
var channelDone=await Channel("Confirmar "+channelOfferId);var stayCode=System.Text.RegularExpressions.Regex.Match(channelDone.Text,@"STAY-[A-F0-9]{8}").Value;
Check(channelDone.Code=="RESORT_COMPLETED"&&stayCode.Length==13&&productStore.MyStays(realActor).Any(s=>s.Id==stayCode),"Channel create confirmation returns source-owned persisted stay");
var changeFlow=await Channel("Change "+stayCode+" Suite "+D(38)+" to "+D(40)+" for 4 guests","change-channel");var changeFlowId=System.Text.RegularExpressions.Regex.Match(changeFlow.Text,@"[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}").Value;
Check(changeFlow.Code=="RESORT_OFFER"&&(await Channel("Confirm "+changeFlowId)).Code=="RESORT_COMPLETED"&&productStore.MyStays(realActor).Single(s=>s.Id==stayCode).Arrival==D(38),"English channel change replaces dates only after explicit source confirmation");
var cancelFlow=await Channel("Cancelar "+stayCode,"cancel-channel");var cancelFlowId=System.Text.RegularExpressions.Regex.Match(cancelFlow.Text,@"[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}").Value;
Check((await Channel("Confirmar "+cancelFlowId)).Code=="RESORT_COMPLETED"&&productStore.MyStays(realActor).Single(s=>s.Id==stayCode).Status=="Cancelled","Channel cancellation completes and persists cancelled status");
await identities.RevokeSessionAsync(realActor.SessionId,DateTimeOffset.UtcNow,CancellationToken.None);
Check((await (await Internal(bridgeInput with{Text="Mis reservas"})).Content.ReadFromJsonAsync<ResortChannelResult>())?.Code=="CHANNEL_LINK_REQUIRED","Logout revokes linked channel on next request");
await app.StopAsync();
Directory.CreateDirectory(Path.Combine(repo,"docs","progress"));File.WriteAllText(Path.Combine(repo,"docs","progress","resort-v0.12-checks.json"),JsonSerializer.Serialize(new{result="PASS",observedAtUtc=DateTimeOffset.UtcNow,checksPassed=checks.Count,checks,scope="Isolated synthetic source, real transactions/concurrency/storage fault/recovery and actual HTTP middleware. Not live provider booking evidence."},new JsonSerializerOptions{WriteIndented=true})+"\n");
Console.WriteLine($"Resort checks: {checks.Count} passed.");return 0;
}
catch(Exception e){Console.Error.WriteLine(e.ToString());return 1;}
sealed class TestClock(DateTimeOffset now):TimeProvider{public DateTimeOffset Now=now;public override DateTimeOffset GetUtcNow()=>Now;}
sealed class CountingHandler:HttpMessageHandler{public int Count;protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken c){Count++;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));}}
