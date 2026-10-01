using System.Text.Json;
using ContactCenterAI.Application;
using ContactCenterAI.Domain;
using ContactCenterAI.Infrastructure;
using ContactCenterAI.Simulator;
using Microsoft.Data.Sqlite;

try
{
    var root=Path.GetFullPath(args.Single()); var checks=new List<string>();
    void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);checks.Add(label);Console.WriteLine("PASS: "+label);}
    async Task Denied(Func<Task> action,int status,string label){try{await action();}catch(RequestRejected error){Check(error.Status==status,label);return;}throw new InvalidOperationException(label);}
    var fixture=await Fixture.Create(root);var a=fixture.A;var b=fixture.B;var convo=await fixture.Conversation(a);var source=fixture.Source;
    var offer=await fixture.Workflow.PreviewAsync(a,convo.ConversationId,"RES-001",1,default);
    Check(offer.ExpiresAt==fixture.Clock.Now.AddMinutes(5)&&offer.PenaltyMinorUnits==0&&source.Store.Get("MEM-001","RES-001").Status==ReservationStatus.Confirmed,"Preview persists bound five minute offer without business effect");
    await Denied(()=>fixture.Workflow.PreviewAsync(b,convo.ConversationId,"RES-001",2,default),404,"Foreign conversation cannot preview or discover reservation");
    var bConvo=await fixture.Conversation(b);
    await Denied(()=>fixture.Workflow.PreviewAsync(b,bConvo.ConversationId,"RES-001",1,default),404,"Server member identity prevents foreign reservation preview");
    await Denied(()=>fixture.Workflow.PreviewAsync(a,convo.ConversationId,"RES-002",2,default),422,"Ineligible reservation creates no active offer");
    await Denied(()=>fixture.Workflow.ConfirmAsync(a,convo.ConversationId,offer.OfferId,"yes",offer.Version,"invalid",default),400,"Chat yes cannot substitute explicit confirm control");
    await Denied(()=>fixture.Workflow.ConfirmAsync(b,bConvo.ConversationId,offer.OfferId,"confirm",offer.Version,"foreign",default),404,"Foreign offer cannot be consumed");
    var receipt=await fixture.Workflow.ConfirmAsync(a,convo.ConversationId,offer.OfferId,"confirm",offer.Version,"same-key",default);
    Check(receipt.Status=="Pending"&&receipt.OperationId is not null&&source.Posts==0,"Confirmation commits Pending before dispatch and reports no success");
    var replays=await Task.WhenAll(Enumerable.Range(0,100).Select(_=>fixture.Workflow.ConfirmAsync(a,convo.ConversationId,offer.OfferId,"confirm",offer.Version,"same-key",default)));
    Check(replays.All(item=>item==receipt),"One hundred confirmation replays preserve operation ID");
    await Denied(()=>fixture.Workflow.ConfirmAsync(a,convo.ConversationId,offer.OfferId,"reject",offer.Version,"same-key",default),409,"Same confirmation key with altered decision conflicts");
    await Denied(()=>fixture.Store.OperationAsync(b,receipt.OperationId!.Value,fixture.Clock.Now,default),404,"Foreign and missing operation receive generic resource denial");
    var worker=new CommandDispatcher(fixture.Store,source,fixture.Clock,fixture.Gate);await worker.RunOnceAsync(default);
    var result=await fixture.Store.OperationAsync(a,receipt.OperationId!.Value,fixture.Clock.Now,default);
    Check(result.Status=="Completed"&&result.SourceReference is not null&&source.Posts==1,"Success becomes visible only after authoritative source receipt");

    var concurrent=await Fixture.Create(root);var cc=await concurrent.Conversation(concurrent.A);var co=await concurrent.Workflow.PreviewAsync(concurrent.A,cc.ConversationId,"RES-001",1,default);
    var confirmations=await Task.WhenAll(Enumerable.Range(0,12).Select(i=>Task.Run(async()=>{try{return (await concurrent.Workflow.ConfirmAsync(concurrent.A,cc.ConversationId,co.OfferId,"confirm",co.Version,"key-"+i,default)).OperationId;}catch(RequestRejected error)when(error.Status==409){return null;}})));
    Check(confirmations.Count(id=>id is not null)==1,"Twelve competing confirmations consume one offer and create one command");
    await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Task.Run(()=>new CommandDispatcher(concurrent.Store,concurrent.Source,concurrent.Clock,concurrent.Gate).RunOnceAsync(default))));
    Check(concurrent.Source.Posts==1&&concurrent.Source.Store.Get("MEM-001","RES-001").Version=="2","Two dispatchers claim one lease and cause one source transition");

    var expired=await Fixture.Create(root);var ec=await expired.Conversation(expired.A);var eo=await expired.Workflow.PreviewAsync(expired.A,ec.ConversationId,"RES-001",1,default);expired.Clock.Now=expired.Clock.Now.AddMinutes(5);
    await Denied(()=>expired.Workflow.ConfirmAsync(expired.A,ec.ConversationId,eo.OfferId,"confirm",eo.Version,"expired",default),409,"Offer expires exactly at five minute boundary");
    var changed=await Fixture.Create(root);var ch=await changed.Conversation(changed.A);var cho=await changed.Workflow.PreviewAsync(changed.A,ch.ConversationId,"RES-001",1,default);
    changed.Source.Store.Cancel("MEM-001",new(Guid.NewGuid(),"RES-001","1",CancellationPolicy.Version));
    await Denied(()=>changed.Workflow.ConfirmAsync(changed.A,ch.ConversationId,cho.OfferId,"confirm",cho.Version,"changed",default),409,"Source change between preview and confirm blocks acceptance");
    var reject=await Fixture.Create(root);var rc=await reject.Conversation(reject.A);var ro=await reject.Workflow.PreviewAsync(reject.A,rc.ConversationId,"RES-001",1,default);
    var rr=await reject.Workflow.ConfirmAsync(reject.A,rc.ConversationId,ro.OfferId,"reject",ro.Version,"reject",default);
    Check(rr.Status=="Rejected"&&rr.OperationId is null&&reject.Source.Posts==0,"Reject consumes offer without source command");

    var lost=await Fixture.Create(root);var lr=await lost.Pending();lost.Source.DropAfterCommit=true;var lw=new CommandDispatcher(lost.Store,lost.Source,lost.Clock,lost.Gate);await lw.RunOnceAsync(default);
    var uncertain=await lost.Store.OperationAsync(lost.A,lr.OperationId!.Value,lost.Clock.Now,default);
    Check(uncertain.Status=="Unknown"&&uncertain.SourceReference is null&&lost.Source.Store.Get("MEM-001","RES-001").Status==ReservationStatus.Cancelled,"Commit with lost response records Unknown without success claim");
    lost.Clock.Now=lost.Clock.Now.AddSeconds(3);await lw.RunOnceAsync(default);
    Check((await lost.Store.OperationAsync(lost.A,lr.OperationId.Value,lost.Clock.Now,default)).Status=="Completed"&&lost.Source.Posts==1&&lost.Source.Queries==1,"Reconciliation observes receipt without second POST");
    var restart=await Fixture.Create(root);var rp=await restart.Pending();var abandoned=await restart.Store.ClaimAsync(restart.Clock.Now,false,default);
    restart.Source.Store.Cancel("MEM-001",new(abandoned!.OperationId,abandoned.Payload.ReservationId,abandoned.Payload.ExpectedReservationVersion,abandoned.Payload.PolicyVersion));
    var reopened=new SqliteOperationalStore(restart.Path);restart.Clock.Now=restart.Clock.Now.AddSeconds(21);
    await new CommandDispatcher(reopened,restart.Source,restart.Clock,restart.Gate).RunOnceAsync(default);
    Check((await reopened.OperationAsync(restart.A,rp.OperationId!.Value,restart.Clock.Now,default)).Status=="Completed"&&restart.Source.Posts==0,"Reopened operational DB recovers expired Submitted lease through receipt query");
    var before=await Fixture.Create(root);var bp=await before.Pending();await before.Store.ClaimAsync(before.Clock.Now,false,default);before.Clock.Now=before.Clock.Now.AddSeconds(21);
    await new CommandDispatcher(new SqliteOperationalStore(before.Path),before.Source,before.Clock,before.Gate).RunOnceAsync(default);
    Check(before.Source.Queries==1&&before.Source.Posts==1&&(await before.Store.OperationAsync(before.A,bp.OperationId!.Value,before.Clock.Now,default)).Status=="Completed","Crash before source POST queries definitive local NotFound then resends same command ID");
    var down=await Fixture.Create(root);var dp=await down.Pending();down.Source.Unavailable=true;var dw=new CommandDispatcher(down.Store,down.Source,down.Clock,down.Gate);await dw.RunOnceAsync(default);down.Clock.Now=down.Clock.Now.AddSeconds(61);await dw.RunOnceAsync(default);
    Check((await down.Store.OperationAsync(down.A,dp.OperationId!.Value,down.Clock.Now,default)) is {Status:"Unknown",RequiresHumanReview:true}&&down.Source.Posts==1,"Unresolved source beyond sixty seconds flags human review without blind retry");
    var killed=await Fixture.Create(root);var kp=await killed.Pending();killed.Gate.Disabled=true;await new CommandDispatcher(killed.Store,killed.Source,killed.Clock,killed.Gate).RunOnceAsync(default);
    Check((await killed.Store.OperationAsync(killed.A,kp.OperationId!.Value,killed.Clock.Now,default)).Status=="Rejected"&&killed.Source.Posts==0,"Kill switch rejects durable Pending without source mutation");
    var recovery=await Fixture.Create(root);var rec=await recovery.Pending();recovery.Source.DropAfterCommit=true;var rw=new CommandDispatcher(recovery.Store,recovery.Source,recovery.Clock,recovery.Gate);await rw.RunOnceAsync(default);recovery.Clock.Now=recovery.Clock.Now.AddSeconds(3);recovery.Gate.Disabled=true;await rw.RunOnceAsync(default);
    Check((await recovery.Store.OperationAsync(recovery.A,rec.OperationId!.Value,recovery.Clock.Now,default)).Status=="Completed"&&recovery.Source.Posts==1,"Kill switch permits read reconciliation of already submitted operation");
    var revoked=await Fixture.Create(root);var rev=await revoked.Pending();using(var db=new SqliteConnection("Data Source="+revoked.Path)){db.Open();using var update=db.CreateCommand();update.CommandText="UPDATE Principal SET Active=0 WHERE MemberRef='MEM-001'";update.ExecuteNonQuery();}
    await new CommandDispatcher(revoked.Store,revoked.Source,revoked.Clock,revoked.Gate).RunOnceAsync(default);
    Check(revoked.Source.Posts==0,"Dispatcher revalidates active principal before first source submission");
    var stale=await Fixture.Create(root);await stale.Pending();var firstLease=(await stale.Store.ClaimAsync(stale.Clock.Now,false,default))!;stale.Clock.Now=stale.Clock.Now.AddSeconds(21);var newLease=await stale.Store.ClaimAsync(stale.Clock.Now,false,default);
    await stale.Store.FinishAsync(firstLease,"Rejected","STALE_TEST",null,stale.Clock.Now,default);
    Check((await stale.Store.OperationAsync(stale.A,firstLease.OperationId,stale.Clock.Now,default)).Status=="Submitted"&&newLease!.LeaseId!=firstLease.LeaseId,"Stale lease holder cannot overwrite current recovery lease");
    File.WriteAllText(Path.Combine(root,"docs/progress/b06-b07-evidence.json"),JsonSerializer.Serialize(new{result="PASS",observedAtUtc=DateTimeOffset.UtcNow,checksPassed=checks.Count,checks,scope="C# persistent Operational/Source SQLite, clocks, two dispatchers, crash boundaries, service-port fault injection. Browser evidence recorded separately.",sqlServerTested=false},new JsonSerializerOptions{WriteIndented=true})+"\n");
    return 0;
}
catch(Exception failure){Console.Error.WriteLine("Workflow checks failed: "+failure.GetType().Name+" "+failure.Message+(failure is OperationalUnavailable unavailable?" storeCode="+unavailable.StoreErrorCode:""));return 1;}
sealed class TestClock:TimeProvider{public DateTimeOffset Now{get;set;}=new(2026,10,1,12,0,0,TimeSpan.Zero);public override DateTimeOffset GetUtcNow()=>Now;}
sealed class SourcePort(ReservationStore store):IReservationSource,ISourceCommandPort
{
    public ReservationStore Store{get;}=store; public int Posts,Queries;public bool DropAfterCommit,Unavailable;
    public Task<IReadOnlyList<SourceReservation>> ListAsync(string member,CancellationToken ct)=>Task.FromResult(Store.List(member));
    private static SourceCancelReceipt Project(SourceCommandReceipt receipt)=>new(receipt.CommandId,receipt.ReservationId,receipt.Status,receipt.CurrentVersion,receipt.ReasonCode,receipt.SourceReference);
    public Task<SourceCancelReceipt> CancelAsync(string member,SourceCancelRequest request,CancellationToken ct){Interlocked.Increment(ref Posts);if(Unavailable)throw new RequestRejected(503,"SOURCE_UNAVAILABLE");try{var receipt=Store.Cancel(member,new(request.CommandId,request.ReservationId,request.ExpectedReservationVersion,request.PolicyVersion));if(DropAfterCommit)throw new RequestRejected(503,"RESPONSE_LOST");return Task.FromResult(Project(receipt));}catch(SourceRejected error){throw new SourceCommandRejected(error.Status,error.Code);}}
    public Task<SourceCancelReceipt?> ReceiptAsync(string member,Guid command,CancellationToken ct){Queries++;if(Unavailable)throw new RequestRejected(503,"SOURCE_UNAVAILABLE");var receipt=Store.Receipt(member,command);return Task.FromResult(receipt is null?null:Project(receipt));}
}
sealed class Fixture
{
    public required string Path{get;init;}public TestClock Clock{get;}=new();public required SqliteOperationalStore Store{get;init;}public required SourcePort Source{get;init;}public TransactionGate Gate{get;}=new(false);public Actor A{get;private set;}=null!;public Actor B{get;private set;}=null!;public CancellationWorkflow Workflow=>new(Store,Store,Source,Clock,Gate);
    public static async Task<Fixture>Create(string root){var directory=System.IO.Path.Combine(root,".local/tests/workflow-"+Guid.NewGuid().ToString("N"));var clock=new TestClock();var path=System.IO.Path.Combine(directory,"operational.db");var fixture=new Fixture{Path=path,Store=new(path),Source=new(new ReservationStore(System.IO.Path.Combine(directory,"source.db"),clock))};fixture.A=await fixture.Login(1);fixture.B=await fixture.Login(2);return fixture;}
    public Task<Actor> Login(int number)=>Store.LoginAsync("http://localhost:8080/realms/contactcenterai-local",$"10000000-0000-0000-0000-{number:D12}",ActorRoles.Customer,null,Clock.Now,default);
    public Task<ConversationReceipt> Conversation(Actor actor)=>new ConversationIngress(Store,Clock).CreateAsync(actor,"es",Guid.NewGuid().ToString(),default);
    public async Task<ConfirmationReceipt> Pending(){var c=await Conversation(A);var o=await Workflow.PreviewAsync(A,c.ConversationId,"RES-001",1,default);return await Workflow.ConfirmAsync(A,c.ConversationId,o.OfferId,"confirm",o.Version,Guid.NewGuid().ToString(),default);}
}
