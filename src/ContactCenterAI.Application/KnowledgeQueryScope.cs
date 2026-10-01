using System.Text.RegularExpressions;
namespace ContactCenterAI.Application;

// Scope rejection is advisory input filtering; server authorization remains the
// authority. Patterns intentionally cover explicit requests, not all attacks.
public static partial class KnowledgeQueryScope
{
    public static bool RequiresAbstention(string text)
    {
        if(text.Length>4096)return true;
        try{return Medical().IsMatch(text)||RefundAction().IsMatch(text)||PrivateSecret().IsMatch(text)||Override().IsMatch(text)||NamedVenue().Matches(text).Any(m=>!m.Groups["venue"].Value.Equals("Caribbean Horizon",StringComparison.OrdinalIgnoreCase));}
        catch(RegexMatchTimeoutException){return true;}
    }
    [GeneratedRegex(@"\b(?:dosis|dosage|dose|antibi[oó]tic\p{L}*|prescri\p{L}*|diagnos\p{L}*)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,100)]
    private static partial Regex Medical();
    [GeneratedRegex(@"\b(?:refund|reembolsa\p{L}*|devuelv\p{L}*|devolv\p{L}*|return)\b.{0,60}\b(?:my money|mi dinero|el dinero|me)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,100)]
    private static partial Regex RefundAction();
    [GeneratedRegex(@"\b(?:private service key|private key|administrator passwords?|admin passwords?|clave privada|contraseñas? de administrador|secretos? internos?)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,100)]
    private static partial Regex PrivateSecret();
    [GeneratedRegex(@"\b(?:ignore|ignora|override|anula|omite)\b.{0,80}\b(?:instructions?|rules?|instrucciones|reglas|policy|política)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,100)]
    private static partial Regex Override();
    [GeneratedRegex(@"\b(?i:hotel|resort)\s+(?<venue>[A-ZÁÉÍÓÚÑ][\p{L}'-]{1,29}(?:\s+[A-ZÁÉÍÓÚÑ][\p{L}'-]{1,29}){0,3})\b",RegexOptions.CultureInvariant,100)]
    private static partial Regex NamedVenue();
}
