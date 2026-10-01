using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ContactCenterAI.Domain;
namespace ContactCenterAI.Application;

public sealed record IntentProposal(string Intent, string Language, string? ReservationId, string? Topic);
public interface IIntentProvider { string ProviderId { get; } Task<string> ProposeAsync(string sanitizedText, string language, CancellationToken ct); }
public static partial class ProposalGateway
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static IntentProposal Validate(string raw, string language)
    {
        IntentProposal proposal;
        try { if (raw.Length > 2048) throw new JsonException(); proposal = JsonSerializer.Deserialize<IntentProposal>(raw, Json) ?? throw new JsonException(); }
        catch (JsonException) { throw new RequestRejected(400, "MODEL_SCHEMA_REJECTED"); }
        if (proposal.Language != language || proposal.Intent is not ("faq" or "get_reservations" or "preview_cancellation" or "request_handoff" or "clarify"))
            throw new RequestRejected(400, "MODEL_TOOL_REJECTED");
        if (proposal.Intent == "preview_cancellation")
        { if (proposal.ReservationId is null || !ReservationId().IsMatch(proposal.ReservationId) || proposal.Topic is not null) throw new RequestRejected(400, "MODEL_ARGUMENTS_REJECTED"); }
        else if (proposal.ReservationId is not null) throw new RequestRejected(400, "MODEL_ARGUMENTS_REJECTED");
        if (proposal.Intent == "faq")
        { if (proposal.Topic is not ("cancellation" or "services" or "identity" or "payments" or "agent" or "unknown")) throw new RequestRejected(400, "MODEL_ARGUMENTS_REJECTED"); }
        else if (proposal.Topic is not null) throw new RequestRejected(400, "MODEL_ARGUMENTS_REJECTED");
        return proposal;
    }
    [GeneratedRegex(@"\ARES-\d{3}\z", RegexOptions.CultureInvariant, 100)] private static partial Regex ReservationId();
}
public sealed record KnowledgeEvidence(string DocumentId, int Version, string Language, string SectionId, string Title, string Content);
public sealed record CitationRef(string DocumentId, int Version, string SectionId, string Title);
public interface IKnowledgeStore
{
    Task<KnowledgeEvidence?> RetrieveAsync(Actor actor, Guid conversation, string topic, string language, DateTimeOffset now, CancellationToken ct);
    Task<KnowledgeEvidence?> SearchAsync(Actor actor, Guid conversation, string text, string topic, string language, DateTimeOffset now, CancellationToken ct) => RetrieveAsync(actor,conversation,topic,language,now,ct);
    Task<KnowledgeEvidence?> ResolveAsync(Actor actor, Guid conversation, string document, int version, string section, DateTimeOffset now, CancellationToken ct);
    Task RecordFeedbackAsync(Actor actor, Guid conversation, Guid turn, string reason, DateTimeOffset now, CancellationToken ct);
}
public sealed record AssistantAnswer(string Text, string Intent, string ReasonCode, IReadOnlyList<CitationRef> Citations, string ProviderId);
public sealed record AssistantTurn(Guid TurnId, AssistantAnswer Answer);
public sealed record AssistantSnapshot(string Ownership, long Epoch, long Version, IReadOnlyList<AssistantTurn> Turns, int PendingTurns);
public sealed record TurnLease(Guid TurnId, Guid LeaseId, Guid ConversationId, Actor Actor, string Text, string Language, long Version, long Epoch);
public interface IAssistantLedger
{
    Task<TurnLease?> ClaimTurnAsync(DateTimeOffset now, CancellationToken ct);
    Task SaveAnswerAsync(TurnLease job, AssistantAnswer answer, DateTimeOffset now, CancellationToken ct);
    Task<AssistantSnapshot> ReadAnswersAsync(Actor actor, Guid conversation, DateTimeOffset now, CancellationToken ct);
}
public sealed record HandoffRequest(Guid RequestId, Guid ConversationId, long Epoch, string Language, string ReasonCode);
public sealed record HandoffAcknowledgment(Guid RequestId, Guid ConversationId, long Epoch, bool Accepted, Guid? AssignedAgent);
public interface IContactCenterAdapter
{ Task<HandoffAcknowledgment> RequestHandoffAsync(HandoffRequest request, CancellationToken ct); }
public interface IHandoffStore
{
    Task<HandoffRequest> RequestAsync(Actor actor, Guid conversation, Guid? turn, string reason, DateTimeOffset now, CancellationToken ct);
    Task<HandoffRequest?> ClaimHandoffAsync(DateTimeOffset now, CancellationToken ct);
    Task ApplyAcknowledgmentAsync(HandoffAcknowledgment acknowledgment, DateTimeOffset now, CancellationToken ct);
}
public static class DemoTelemetry
{
    public static readonly ActivitySource Activities = new("ContactCenterAI.Workflow", "0.5.0");
    private static readonly Meter Meter = new("ContactCenterAI.Workflow", "0.5.0");
    public static readonly Counter<long> Turns = Meter.CreateCounter<long>("ccai.turns");
    public static readonly Counter<long> ToolDenials = Meter.CreateCounter<long>("ccai.tool.denials");
}
public sealed class AssistantProcessor(IAssistantLedger ledger, IIntentProvider model, IKnowledgeStore knowledge,
    IReservationSource source, CancellationWorkflow cancellation, IHandoffStore handoff, TimeProvider clock)
{
    public async Task<bool> RunOnceAsync(CancellationToken stopping)
    {
        var job = await ledger.ClaimTurnAsync(clock.GetUtcNow(), stopping); if (job is null) return false;
        using var activity = DemoTelemetry.Activities.StartActivity("assistant.turn");
        activity?.SetTag("turn.id", job.TurnId.ToString()); activity?.SetTag("language", job.Language);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopping); deadline.CancelAfter(TimeSpan.FromSeconds(8));
        AssistantAnswer answer;
        try
        {
            var proposal = ProposalGateway.Validate(await model.ProposeAsync(job.Text, job.Language, deadline.Token), job.Language);
            answer = await ExecuteAsync(job, proposal, deadline.Token);
        }
        catch (Exception error) when (error is RequestRejected || error is OperationCanceledException && !stopping.IsCancellationRequested || error is HttpRequestException)
        {
            var code = error is RequestRejected rejection ? rejection.Code : "ASSISTANT_DEADLINE";
            DemoTelemetry.ToolDenials.Add(1, new KeyValuePair<string, object?>("code", code));
            answer = Answer(job, "fallback", code, "No pude completar la consulta. Puedes revisar Mis reservas o solicitar atención humana.", "I could not complete this request. You can check My reservations or ask for a human.");
        }
        await ledger.SaveAnswerAsync(job, answer, clock.GetUtcNow(), stopping);
        DemoTelemetry.Turns.Add(1, new KeyValuePair<string, object?>("intent", answer.Intent));
        return true;
    }
    private AssistantAnswer Answer(TurnLease job, string intent, string code, string es, string en, IReadOnlyList<CitationRef>? citations = null) =>
        new(job.Language == "en" ? en : es, intent, code, citations ?? [], model.ProviderId);
    private async Task<AssistantAnswer> ExecuteAsync(TurnLease job, IntentProposal proposal, CancellationToken ct)
    {
        var state = await ledger.ReadAnswersAsync(job.Actor, job.ConversationId, clock.GetUtcNow(), ct);
        if (state.Ownership != "AI" || state.Epoch != job.Epoch) throw new RequestRejected(409, "HUMAN_OWNS_CONVERSATION");
        switch (proposal.Intent)
        {
            case "faq":
                var evidence = await knowledge.SearchAsync(job.Actor, job.ConversationId, job.Text, proposal.Topic!, job.Language, clock.GetUtcNow(), ct);
                if (evidence is null) return Answer(job, "faq", "NO_AUTHORIZED_EVIDENCE", "No tengo información aprobada para esa pregunta. Puedes solicitar atención humana.", "I have no approved information for that question. You can request human assistance.");
                return Answer(job, "faq", "GROUNDED_EXTRACT", evidence.Content, evidence.Content, [new(evidence.DocumentId, evidence.Version, evidence.SectionId, evidence.Title)]);
            case "get_reservations":
                if (job.Actor.MemberRef is null || !job.Actor.Roles.HasFlag(ActorRoles.Customer)) throw new RequestRejected(403, "VERIFIED_CUSTOMER_REQUIRED");
                var items = await source.ListAsync(job.Actor.MemberRef, ct);
                var facts = string.Join("; ", items.Select(item => item.ReservationId + ": " + (job.Language == "en" ? item.Status.ToString() : item.Status == ReservationStatus.Confirmed ? "confirmada" : "cancelada") + ", " + item.CheckInUtc.ToString("yyyy-MM-dd HH:mm 'UTC'")));
                return Answer(job, "get_reservations", "SOURCE_READ", "Tus reservas: " + facts + ". Pulsa Mis reservas para ver las opciones.", "Your reservations: " + facts + ". Open My reservations to see your options.");
            case "preview_cancellation":
                var offer = await cancellation.PreviewAsync(job.Actor, job.ConversationId, proposal.ReservationId!, job.Version, ct);
                return Answer(job, "preview_cancellation", "OFFER_READY", "Preparé una propuesta para " + offer.ReservationId + ". Revisa el panel y pulsa Confirmar si deseas continuar. La reserva todavía no se ha cancelado.", "I prepared an offer for " + offer.ReservationId + ". Review it and press Confirm if you wish to proceed. The reservation has not been cancelled yet.");
            case "request_handoff":
                _ = await handoff.RequestAsync(job.Actor, job.ConversationId, job.TurnId, "CUSTOMER_REQUEST", clock.GetUtcNow(), ct);
                return Answer(job, "request_handoff", "HANDOFF_REQUESTED", "Solicité atención humana en el contact center simulado. La conexión se confirma cuando llega su aceptación.", "I requested human assistance in the simulated contact center. Connection is confirmed only after acknowledgment.");
            default:
                return Answer(job, "clarify", "CLARIFICATION_REQUIRED", "Puedo explicar la política, consultar tus reservas o preparar una cancelación. Para cancelar indica el código, por ejemplo RES-001. Decir «sí» en el chat no confirma: utiliza el botón de la propuesta.", "I can explain the policy, read your reservations or prepare a cancellation. Include a reservation code, for example RES-001. Typing yes does not confirm: use the offer button.");
        }
    }
}
