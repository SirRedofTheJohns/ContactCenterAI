using System.Globalization;
using System.Text.RegularExpressions;

namespace ContactCenterAI.Application;

public sealed record RoomCategory(string Id, string NameEs, string NameEn, int Capacity, long NightlyCents, string[] AmenitiesEs, string[] AmenitiesEn);
public sealed record ResortRequest(string Action, string? StayId = null, string? Type = null, string? Arrival = null, string? Departure = null, int Guests = 2);
public sealed record ResortStay(string Id, string Type, string Arrival, string Departure, int Guests, long TotalCents, string Status, long Version);
public sealed record ResortOffer(Guid Id, ResortRequest Request, long PreviousTotalCents, long TotalCents, string Currency, DateTimeOffset ExpiresAt, string Status);
public sealed record ResortReceipt(Guid CommandId, string Status, string ReasonCode, ResortStay? Stay);
public sealed record ResortChannelRequest(string Route, long Epoch, string Endpoint, string Channel, string Text, string EventKey, string Language);
public sealed record ResortChannelResult(string Text, string Code);
public sealed record ResortBlockRequest(string Unit, string Arrival, string Departure);
public sealed record ResortConfirm(string Decision);
public sealed record ResortChatRequest(string Text, string Language);
public sealed record ResortAvailability(string Type, long NightlyCents, int Capacity, string[] Ranges);
// One structured conversation turn shared by the web chat and the WhatsApp bridge. Text is presentation only;
// authority comes from the server-resolved Actor, never from the interpretation.
public sealed record ResortTurn(string Code, string Text, string Mode, ResortRequest? Interpreted, string[] Missing, string[] Assumptions,
    ResortOffer? Offer = null, ResortStay[]? Stays = null, IReadOnlyList<RoomCategory>? Catalog = null, ResortAvailability[]? Availability = null,
    ResortReceipt? Receipt = null, string[]? Suggestions = null, int? StatedGuests = null);
// Untrusted proposal from an interpreter: either the deterministic parser or a local model.
public sealed record ResortInterpretation(ResortRequest? Request, int? Guests, bool GuestsAmbiguous, string Source, string? Error = null);
public interface IResortInterpreter
{
    string Mode { get; }
    string ProviderId { get; }
    Task<ResortInterpretation> InterpretAsync(string sanitizedText, string language, DateOnly today, CancellationToken ct);
}
public sealed class DeterministicResortInterpreter : IResortInterpreter
{
    public string Mode => "deterministic";
    public string ProviderId => "resort-rules-v0.12";
    public Task<ResortInterpretation> InterpretAsync(string sanitizedText, string language, DateOnly today, CancellationToken ct) =>
        Task.FromResult(Interpret(sanitizedText, new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)));
    public static ResortInterpretation Interpret(string text, DateTimeOffset now)
    {
        var (guests, ambiguous) = ResortLanguage.GuestMention(text);
        var parsed = ResortLanguage.Parse(text, now);
        if (parsed is null && ResortLanguage.IsPolicyQuestion(text)) parsed = new("policy");
        else if (ResortLanguage.ActionWithoutReference(text) is string action && parsed?.Action is not ("change" or "cancel")) parsed = new(action, null, parsed?.Type, parsed?.Arrival, parsed?.Departure);
        return new(parsed, guests, ambiguous, "resort-rules-v0.12");
    }
}

