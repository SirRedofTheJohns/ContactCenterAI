using System.Text.Json;
using System.Diagnostics;
using ContactCenterAI.Application;
using ContactCenterAI.Domain;
using Microsoft.Data.Sqlite;
namespace ContactCenterAI.Infrastructure;

public sealed partial class SqliteOperationalStore
{
    private readonly SemaphoreSlim indexAccess=new(1,1);
    private sealed record Section(string DocumentId,int Version,string Language,string Title,string Content,string Classification,string SectionId,string Topic);
    private sealed record IndexRow(string DocumentId,int Version,string Hash,string Text);
    private static readonly JsonSerializerOptions KnowledgeJson=new(JsonSerializerDefaults.Web);
    private static void InitializeSemanticKnowledge(SqliteConnection db)
    {
        using var schema=Command(db,null,"""
          CREATE TABLE IF NOT EXISTS KnowledgeIndex(Id TEXT PRIMARY KEY,ModelId TEXT NOT NULL,SnapshotHash TEXT NOT NULL,Count INTEGER NOT NULL,CreatedAt INTEGER NOT NULL);
          CREATE TABLE IF NOT EXISTS KnowledgeVector(GenerationId TEXT NOT NULL,DocumentId TEXT NOT NULL,Version INTEGER NOT NULL,ContentHash TEXT NOT NULL,Vector TEXT NOT NULL,PRIMARY KEY(GenerationId,DocumentId,Version));
          CREATE TABLE IF NOT EXISTS KnowledgeIndexActive(Singleton INTEGER PRIMARY KEY CHECK(Singleton=1),GenerationId TEXT NOT NULL);
          """);schema.ExecuteNonQuery();
        using var stream=typeof(SqliteOperationalStore).Assembly.GetManifestResourceStream("ContactCenterAI.Infrastructure.Knowledge.demo-v1.json")!;
        var sections=JsonSerializer.Deserialize<Section[]>(stream,KnowledgeJson)!;
        using var tx=db.BeginTransaction(deferred:false);
        foreach(var section in sections)
        {
            using var insert=Command(db,tx,"""
              INSERT OR IGNORE INTO KnowledgeVersion VALUES(@id,1,@language,@topic,'overview',@title,@content,@classification,'tenant-demo','Published',@from,@until,'10000000-0000-0000-0000-000000000006','10000000-0000-0000-0000-000000000007',@hash);
              INSERT OR IGNORE INTO KnowledgeRegistry VALUES(@id,1);
              """,("@id",section.DocumentId),("@language",section.Language),("@topic",section.Topic),("@title",section.Title),("@content",section.Content),("@classification",section.Classification),("@from",new DateTimeOffset(2026,1,1,0,0,0,TimeSpan.Zero)),("@until",new DateTimeOffset(2028,1,1,0,0,0,TimeSpan.Zero)),("@hash",ConversationIngress.Hash(section.Content)));insert.ExecuteNonQuery();
        }
        tx.Commit();
    }
    public async Task<int> BuildKnowledgeIndexAsync(DateTimeOffset now,CancellationToken ct)
    {
        if(embeddings is null)return 0;
        await indexAccess.WaitAsync(ct);
        try
        {
            var rows=await Run(ct,(db,tx)=>
            {
                var values=new List<IndexRow>();using var command=Command(db,tx,"""
                  SELECT k.DocumentId,k.Version,k.ContentHash,k.Title,k.Content FROM KnowledgeVersion k JOIN KnowledgeRegistry r ON r.DocumentId=k.DocumentId AND r.ActiveVersion=k.Version
                  WHERE k.Status='Published' AND k.EffectiveAt<=@now AND k.ExpiresAt>@now ORDER BY k.DocumentId LIMIT 1001
                  """,("@now",now));using var read=command.ExecuteReader();while(read.Read())
                {if(ConversationIngress.Hash(read.GetString(4))!=read.GetString(2))throw new RequestRejected(503,"KNOWLEDGE_HASH_MISMATCH");var content=read.GetString(4);var titled=read.GetString(3)+". "+content;values.Add(new(read.GetString(0),read.GetInt32(1),read.GetString(2),titled.Length<=4096?titled:content));}return values;
            });
            if(rows.Count is 0 or >1000)throw new RequestRejected(503,"KNOWLEDGE_INDEX_BOUND_REJECTED");
            var snapshot=ConversationIngress.Hash(string.Join("\n",rows.Select(row=>$"{row.DocumentId}|{row.Version}|{row.Hash}")));
            var current=await Run(ct,(db,tx)=>{using var query=Command(db,tx,"SELECT COUNT(*) FROM KnowledgeIndex i JOIN KnowledgeIndexActive a ON a.GenerationId=i.Id WHERE i.ModelId=@model AND i.SnapshotHash=@hash",("@model",embeddings.ModelId),("@hash",snapshot));return (long)query.ExecuteScalar()!>0;});
            if(current)return rows.Count;
            var vectors=new List<float[]>();
            foreach(var batch in rows.Chunk(8))vectors.AddRange(await embeddings.EmbedAsync(batch.Select(row=>row.Text).ToArray(),ct));
            await Run(ct,(db,tx)=>
            {
                var generation=Guid.NewGuid().ToString();using var insert=Command(db,tx,"INSERT INTO KnowledgeIndex VALUES(@id,@model,@hash,@count,@now)",("@id",generation),("@model",embeddings.ModelId),("@hash",snapshot),("@count",rows.Count),("@now",now));insert.ExecuteNonQuery();
                for(var i=0;i<rows.Count;i++)
                {var row=rows[i];using var vector=Command(db,tx,"INSERT INTO KnowledgeVector VALUES(@generation,@id,@version,@hash,@vector)",("@generation",generation),("@id",row.DocumentId),("@version",row.Version),("@hash",row.Hash),("@vector",JsonSerializer.Serialize(OllamaEmbeddingProvider.Normalize(vectors[i]))));vector.ExecuteNonQuery();}
                using var activate=Command(db,tx,"INSERT INTO KnowledgeIndexActive VALUES(1,@generation) ON CONFLICT(Singleton) DO UPDATE SET GenerationId=excluded.GenerationId;DELETE FROM KnowledgeVector WHERE GenerationId<>@generation;DELETE FROM KnowledgeIndex WHERE Id<>@generation;",("@generation",generation));activate.ExecuteNonQuery();return rows.Count;
            });return rows.Count;
        }finally{indexAccess.Release();}
    }
    public async Task<RetrievalResult> SearchKnowledgeAsync(Actor supplied,Guid conversation,string text,string language,DateTimeOffset now,CancellationToken ct)
    {
        using var activity=DemoTelemetry.Activities.StartActivity("knowledge.search");activity?.SetTag("language",language);
        if(embeddings is null)throw new RequestRejected(503,"SEMANTIC_RETRIEVAL_NOT_CONFIGURED");
        if(language is not("es" or "en")||string.IsNullOrWhiteSpace(text)||text.Length>4096)throw new RequestRejected(400,"INVALID_REQUEST");
        // Reject a stale/forged session and resource before spending inference budget.
        await Run(ct,(db,tx)=>{var actor=Require(db,tx,supplied,now);if(Resource(db,tx,actor,conversation,now) is null)throw new RequestRejected(404,"RESOURCE_NOT_FOUND");return true;});
        if(KnowledgeQueryScope.RequiresAbstention(text))return new("OUT_OF_SCOPE",[],null);
        var enteredAt=now;var elapsed=Stopwatch.StartNew();
        var query=(await embeddings.EmbedAsync([text],ct))[0];
        now=now.Add(elapsed.Elapsed);
        var ranked=await Run(ct,(db,tx)=>
        {
            var actor=Require(db,tx,supplied,now);if(Resource(db,tx,actor,conversation,now) is null)throw new RequestRejected(404,"RESOURCE_NOT_FOUND");
            var allowed=InternalAllowed(db,tx,actor,conversation,now);using var command=Command(db,tx,"""
              SELECT k.DocumentId,k.Version,k.Language,k.SectionId,k.Title,k.Content,k.ContentHash,v.Vector FROM KnowledgeVersion k
              JOIN KnowledgeRegistry r ON r.DocumentId=k.DocumentId AND r.ActiveVersion=k.Version
              JOIN KnowledgeVector v ON v.DocumentId=k.DocumentId AND v.Version=k.Version AND v.ContentHash=k.ContentHash
              JOIN KnowledgeIndexActive a ON a.GenerationId=v.GenerationId JOIN KnowledgeIndex i ON i.Id=a.GenerationId AND i.ModelId=@model
              WHERE k.TenantId=@tenant AND k.Language=@language AND k.Status='Published' AND k.EffectiveAt<=@now AND k.ExpiresAt>@now AND (k.Classification='Public' OR (k.Classification='Agent' AND @internal=1))
              """,("@model",embeddings.ModelId),("@tenant",actor.TenantId),("@language",language),("@internal",allowed?1:0),("@now",now));
            var candidates=new List<RankedKnowledge>();using var row=command.ExecuteReader();while(row.Read())
            {
                if(ConversationIngress.Hash(row.GetString(5))!=row.GetString(6))continue;
                var vector=OllamaEmbeddingProvider.Normalize(JsonSerializer.Deserialize<float[]>(row.GetString(7))!);
                var score=vector.Zip(query,(a,b)=>(double)a*b).Sum();
                candidates.Add(new(new(row.GetString(0),row.GetInt32(1),row.GetString(2),row.GetString(3),row.GetString(4),row.GetString(5)),score));
            }
            return candidates.OrderByDescending(item=>item.Score).ThenBy(item=>item.Evidence.DocumentId,StringComparer.Ordinal).Take(5).ToArray();
        });
        if(ranked.Length==0)return new("INDEX_UNAVAILABLE",ranked,null);
        // A similarity score is not confidence in an answer. The closed selector
        // can review weaker paraphrases, while the unreviewed mode stays stricter.
        var minimumScore=reranker is null?0.55:0.35;
        if(ranked[0].Score<minimumScore)return new("INSUFFICIENT_EVIDENCE",ranked,null);
        int? selected=0;
        if(reranker is not null)selected=await reranker.SelectAsync(text,language,ranked,ct);
        else if(ranked.Length>1&&ranked[0].Score-ranked[1].Score<0.015)selected=null;
        if(selected is null||selected<0||selected>=ranked.Length||ranked[selected.Value].Score<minimumScore)return new("INSUFFICIENT_EVIDENCE",ranked,null);
        var first=ranked[selected.Value].Evidence;now=enteredAt.Add(elapsed.Elapsed);
        var evidence=await ResolveAsync(supplied,conversation,first.DocumentId,first.Version,first.SectionId,now,ct);
        return new(evidence is null?"EVIDENCE_CHANGED":"GROUNDED_EXTRACT",ranked,evidence);
    }
    public async Task<KnowledgeEvidence?> SearchAsync(Actor actor,Guid conversation,string text,string topic,string language,DateTimeOffset now,CancellationToken ct)=>
        embeddings is null?await RetrieveAsync(actor,conversation,topic,language,now,ct):(await SearchKnowledgeAsync(actor,conversation,text,language,now,ct)).Selected;
}
