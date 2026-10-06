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
    [GeneratedRegex(@"^(?:vincular|link)\s+([a-fA-F0-9]{32})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)] private static partial Regex Link();
    [GeneratedRegex(@"^(?:confirmar|confirm)\s+([a-fA-F0-9-]{36})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)] private static partial Regex Confirm();
    [GeneratedRegex(@"\bSTAY-[A-F0-9]{8}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)] private static partial Regex Stay();
    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}\b", RegexOptions.CultureInvariant, 100)] private static partial Regex Dates();
    [GeneratedRegex(@"(?:del|from)\s+(\d{1,2})\s+(?:al|to)\s+(\d{1,2})\s+(?:de\s+)?([a-z]+)\s+(?:de\s+)?(\d{4})", RegexOptions.CultureInvariant, 100)] private static partial Regex NamedRange();
    [GeneratedRegex(@"\b(?:para|for)\s+(\d{1,2})\s*(?:personas?|hu[eé]spedes?|guests?|people)\b", RegexOptions.CultureInvariant, 100)] private static partial Regex Guests();
}