// This parser is a bounded demo interpreter, not an unrestricted language model.
public static partial class ResortLanguage
{
    public static string SanitizeChannelText(string text)
    {
        // Protect closed command identifiers/dates from the general phone-number mask.
        // Payment/secret/email policy still applies to all remaining user text.
        if (text.Contains("CCAIRESORTPLACEHOLDER", StringComparison.Ordinal)) throw new RequestRejected(400, "INVALID_REQUEST");
        if (LinkCommand(text)) return text.Trim();
        var values = new Dictionary<string, string>();
        string Keep(string value) { var marker = "CCAIRESORTPLACEHOLDER" + new string('Z', values.Count + 1); values.Add(marker, value); return marker; }
        var safe = Dates().Replace(text, m => Keep(m.Value));
        if (Confirmation(text) is Guid id) safe = safe.Replace(id.ToString(), Keep(id.ToString()), StringComparison.OrdinalIgnoreCase);
        safe = ConversationIngress.SanitizeText(safe);
        foreach (var pair in values.Reverse()) safe = safe.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
        return safe;
    }
    public static string? LanguageControl(string text) => text.Trim().ToLowerInvariant() switch
    {
        "/es" or "\\es" or "\\es:" or "en español" or "en espanol" or "que sea en español" or "que sea en espanol" or "háblame en español" or "hablame en espanol" => "es",
        "/en" or "in english" or "speak english" or "háblame en inglés" or "hablame en ingles" => "en",
        _ => null
    };
    public static bool LinkCommand(string text) => Link().IsMatch(text.Trim());
    public static string? LinkCode(string text) { var m = Link().Match(text.Trim()); return m.Success ? m.Groups[1].Value : null; }
    public static Guid? Confirmation(string text) { var m = Confirm().Match(text.Trim()); return m.Success && Guid.TryParse(m.Groups[1].Value, out var id) ? id : null; }
    public static bool IsConfirmation(string text) => Regex.IsMatch(text, @"^\s*(confirmar|confirm|sí|si|yes)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static ResortRequest? Parse(string text, DateTimeOffset now)
    {
        var q = text.ToLowerInvariant();
        if (q.Contains("política") || q.Contains("politica") || q.Contains("policy") || q.Contains("regla")) return null;
        var code = Stay().Match(text); var id = code.Success ? code.Value.ToUpperInvariant() : null;
        var type = q.Contains("suite") || q.Contains("jacuzzi") ? "suite" : q.Contains("deluxe") ? "deluxe" : q.Contains("estándar") || q.Contains("estandar") || q.Contains("standard") ? "standard" : null;
        var action = (q.Contains("cancel") || q.Contains("anular")) && id is not null ? "cancel" :
            (q.Contains("cambia") || q.Contains("change") || q.Contains("mover") || q.Contains("reschedul")) && id is not null ? "change" :
            q.Contains("mis reservas") || q.Contains("my bookings") || q.Contains("my reservations") ? "list" :
            q.Contains("disponib") || q.Contains("available") || q.Contains("fechas libres") ? "availability" :
            q.Contains("reservar") || q.StartsWith("reserva ", StringComparison.Ordinal) || q.Contains("book ") ? "create" :
            type is not null || q.Contains("habitacion") || q.Contains("habitación") || q.Contains("rooms") || q.Contains("precios") || q.Contains("prices") || q.Contains("amenidad") ? "catalog" : null;
        if (action is null) return null;
        var dates = Dates().Matches(text).Select(m => m.Value).ToArray();
        string? arrival = dates.Length == 2 ? dates[0] : null, departure = dates.Length == 2 ? dates[1] : null;
        if (dates.Length == 0)
        {
            var range = NamedRange().Match(q);
            if (range.Success)
            {
                var months = new[] { "enero|january", "febrero|february", "marzo|march", "abril|april", "mayo|may", "junio|june", "julio|july", "agosto|august", "septiembre|september", "octubre|october", "noviembre|november", "diciembre|december" };
                var month = Array.FindIndex(months, v => v.Split('|').Contains(range.Groups[3].Value)) + 1;
                if (month > 0 && int.TryParse(range.Groups[4].Value, out var year) && year >= 2000 && year <= 2100)
                {
                    try { arrival = new DateOnly(year, month, int.Parse(range.Groups[1].Value, CultureInfo.InvariantCulture)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); departure = new DateOnly(year, month, int.Parse(range.Groups[2].Value, CultureInfo.InvariantCulture)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
                    catch (ArgumentOutOfRangeException) { }
                }
            }
        }
        var guest = Guests().Match(q); var guests = guest.Success ? int.Parse(guest.Groups[1].Value, CultureInfo.InvariantCulture) : 2;
        return new(action, id, type, arrival, departure, guests);
    }
    // Parse keeps its v0.12 default of two guests for the declared examples; the conversation asks instead of assuming.
    public static (int? Count, bool Ambiguous) GuestMention(string text)
    {
        var counts = GuestCount().Matches(text.ToLowerInvariant()).Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).Distinct().ToArray();
        var hedged = GuestRange().IsMatch(text.ToLowerInvariant());
        return counts.Length == 1 && !hedged ? (counts[0], false) : (null, counts.Length > 1 || hedged);
    }
    public static bool IsPolicyQuestion(string text) { var q = text.ToLowerInvariant(); return q.Contains("política") || q.Contains("politica") || q.Contains("policy") || q.Contains("regla") || q.Contains("rules"); }
    // Change/cancel intent without a STAY reference: Parse cannot act on it, so the conversation asks which booking.
    public static string? ActionWithoutReference(string text)
    {
        var q = text.ToLowerInvariant(); if (Stay().IsMatch(text)) return null;
        return q.Contains("cancel") || q.Contains("anular") ? "cancel" : q.Contains("cambia") || q.Contains("change") || q.Contains("mover") || q.Contains("reschedul") ? "change" : null;
    }
    public static int DateCount(string text) => Dates().Matches(text).Count;
    [GeneratedRegex(@"\b(\d{1,2})\s*(?:personas?|hu[eé]spedes?|guests?|people|adult(?:o|e)?s?)\b", RegexOptions.CultureInvariant, 100)] private static partial Regex GuestCount();
    [GeneratedRegex(@"\b\d{1,2}\s*(?:o|or|a|to|-|–)\s*\d{1,2}\s*(?:personas?|hu[eé]spedes?|guests?|people|adult(?:o|e)?s?)\b", RegexOptions.CultureInvariant, 100)] private static partial Regex GuestRange();
    [GeneratedRegex(@"^(?:vincular|link)\s+([a-fA-F0-9]{32})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)] private static partial Regex Link();
    [GeneratedRegex(@"^(?:confirmar|confirm)\s+([a-fA-F0-9-]{36})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)] private static partial Regex Confirm();
    [GeneratedRegex(@"\bSTAY-[A-F0-9]{8}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)] private static partial Regex Stay();
    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}\b", RegexOptions.CultureInvariant, 100)] private static partial Regex Dates();
    [GeneratedRegex(@"(?:del|from)\s+(\d{1,2})\s+(?:al|to)\s+(\d{1,2})\s+(?:de\s+)?([a-z]+)\s+(?:de\s+)?(\d{4})", RegexOptions.CultureInvariant, 100)] private static partial Regex NamedRange();
    [GeneratedRegex(@"\b(?:para|for)\s+(\d{1,2})\s*(?:personas?|hu[eé]spedes?|guests?|people)\b", RegexOptions.CultureInvariant, 100)] private static partial Regex Guests();
}
