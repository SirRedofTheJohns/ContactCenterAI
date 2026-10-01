using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ContactCenterAI.Application;
namespace ContactCenterAI.Infrastructure;

public sealed partial class SimulatedIntentProvider : IIntentProvider
{
    public string ProviderId => "simulated-intent-v1";
    public Task<string> ProposeAsync(string text, string language, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var query = Normalize(text); string intent = "clarify"; string? topic = null, reservation = null;
        if (Contains(query, "humano", "agente", "persona", "human", "representative", "speak to", "hablar con")) intent = "request_handoff";
        else if (Contains(query, "cancel", "anular"))
        {
            var match = Code().Match(text);
            if (match.Success && !Contains(query, "politica", "policy", "regla", "rule", "cuanto", "hours", "horas"))
            { intent = "preview_cancellation"; reservation = match.Value.ToUpperInvariant(); }
            else if (!match.Success && Contains(query, "politica", "policy", "regla", "rule", "cuanto", "hours", "horas", "plazo", "penalty")) { intent = "faq"; topic = "cancellation"; }
        }
        else if (Contains(query, "reserva", "reservation", "booking")) intent = "get_reservations";
        else if (Contains(query, "servicio", "service", "check-in", "check in", "llegada", "wifi", "desayuno", "breakfast")) { intent = "faq"; topic = "services"; }
        else if (Contains(query, "login", "sesion", "sign in", "identidad", "identity")) { intent = "faq"; topic = "identity"; }
        else if (Contains(query, "pago", "payment", "tarjeta", "card", "refund", "reembolso")) { intent = "faq"; topic = "payments"; }
        else if (query.Length > 25 && query is not ("si" or "yes")) { intent = "faq"; topic = "unknown"; }
        return Task.FromResult(JsonSerializer.Serialize(new IntentProposal(intent, language, reservation, topic), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
    private static bool Contains(string text, params string[] words) => words.Any(word => text.Contains(word, StringComparison.Ordinal));
    private static string Normalize(string value) => string.Concat(value.Normalize(NormalizationForm.FormD).Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)).ToLowerInvariant();
    [GeneratedRegex(@"\bRES-\d{3}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)] private static partial Regex Code();
}
public sealed class LocalContactCenterMock : IContactCenterAdapter
{
    public Task<HandoffAcknowledgment> RequestHandoffAsync(HandoffRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new HandoffAcknowledgment(request.RequestId, request.ConversationId, request.Epoch, true,
            Guid.Parse("10000000-0000-0000-0000-000000000003")));
    }
}
