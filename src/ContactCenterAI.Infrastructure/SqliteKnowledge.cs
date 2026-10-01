using ContactCenterAI.Application;
using ContactCenterAI.Domain;
using Microsoft.Data.Sqlite;
namespace ContactCenterAI.Infrastructure;

public sealed record KnowledgeDraft(string DocumentId, int Version, string Language, string Topic, string SectionId,
    string Title, string Content, string Classification, DateTimeOffset ExpiresAt);
public sealed partial class SqliteOperationalStore : IKnowledgeStore
{
    private static readonly (string Topic,string Language,string Title,string Content,string Classification)[] Corpus =
    [
        ("cancellation","es","Política de cancelación CP-001","CP-001:v1 permite cancelar sin penalidad una reserva confirmada cuando faltan al menos 72 horas antes de la llegada. Se requiere una propuesta vigente y confirmación explícita. La fuente vuelve a validar el estado antes de cancelar.","Public"),
        ("cancellation","en","Cancellation policy CP-001","CP-001:v1 allows a confirmed reservation to be cancelled without penalty at least 72 hours before check-in. A valid offer and explicit confirmation are required. The source checks the current state again before cancellation.","Public"),
        ("services","es","Servicios de la propiedad ficticia","Caribbean Horizon es una propiedad ficticia de la demo. Ofrece Wi-Fi y desayuno incluidos. La llegada comienza a las 15:00 y la salida es a las 11:00, hora local. Esta información es sintética y no representa un hotel real.","Public"),
        ("services","en","Synthetic property services","Caribbean Horizon is a fictional demo property. Wi-Fi and breakfast are included. Check-in starts at 15:00 and check-out is at 11:00 local time. This synthetic information does not represent a real hotel.","Public"),
        ("identity","es","Acceso a reservas","Inicia sesión con tu cuenta ficticia para consultar tus reservas. El sistema utiliza la identidad verificada de Keycloak. Escribir otro nombre o número de miembro en el chat no cambia tus permisos.","Public"),
        ("identity","en","Reservation access","Sign in with your fictional account to access your reservations. The system uses the identity verified by Keycloak. Typing another name or member number in chat does not change your permissions.","Public"),
        ("payments","es","Canal de pagos","Esta demo no procesa pagos, tarjetas ni reembolsos. No envíes números de tarjeta ni códigos CVV. Para una consulta fuera de alcance solicita atención humana en el contact center simulado.","Public"),
        ("payments","en","Payment channel","This demo does not process payments, cards or refunds. Do not send card numbers or CVV codes. For a request outside the scope, ask for human assistance in the simulated contact center.","Public"),
        ("agent","es","Revisión interna de operaciones","Procedimiento interno: una operación Unknown debe reconciliarse por commandId. Nunca declarar cancelación confirmada sin receipt Completed. Registrar revisión humana si la fuente sigue sin responder; no ejecutar un segundo comando con ID nuevo.","Agent"),
        ("agent","en","Internal operation review","Internal procedure: an Unknown operation must be reconciled by commandId. Never claim confirmed cancellation without a Completed receipt. Record human review if the source remains unavailable; never submit a second command with a new ID.","Agent")
    ];
    public static IReadOnlyList<object> CorpusManifest => Corpus.Select(item => (object)new { documentId=DocumentId(item.Topic,item.Language),version=1,item.Language,item.Title,item.Content,item.Classification,sectionId="overview" }).ToArray();
    private static string DocumentId(string topic,string language)=>"KB-"+topic.ToUpperInvariant()+"-"+language.ToUpperInvariant();
    private static void InitializeKnowledge(SqliteConnection db)
    {
        using (var schema=Command(db,null,"""
            CREATE TABLE IF NOT EXISTS KnowledgeRegistry(DocumentId TEXT PRIMARY KEY,ActiveVersion INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS KnowledgeVersion(DocumentId TEXT NOT NULL,Version INTEGER NOT NULL,Language TEXT NOT NULL,Topic TEXT NOT NULL,SectionId TEXT NOT NULL,Title TEXT NOT NULL,Content TEXT NOT NULL,Classification TEXT NOT NULL,TenantId TEXT NOT NULL,Status TEXT NOT NULL,EffectiveAt INTEGER NOT NULL,ExpiresAt INTEGER NOT NULL,EditorId TEXT NOT NULL,ReviewerId TEXT,ContentHash TEXT NOT NULL,PRIMARY KEY(DocumentId,Version));
            CREATE TABLE IF NOT EXISTS Feedback(Id TEXT PRIMARY KEY,ConversationId TEXT NOT NULL,TurnId TEXT NOT NULL,PrincipalId TEXT,Reason TEXT NOT NULL,Status TEXT NOT NULL,CreatedAt INTEGER NOT NULL);
            """))schema.ExecuteNonQuery();
        using var tx=db.BeginTransaction(deferred:false);
        foreach(var item in Corpus)
        {
            var id=DocumentId(item.Topic,item.Language);
            using var seed=Command(db,tx,"""
                INSERT OR IGNORE INTO KnowledgeVersion VALUES(@id,1,@language,@topic,'overview',@title,@content,@classification,'tenant-demo','Published',@from,@until,'10000000-0000-0000-0000-000000000006','10000000-0000-0000-0000-000000000007',@hash);
                INSERT OR IGNORE INTO KnowledgeRegistry VALUES(@id,1);
                """,("@id",id),("@language",item.Language),("@topic",item.Topic),("@title",item.Title),("@content",item.Content),("@classification",item.Classification),
                ("@from",new DateTimeOffset(2026,1,1,0,0,0,TimeSpan.Zero)),("@until",new DateTimeOffset(2028,1,1,0,0,0,TimeSpan.Zero)),("@hash",ConversationIngress.Hash(item.Content)));
            seed.ExecuteNonQuery();
        }
        tx.Commit();
    }
    private static bool InternalAllowed(SqliteConnection db,SqliteTransaction tx,Actor actor,Guid conversation,DateTimeOffset now)
    {
        if(actor.PrincipalId is null||(actor.Roles&(ActorRoles.Agent|ActorRoles.Supervisor))==0)return false;
        using var assignment=Command(db,tx,"SELECT COUNT(*) FROM Assignment WHERE PrincipalId=@principal AND ConversationId=@conversation AND TenantId=@tenant AND Revoked=0 AND StartsAt<=@now AND EndsAt>@now",
            ("@principal",actor.PrincipalId),("@conversation",conversation),("@tenant",actor.TenantId),("@now",now));
        return (long)assignment.ExecuteScalar()!>0;
    }
    private static KnowledgeEvidence? Evidence(SqliteConnection db,SqliteTransaction tx,Actor actor,Guid conversation,string? topic,string? document,int? version,string? section,string? language,DateTimeOffset now)
    {
        if(Resource(db,tx,actor,conversation,now) is null)throw new RequestRejected(404,"RESOURCE_NOT_FOUND");
        var internalAllowed=InternalAllowed(db,tx,actor,conversation,now);
        using var query=Command(db,tx,"""
            SELECT k.DocumentId,k.Version,k.Language,k.SectionId,k.Title,k.Content,k.ContentHash FROM KnowledgeVersion k JOIN KnowledgeRegistry r ON r.DocumentId=k.DocumentId AND r.ActiveVersion=k.Version
            WHERE k.TenantId=@tenant AND k.Status='Published' AND k.EffectiveAt<=@now AND k.ExpiresAt>@now AND (k.Classification='Public' OR (k.Classification='Agent' AND @internal=1))
            AND (@topic IS NULL OR k.Topic=@topic) AND (@document IS NULL OR k.DocumentId=@document) AND (@version IS NULL OR k.Version=@version)
            AND (@section IS NULL OR k.SectionId=@section) AND (@language IS NULL OR k.Language=@language) ORDER BY k.DocumentId LIMIT 1
            """,("@tenant",actor.TenantId),("@now",now),("@internal",internalAllowed?1:0),("@topic",topic),("@document",document),("@version",version),("@section",section),("@language",language));
        using var row=query.ExecuteReader();if(!row.Read())return null;
        if(ConversationIngress.Hash(row.GetString(5))!=row.GetString(6))return null;
        return new(row.GetString(0),row.GetInt32(1),row.GetString(2),row.GetString(3),row.GetString(4),row.GetString(5));
    }
    public Task<KnowledgeEvidence?> RetrieveAsync(Actor supplied,Guid conversation,string topic,string language,DateTimeOffset now,CancellationToken ct)=>Run(ct,(db,tx)=>Evidence(db,tx,Require(db,tx,supplied,now),conversation,topic,DocumentId(topic,language),null,null,language,now));
    public Task<KnowledgeEvidence?> ResolveAsync(Actor supplied,Guid conversation,string document,int version,string section,DateTimeOffset now,CancellationToken ct)=>Run(ct,(db,tx)=>Evidence(db,tx,Require(db,tx,supplied,now),conversation,null,document,version,section,null,now));
    public async Task RecordFeedbackAsync(Actor supplied,Guid conversation,Guid turn,string reason,DateTimeOffset now,CancellationToken ct)=>await Run(ct,(db,tx)=>
    {
        var actor=Require(db,tx,supplied,now);if(Resource(db,tx,actor,conversation,now) is null)throw new RequestRejected(404,"RESOURCE_NOT_FOUND");
        if(reason is not ("incorrect" or "missing" or "unclear"))throw new RequestRejected(400,"INVALID_REQUEST");
        using var lookup=Command(db,tx,"SELECT COUNT(*) FROM TurnJob WHERE Id=@turn AND ConversationId=@conversation",("@turn",turn),("@conversation",conversation));
        if((long)lookup.ExecuteScalar()!!=1)throw new RequestRejected(404,"RESOURCE_NOT_FOUND");
        using var insert=Command(db,tx,"INSERT INTO Feedback VALUES(@id,@conversation,@turn,@principal,@reason,'PendingReview',@now)",("@id",Guid.NewGuid()),("@conversation",conversation),("@turn",turn),("@principal",actor.PrincipalId),("@reason",reason),("@now",now));
        var count=insert.ExecuteNonQuery();Audit(db,tx,actor,conversation,"FEEDBACK_RECORDED",now);return count;
    });
    public async Task CreateDraftAsync(Actor supplied,KnowledgeDraft draft,DateTimeOffset now,CancellationToken ct)=>await Run(ct,(db,tx)=>
    {
        var actor=Require(db,tx,supplied,now);if(!actor.Roles.HasFlag(ActorRoles.KnowledgeEditor))throw new RequestRejected(403,"EDITOR_REQUIRED");
        if(draft.Language is not ("es" or "en")||draft.Topic is not ("cancellation" or "services" or "identity" or "payments" or "agent")||draft.Classification is not ("Public" or "Agent")||draft.Version<2||draft.SectionId!="overview"||string.IsNullOrWhiteSpace(draft.Content)||draft.Content.Length>4096||string.IsNullOrWhiteSpace(draft.Title)||draft.Title.Length>100||draft.ExpiresAt<=now)
            throw new RequestRejected(400,"INVALID_REQUEST");
        using(var registered=Command(db,tx,"SELECT COUNT(*) FROM KnowledgeVersion WHERE DocumentId=@id AND Language=@language AND Topic=@topic AND TenantId=@tenant",("@id",draft.DocumentId),("@language",draft.Language),("@topic",draft.Topic),("@tenant",actor.TenantId)))
            if((long)registered.ExecuteScalar()! == 0)throw new RequestRejected(400,"INVALID_REQUEST");
        if(draft.Topic=="agent"&&draft.Classification!="Agent")throw new RequestRejected(400,"INTERNAL_CLASSIFICATION_REQUIRED");
        if(new[]{"ignore previous","ignore all","ignora las instrucciones","system prompt","api_key=","password=","sk-"}.Any(marker=>draft.Content.Contains(marker,StringComparison.OrdinalIgnoreCase)))
            throw new RequestRejected(400,"SUSPICIOUS_KNOWLEDGE_TEXT");
        if(draft.Topic=="cancellation"&&draft.Content!=Corpus.Single(item=>item.Topic=="cancellation"&&item.Language==draft.Language).Content)
            throw new RequestRejected(409,"POLICY_TEXT_MISMATCH");
        using var insert=Command(db,tx,"INSERT INTO KnowledgeVersion VALUES(@id,@version,@language,@topic,@section,@title,@content,@classification,@tenant,'PendingReview',@now,@expiry,@editor,NULL,@hash)",
            ("@id",draft.DocumentId),("@version",draft.Version),("@language",draft.Language),("@topic",draft.Topic),("@section",draft.SectionId),("@title",draft.Title),("@content",draft.Content),("@classification",draft.Classification),("@tenant",actor.TenantId),("@now",now),("@expiry",draft.ExpiresAt),("@editor",actor.PrincipalId),("@hash",ConversationIngress.Hash(draft.Content)));
        var count=insert.ExecuteNonQuery();Audit(db,tx,actor,null,"KNOWLEDGE_DRAFT_CREATED",now);return count;
    });
    public async Task PublishAsync(Actor supplied,string document,int version,DateTimeOffset now,CancellationToken ct)=>await Run(ct,(db,tx)=>
    {
        var actor=Require(db,tx,supplied,now);if(!actor.Roles.HasFlag(ActorRoles.KnowledgeReviewer))throw new RequestRejected(403,"REVIEWER_REQUIRED");
        using var lookup=Command(db,tx,"SELECT EditorId,Status,ExpiresAt FROM KnowledgeVersion WHERE DocumentId=@id AND Version=@version AND TenantId=@tenant",("@id",document),("@version",version),("@tenant",actor.TenantId));
        using(var row=lookup.ExecuteReader())
        {if(!row.Read())throw new RequestRejected(404,"RESOURCE_NOT_FOUND");if(row.GetString(0)==actor.PrincipalId?.ToString())throw new RequestRejected(403,"SEPARATE_REVIEWER_REQUIRED");if(row.GetString(1)!="PendingReview"||row.GetInt64(2)<=now.ToUnixTimeMilliseconds())throw new RequestRejected(409,"NOT_PUBLISHABLE");}
        using var publish=Command(db,tx,"""
            UPDATE KnowledgeVersion SET Status='Superseded' WHERE DocumentId=@id AND Status='Published';
            UPDATE KnowledgeVersion SET Status='Published',ReviewerId=@reviewer WHERE DocumentId=@id AND Version=@version;
            INSERT INTO KnowledgeRegistry VALUES(@id,@version) ON CONFLICT(DocumentId) DO UPDATE SET ActiveVersion=excluded.ActiveVersion;
            """,("@id",document),("@version",version),("@reviewer",actor.PrincipalId));var count=publish.ExecuteNonQuery();Audit(db,tx,actor,null,"KNOWLEDGE_PUBLISHED",now);return count;
    });
    public async Task RevokeKnowledgeAsync(Actor supplied,string document,int version,DateTimeOffset now,CancellationToken ct)=>await Run(ct,(db,tx)=>
    {
        var actor=Require(db,tx,supplied,now);if(!actor.Roles.HasFlag(ActorRoles.KnowledgeReviewer))throw new RequestRejected(403,"REVIEWER_REQUIRED");
        using var revoke=Command(db,tx,"UPDATE KnowledgeVersion SET Status='Revoked' WHERE DocumentId=@id AND Version=@version AND TenantId=@tenant",("@id",document),("@version",version),("@tenant",actor.TenantId));
        var count=revoke.ExecuteNonQuery();if(count==0)throw new RequestRejected(404,"RESOURCE_NOT_FOUND");Audit(db,tx,actor,null,"KNOWLEDGE_REVOKED",now);return count;
    });
}
