using System.Text.Json;
using ContactCenterAI.Application;
using ContactCenterAI.Domain;
using Microsoft.Data.Sqlite;
namespace ContactCenterAI.Infrastructure;

public sealed record AssignedConversation(Guid ConversationId,string Language,string Ownership);
public sealed record AgentContext(Guid ConversationId,string Ownership,string Language,string Summary,IReadOnlyList<StoredMessage> Messages,IReadOnlyList<OperationView> Operations);
public sealed partial class SqliteOperationalStore : IAssistantLedger, IHandoffStore
{
    private static void InitializeAssistant(SqliteConnection db)
    {
        using var schema=Command(db,null,"""
            CREATE TABLE IF NOT EXISTS ConversationControl(ConversationId TEXT PRIMARY KEY,Ownership TEXT NOT NULL,RequestId TEXT,RequestEpoch INTEGER,ReasonCode TEXT,RequestedByTurn TEXT,NextAttempt INTEGER,HandoffAttempts INTEGER NOT NULL DEFAULT 0,FOREIGN KEY(ConversationId) REFERENCES Conversation(Id));
            CREATE TABLE IF NOT EXISTS TurnJob(Id TEXT PRIMARY KEY,ConversationId TEXT NOT NULL,MessageId TEXT NOT NULL,SessionId TEXT NOT NULL,Epoch INTEGER NOT NULL,Version INTEGER NOT NULL,Status TEXT NOT NULL,LeaseId TEXT,LeaseUntil INTEGER,CreatedAt INTEGER NOT NULL,Attempts INTEGER NOT NULL DEFAULT 0,FOREIGN KEY(Id) REFERENCES Inbox(Id));
            CREATE TABLE IF NOT EXISTS TurnResult(TurnId TEXT PRIMARY KEY,AnswerJson TEXT NOT NULL,CreatedAt INTEGER NOT NULL,FOREIGN KEY(TurnId) REFERENCES TurnJob(Id));
            """);schema.ExecuteNonQuery();
        var hasAttempts=false;
        using(var columns=Command(db,null,"PRAGMA table_info(TurnJob)"))using(var row=columns.ExecuteReader())while(row.Read())hasAttempts|=row.GetString(1)=="Attempts";
        if(!hasAttempts){using var migrate=Command(db,null,"ALTER TABLE TurnJob ADD COLUMN Attempts INTEGER NOT NULL DEFAULT 0");migrate.ExecuteNonQuery();}
    }
    private static void EnsureControl(SqliteConnection db,SqliteTransaction tx,Guid conversation)
    {using var insert=Command(db,tx,"INSERT OR IGNORE INTO ConversationControl(ConversationId,Ownership) VALUES(@id,'AI')",("@id",conversation));insert.ExecuteNonQuery();}
    private static void QueueTurn(SqliteConnection db,SqliteTransaction tx,Actor actor,Guid conversation,MessageReceipt receipt,long epoch,DateTimeOffset now)
    {
        EnsureControl(db,tx,conversation);
        using var control=Command(db,tx,"SELECT Ownership FROM ConversationControl WHERE ConversationId=@id",("@id",conversation));
        var ownership=(string)control.ExecuteScalar()!;
        if(ownership!="AI")
        {using var update=Command(db,tx,"UPDATE Inbox SET Status='HumanPending' WHERE Id=@turn",("@turn",receipt.TurnId));update.ExecuteNonQuery();return;}
        using var insert=Command(db,tx,"INSERT INTO TurnJob(Id,ConversationId,MessageId,SessionId,Epoch,Version,Status,LeaseId,LeaseUntil,CreatedAt) VALUES(@turn,@conversation,@message,@session,@epoch,@version,'Pending',NULL,NULL,@now)",
            ("@turn",receipt.TurnId),("@conversation",conversation),("@message",receipt.MessageId),("@session",actor.SessionId),("@epoch",epoch),("@version",receipt.Version),("@now",now));insert.ExecuteNonQuery();
    }
    public Task<TurnLease?> ClaimTurnAsync(DateTimeOffset now,CancellationToken ct)=>Run<TurnLease?>(ct,(db,tx)=>
    {
        Guid turn,conversation,message,session;long epoch,version;int attempts;
        using(var query=Command(db,tx,"SELECT Id,ConversationId,MessageId,SessionId,Epoch,Version,Attempts FROM TurnJob WHERE Status IN ('Pending','Processing') AND (LeaseUntil IS NULL OR LeaseUntil<=@now) ORDER BY CreatedAt LIMIT 1",("@now",now)))
        using(var row=query.ExecuteReader())
        {if(!row.Read())return null;turn=Guid.Parse(row.GetString(0));conversation=Guid.Parse(row.GetString(1));message=Guid.Parse(row.GetString(2));session=Guid.Parse(row.GetString(3));epoch=row.GetInt64(4);version=row.GetInt64(5);attempts=row.GetInt32(6);}
        var actor=Session(db,tx,session,now);var resource=actor is null?null:Resource(db,tx,actor,conversation,now);
        EnsureControl(db,tx,conversation);using var ownership=Command(db,tx,"SELECT Ownership FROM ConversationControl WHERE ConversationId=@id",("@id",conversation));
        if(actor is null||resource is null||resource.Value.Resource.Epoch!=epoch||(string)ownership.ExecuteScalar()!!="AI")
        {using var skip=Command(db,tx,"UPDATE TurnJob SET Status='Suppressed',LeaseId=NULL,LeaseUntil=NULL WHERE Id=@id; UPDATE Inbox SET Status='Suppressed' WHERE Id=@id",("@id",turn));skip.ExecuteNonQuery();return null;}
        if(attempts>=2)
        {
            var fallback=new AssistantAnswer(resource.Value.Language=="en"?"The turn reached its retry limit. Please request human assistance.":"El turno alcanzó su límite de reintentos. Solicita atención humana.","fallback","TURN_BUDGET_EXHAUSTED",[],"simulated-intent-v1");
            using var budget=Command(db,tx,"INSERT OR IGNORE INTO TurnResult VALUES(@id,@answer,@now); UPDATE TurnJob SET Status='Completed',LeaseId=NULL,LeaseUntil=NULL WHERE Id=@id; UPDATE Inbox SET Status='Completed' WHERE Id=@id",("@id",turn),("@answer",JsonSerializer.Serialize(fallback)),("@now",now));budget.ExecuteNonQuery();return null;
        }
        using var textQuery=Command(db,tx,"SELECT SanitizedText FROM Message WHERE Id=@id",("@id",message));var text=(string)textQuery.ExecuteScalar()!;
        var lease=Guid.NewGuid();using var claim=Command(db,tx,"UPDATE TurnJob SET Status='Processing',LeaseId=@lease,LeaseUntil=@until,Attempts=Attempts+1 WHERE Id=@id",("@lease",lease),("@until",now.AddSeconds(20)),("@id",turn));claim.ExecuteNonQuery();
        return new TurnLease(turn,lease,conversation,actor,text,resource.Value.Language,version,epoch);
    });
    public async Task SaveAnswerAsync(TurnLease job,AssistantAnswer answer,DateTimeOffset now,CancellationToken ct)=>await Run(ct,(db,tx)=>
    {
        using var query=Command(db,tx,"""
            SELECT c.Epoch,x.Ownership,x.RequestedByTurn FROM Conversation c JOIN ConversationControl x ON x.ConversationId=c.Id JOIN TurnJob j ON j.ConversationId=c.Id
            WHERE j.Id=@turn AND j.LeaseId=@lease AND j.Status='Processing'
            """,("@turn",job.TurnId),("@lease",job.LeaseId));
        bool publish;
        using(var row=query.ExecuteReader())
        {if(!row.Read())return 0;publish=row.GetInt64(0)==job.Epoch&&(row.GetString(1)=="AI"||(row.GetString(1)=="HandoffPending"&&answer.Intent=="request_handoff"&&!row.IsDBNull(2)&&row.GetString(2)==job.TurnId.ToString()));}
        // Revalidate all citation authority at commit; a stale retrieval cannot publish revoked material.
        if(publish&&answer.Citations.Any(citation=>Evidence(db,tx,job.Actor,job.ConversationId,null,citation.DocumentId,citation.Version,citation.SectionId,null,now) is null))
            answer=new(job.Language=="en"?"The approved evidence is no longer available. Please request human assistance.":"La evidencia aprobada ya no está disponible. Solicita atención humana.","faq","EVIDENCE_REVOKED",[],answer.ProviderId);
        if(publish)
        {using var insert=Command(db,tx,"INSERT OR IGNORE INTO TurnResult VALUES(@turn,@answer,@now)",("@turn",job.TurnId),("@answer",JsonSerializer.Serialize(answer)),("@now",now));insert.ExecuteNonQuery();}
        using var finish=Command(db,tx,"UPDATE TurnJob SET Status=@status,LeaseId=NULL,LeaseUntil=NULL WHERE Id=@id AND LeaseId=@lease; UPDATE Inbox SET Status=@status WHERE Id=@id",("@status",publish?"Completed":"Suppressed"),("@id",job.TurnId),("@lease",job.LeaseId));
        var count=finish.ExecuteNonQuery();Audit(db,tx,job.Actor,job.ConversationId,publish?"ASSISTANT_ANSWERED":"STALE_ANSWER_SUPPRESSED",now);return count;
    });
    public Task<AssistantSnapshot> ReadAnswersAsync(Actor supplied,Guid conversation,DateTimeOffset now,CancellationToken ct)=>Run(ct,(db,tx)=>
    {
        var actor=Require(db,tx,supplied,now);var resource=Resource(db,tx,actor,conversation,now)??throw new RequestRejected(404,"RESOURCE_NOT_FOUND");
        var turns=new List<AssistantTurn>();
        using(var query=Command(db,tx,"SELECT r.TurnId,r.AnswerJson FROM TurnResult r JOIN TurnJob j ON j.Id=r.TurnId WHERE j.ConversationId=@id ORDER BY j.CreatedAt LIMIT 100",("@id",conversation)))
        using(var row=query.ExecuteReader())while(row.Read())turns.Add(new(Guid.Parse(row.GetString(0)),JsonSerializer.Deserialize<AssistantAnswer>(row.GetString(1))!));
        for(var index=0;index<turns.Count;index++)
        {
            var turn=turns[index];if(turn.Answer.Citations.Any(citation=>Evidence(db,tx,actor,conversation,null,citation.DocumentId,citation.Version,citation.SectionId,null,now) is null))
                turns[index]=turn with{Answer=turn.Answer with{Text=resource.Language=="en"?"This evidence is no longer available.":"Esta evidencia ya no está disponible.",Citations=[],ReasonCode="EVIDENCE_UNAVAILABLE"}};
        }
        using var ownership=Command(db,tx,"SELECT Ownership FROM ConversationControl WHERE ConversationId=@id",("@id",conversation));var state=ownership.ExecuteScalar() as string??"AI";
        using var pending=Command(db,tx,"SELECT COUNT(*) FROM TurnJob WHERE ConversationId=@id AND Status IN ('Pending','Processing')",("@id",conversation));
        return new AssistantSnapshot(state,resource.Resource.Epoch,resource.Resource.Version,turns,Convert.ToInt32(pending.ExecuteScalar()));
    });
    public Task<HandoffRequest> RequestAsync(Actor supplied,Guid conversation,Guid? turn,string reason,DateTimeOffset now,CancellationToken ct)=>Run(ct,(db,tx)=>
    {
        var actor=Require(db,tx,supplied,now);var resource=Resource(db,tx,actor,conversation,now)??throw new RequestRejected(404,"RESOURCE_NOT_FOUND");EnsureControl(db,tx,conversation);
        if(actor.PrincipalId is null||!actor.Roles.HasFlag(ActorRoles.Customer))throw new RequestRejected(403,"VERIFIED_CUSTOMER_REQUIRED");
        using(var existing=Command(db,tx,"SELECT Ownership,RequestId,RequestEpoch,ReasonCode FROM ConversationControl WHERE ConversationId=@id",("@id",conversation)))
        using(var row=existing.ExecuteReader())if(row.Read()&&row.GetString(0)!="AI")return new(Guid.Parse(row.GetString(1)),conversation,row.GetInt64(2),resource.Language,row.GetString(3));
        if(reason is not ("CUSTOMER_REQUEST" or "NO_EVIDENCE" or "SOURCE_UNCERTAIN"))throw new RequestRejected(400,"INVALID_REQUEST");
        var request=new HandoffRequest(Guid.NewGuid(),conversation,resource.Resource.Epoch,resource.Language,reason);
        using var update=Command(db,tx,"""
            UPDATE ConversationControl SET Ownership='HandoffPending',RequestId=@request,RequestEpoch=@epoch,ReasonCode=@reason,RequestedByTurn=@turn,NextAttempt=@now WHERE ConversationId=@id;
            UPDATE Offer SET Status='Invalidated' WHERE ConversationId=@id AND Status='Active';
            UPDATE Conversation SET Version=Version+1 WHERE Id=@id;
            """,("@request",request.RequestId),("@epoch",request.Epoch),("@reason",reason),("@turn",turn),("@now",now),("@id",conversation));update.ExecuteNonQuery();Audit(db,tx,actor,conversation,"HANDOFF_REQUESTED",now);return request;
    });
    public Task<HandoffRequest?> ClaimHandoffAsync(DateTimeOffset now,CancellationToken ct)=>Run<HandoffRequest?>(ct,(db,tx)=>
    {
        HandoffRequest request;int attempts;
        using(var query=Command(db,tx,"SELECT x.RequestId,c.Id,x.RequestEpoch,c.Language,x.ReasonCode,x.HandoffAttempts FROM ConversationControl x JOIN Conversation c ON c.Id=x.ConversationId WHERE x.Ownership='HandoffPending' AND x.NextAttempt<=@now AND x.HandoffAttempts<10 ORDER BY x.NextAttempt LIMIT 1",("@now",now)))
        using(var row=query.ExecuteReader()){if(!row.Read())return null;request=new(Guid.Parse(row.GetString(0)),Guid.Parse(row.GetString(1)),row.GetInt64(2),row.GetString(3),row.GetString(4));attempts=row.GetInt32(5)+1;}
        using var lease=Command(db,tx,"UPDATE ConversationControl SET NextAttempt=@until,HandoffAttempts=@attempts WHERE ConversationId=@id",("@until",now.AddSeconds(20)),("@attempts",attempts),("@id",request.ConversationId));lease.ExecuteNonQuery();return request;
    });
    public async Task ApplyAcknowledgmentAsync(HandoffAcknowledgment acknowledgment,DateTimeOffset now,CancellationToken ct)=>await Run(ct,(db,tx)=>
    {
        string tenant;
        using(var match=Command(db,tx,"SELECT c.TenantId FROM Conversation c JOIN ConversationControl x ON x.ConversationId=c.Id WHERE c.Id=@conversation AND c.Epoch=@epoch AND x.RequestId=@request AND x.RequestEpoch=@epoch AND x.Ownership='HandoffPending'",("@conversation",acknowledgment.ConversationId),("@epoch",acknowledgment.Epoch),("@request",acknowledgment.RequestId)))
            if(match.ExecuteScalar() is string found)tenant=found;else return 0;
        if(!acknowledgment.Accepted||acknowledgment.AssignedAgent is not{} agent)return 0;
        using var binding=Command(db,tx,"SELECT COUNT(*) FROM Principal WHERE Id=@id AND TenantId=@tenant AND Active=1 AND (AllowedRoles & 2)=2",("@id",agent),("@tenant",tenant));
        if((long)binding.ExecuteScalar()!!=1)return 0;
        using var accept=Command(db,tx,"""
            UPDATE ConversationControl SET Ownership='HumanOwned' WHERE ConversationId=@conversation;
            UPDATE Conversation SET Epoch=Epoch+1,Version=Version+1 WHERE Id=@conversation;
            INSERT INTO Assignment VALUES(@assignment,@tenant,@agent,@conversation,@now,@expiry,0);
            """,("@conversation",acknowledgment.ConversationId),("@assignment",Guid.NewGuid()),("@tenant",tenant),("@agent",agent),("@now",now),("@expiry",now.AddHours(8)));
        var count=accept.ExecuteNonQuery();WorkerAudit(db,tx,tenant,acknowledgment.ConversationId,"HANDOFF_ACCEPTED",now);return count;
    });
    public Task<IReadOnlyList<AssignedConversation>> AssignedAsync(Actor supplied,DateTimeOffset now,CancellationToken ct)=>Run<IReadOnlyList<AssignedConversation>>(ct,(db,tx)=>
    {
        var actor=Require(db,tx,supplied,now);if((actor.Roles&(ActorRoles.Agent|ActorRoles.Supervisor))==0)throw new RequestRejected(403,"AGENT_REQUIRED");
        using var query=Command(db,tx,"SELECT DISTINCT c.Id,c.Language,x.Ownership FROM Conversation c JOIN Assignment a ON a.ConversationId=c.Id JOIN ConversationControl x ON x.ConversationId=c.Id WHERE a.PrincipalId=@principal AND a.TenantId=@tenant AND a.Revoked=0 AND a.StartsAt<=@now AND a.EndsAt>@now",("@principal",actor.PrincipalId),("@tenant",actor.TenantId),("@now",now));
        using var row=query.ExecuteReader();var result=new List<AssignedConversation>();while(row.Read())result.Add(new(Guid.Parse(row.GetString(0)),row.GetString(1),row.GetString(2)));return result;
    });
    public async Task<AgentContext> ContextAsync(Actor actor,Guid conversation,DateTimeOffset now,CancellationToken ct)
    {
        if((actor.Roles&(ActorRoles.Agent|ActorRoles.Supervisor))==0)throw new RequestRejected(403,"AGENT_REQUIRED");
        var snapshot=await ReadConversationAsync(actor,conversation,now,ct)??throw new RequestRejected(404,"RESOURCE_NOT_FOUND");
        var operations=await Run<IReadOnlyList<OperationView>>(ct,(db,tx)=>
        {
            var current=Require(db,tx,actor,now);if(!InternalAllowed(db,tx,current,conversation,now))throw new RequestRejected(404,"RESOURCE_NOT_FOUND");
            using var query=Command(db,tx,"SELECT "+OperationColumns+" FROM BusinessCommand WHERE ConversationId=@id ORDER BY CreatedAt DESC LIMIT 20",("@id",conversation));using var row=query.ExecuteReader();var result=new List<OperationView>();while(row.Read())result.Add(OperationRow(row));return result;
        });
        var assistant=await ReadAnswersAsync(actor,conversation,now,ct);
        var summary=string.Join("; ",operations.Select(item=>item.ReservationId+": "+item.Status+(item.Status=="Unknown"?" (resultado incierto / uncertain outcome)":"")+(item.Status=="Completed"?" (receipt fuente verificado / source receipt verified)":"")));
        return new(conversation,assistant.Ownership,snapshot.Language,string.IsNullOrEmpty(summary)?"Sin transacciones confirmadas / No confirmed transactions":summary,snapshot.Messages.TakeLast(10).ToArray(),operations);
    }
}
