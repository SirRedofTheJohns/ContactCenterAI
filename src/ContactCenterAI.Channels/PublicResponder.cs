using ContactCenterAI.Application;
using ContactCenterAI.Infrastructure;

namespace ContactCenterAI.Channels;

// Approved FAQ evidence and an optional fixed, authenticated local booking bridge.
public sealed class PublicResponder(SqliteOperationalStore knowledge, IIntentProvider model, TimeProvider clock, ResortBridgeClient? resort = null)
{
    public async Task<PreparedOutput> AnswerAsync(ChannelJob job, CancellationToken ct)
    {
        var language = job.Route.Language;
        ChannelReply Answer(string code, string es, string en) => new(language == "en" ? en : es, code, []);
        if (job.Text is "/start" or "/en" or "/es")
            return new(Answer("CHANNEL_HELP", "Demo ficticia: pregunta por servicios, políticas, habitaciones o fechas disponibles. Para gestionar reservas vincula WhatsApp desde la página del resort. /en: inglés, /es: español, /human: cola humana simulada. No envíes datos personales ni de pago.", "Fictional demo: ask about services, policies, rooms or available dates. Link WhatsApp from the resort page to manage bookings. /en: English, /es: Spanish, /human: simulated human queue. Do not send personal or payment data."));
        if (KnowledgeQueryScope.RequiresAbstention(job.Text))
            return new(Answer("OUT_OF_SCOPE", "No tengo información aprobada para esa pregunta. Usa /human para la cola simulada.", "I have no approved information for that question. Use /human for the simulated queue."));
        if (resort is not null && job.Route.Channel == "whatsapp" && ResortBridgeClient.Recognizes(job.Text))
            return new(await resort.AnswerAsync(job, ct));
        var proposal = ProposalGateway.Validate(await model.ProposeAsync(job.Text, language, ct), language);
        if (proposal.Intent is "get_reservations" or "preview_cancellation")
            return new(Answer("VERIFIED_MEMBER_REQUIRED", "Las reservas se consultan y confirman en la demo web con login. Este canal no verifica una cuenta de miembro por teléfono o ID.", "Reservations require login in the web demo. This channel does not verify membership from a phone number or sender ID."));
        if (proposal.Intent == "request_handoff")
            return new(Answer("HANDOFF_REQUESTED", "Solicité la cola humana simulada. El bot queda en pausa; esta demo no conecta con un agente real del proveedor.", "I requested the simulated human queue. The bot pauses; this demo does not connect to a real provider agent."));
        if (proposal.Intent != "faq")
            return new(Answer("CLARIFICATION_REQUIRED", "Puedes preguntar por servicios y políticas o usar /human.", "You can ask about services and policies or use /human."));
        var guest = await knowledge.CreateGuestAsync(clock.GetUtcNow(), ct);
        var conversation = await new ConversationIngress(knowledge, clock).CreateAsync(guest, language, Guid.NewGuid().ToString("D"), ct);
        var evidence = await knowledge.SearchAsync(guest, conversation.ConversationId, job.Text, proposal.Topic!, language, clock.GetUtcNow(), ct);
        if (evidence is null) return new(Answer("NO_AUTHORIZED_EVIDENCE", "No encontré información pública aprobada. Puedes usar /human.", "I found no approved public evidence. You can use /human."));
        return new(new(evidence.Content, "GROUNDED_EXTRACT", [new(evidence.DocumentId, evidence.Version, evidence.SectionId, evidence.Title)]), guest, conversation.ConversationId);
    }
    public async Task<bool> StillValidAsync(PreparedOutput output, CancellationToken ct)
    {
        if (output.Reply.Citations.Count == 0) return true;
        if (output.Guest is null || output.ConversationId is null || output.Guest.PrincipalId is not null || output.Guest.MemberRef is not null || output.Guest.Roles != ContactCenterAI.Domain.ActorRoles.None) return false;
        foreach (var citation in output.Reply.Citations)
        {
            var evidence = await knowledge.ResolveAsync(output.Guest, output.ConversationId.Value, citation.DocumentId, citation.Version, citation.SectionId, clock.GetUtcNow(), ct);
            if (evidence is null || evidence.Content != output.Reply.Text || evidence.Title != citation.Title) return false;
        }
        return true;
    }
}
