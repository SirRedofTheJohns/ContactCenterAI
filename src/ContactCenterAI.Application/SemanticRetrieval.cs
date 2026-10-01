namespace ContactCenterAI.Application;

public interface ITextEmbeddingProvider
{
    string ModelId { get; }
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct);
}
public sealed record RankedKnowledge(KnowledgeEvidence Evidence,double Score);
public sealed record RetrievalResult(string ReasonCode,IReadOnlyList<RankedKnowledge> Candidates,KnowledgeEvidence? Selected);
public interface IKnowledgeReranker
{Task<int?> SelectAsync(string text,string language,IReadOnlyList<RankedKnowledge> candidates,CancellationToken ct);}
