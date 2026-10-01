using ContactCenterAI.Application;
using ContactCenterAI.Domain;
namespace ContactCenterAI.Infrastructure;

public sealed record OperationCounts(int Pending,int Submitted,int Unknown,int Completed,int Rejected,int Conflict,int HumanReview);
public sealed record OperationalSnapshot(OperationCounts Commands,int PendingTurns,int KnowledgeVariants,int IndexedVariants,bool SemanticEnabled,IReadOnlyList<string> Alerts);
public sealed partial class SqliteOperationalStore
{
    public Task<OperationalSnapshot> OperationsAsync(Actor supplied,DateTimeOffset now,CancellationToken ct)=>Run(ct,(db,tx)=>
    {
        var actor=Require(db,tx,supplied,now);if(!actor.Roles.HasFlag(ActorRoles.OperationsAdmin))throw new RequestRejected(403,"OPERATIONS_ADMIN_REQUIRED");
        int Count(string sql){using var q=Command(db,tx,sql,("@tenant",actor.TenantId),("@now",now));return Convert.ToInt32(q.ExecuteScalar());}
        int Status(string value){using var q=Command(db,tx,"SELECT COUNT(*) FROM BusinessCommand WHERE TenantId=@tenant AND Status=@status",("@tenant",actor.TenantId),("@status",value));return Convert.ToInt32(q.ExecuteScalar());}
        var commands=new OperationCounts(Status("Pending"),Status("Submitted"),Status("Unknown"),Status("Completed"),Status("Rejected"),Status("Conflict"),Count("SELECT COUNT(*) FROM BusinessCommand WHERE TenantId=@tenant AND RequiresHumanReview=1 AND Status IN ('Pending','Submitted','Unknown')"));
        var pending=Count("SELECT COUNT(*) FROM TurnJob j JOIN Conversation c ON c.Id=j.ConversationId WHERE c.TenantId=@tenant AND j.Status IN ('Pending','Processing')");
        var variants=Count("SELECT COUNT(*) FROM KnowledgeVersion k JOIN KnowledgeRegistry r ON r.DocumentId=k.DocumentId AND r.ActiveVersion=k.Version WHERE k.TenantId=@tenant AND k.Status='Published' AND k.EffectiveAt<=@now AND k.ExpiresAt>@now");
        var indexed=Count("SELECT COUNT(*) FROM KnowledgeVector v JOIN KnowledgeIndexActive a ON a.GenerationId=v.GenerationId JOIN KnowledgeVersion k ON k.DocumentId=v.DocumentId AND k.Version=v.Version AND k.ContentHash=v.ContentHash JOIN KnowledgeRegistry r ON r.DocumentId=k.DocumentId AND r.ActiveVersion=k.Version WHERE k.TenantId=@tenant AND k.Status='Published' AND k.EffectiveAt<=@now AND k.ExpiresAt>@now");
        var alerts=new List<string>();if(commands.Unknown>0)alerts.Add("Hay operaciones inciertas; reconciliar antes de repetir.");if(commands.HumanReview>0)alerts.Add("Hay operaciones que requieren revisión humana.");if(embeddings is not null&&indexed<variants)alerts.Add("Hay documentos aprobados pendientes de indexar; la búsqueda puede abstenerse.");
        return new OperationalSnapshot(commands,pending,variants,indexed,embeddings is not null,alerts);
    });
}
