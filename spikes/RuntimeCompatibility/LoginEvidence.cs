using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

// Sanitized observations from the real OIDC middleware. No token/cookie values or passwords.
internal sealed class LoginEvidence(string runId, string directory)
{
    private readonly object sync = new();
    private readonly Dictionary<string, AccountEvidence> accounts = new(StringComparer.Ordinal);
    private readonly JsonSerializerOptions json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    internal string RunId { get; } = runId;
    internal static string? Property(AuthenticationProperties? properties, string key)
        => properties is not null && properties.Items.TryGetValue(key, out var value) ? value : null;

    internal static string? ExpectedSubject(string? account) => account switch
    {
        "customer-a" => "10000000-0000-0000-0000-000000000001",
        "customer-b" => "10000000-0000-0000-0000-000000000002",
        _ => null
    };

    internal void Challenge(string account, bool code, bool s256, bool callback)
    {
        lock (sync)
        {
            accounts[account] = new AccountEvidence(account, ExpectedSubject(account)!, code, s256, callback);
            Save();
        }
    }

    internal void CodeReceived(string? account, bool present, bool callback)
        => Update(account, row => row with { CodeReceived = present, CallbackObserved = callback });

    internal bool Validated(string? account, ClaimsPrincipal? principal, bool issuer)
    {
        var expected = ExpectedSubject(account);
        var valid = expected is not null && principal?.FindFirstValue("sub") == expected && principal.IsInRole("Customer") && issuer;
        Update(account, row => row with { IdTokenValidated = valid, CustomerRole = valid });
        return valid;
    }

    internal void Cookie(string? account, IEnumerable<string?> headers)
    {
        // Inspect only attribute names. Never serialize a header or cookie value.
        var attributes = headers.Where(h => h?.StartsWith("ccai.b01.diagnostic=", StringComparison.Ordinal) == true)
            .SelectMany(h => h!.Split(';').Skip(1)).Select(a => a.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Update(account, row => row with
        {
            CookieHttpOnly = attributes.Contains("httponly"),
            CookieSecure = attributes.Contains("secure"),
            CookieSameSiteLax = attributes.Contains("samesite=lax")
        });
    }

    internal bool Proof(string? account, ClaimsPrincipal principal, bool tokensAbsent)
    {
        lock (sync)
        {
            if (account is null || !accounts.TryGetValue(account, out var row)) return false;
            row = row with { CookieRoundtrip = principal.FindFirstValue("sub") == row.Subject && principal.IsInRole("Customer"), TokensAbsent = tokensAbsent };
            accounts[account] = row;
            Save();
            return row.Passed;
        }
    }

    internal void Failure(string? account) => Update(account, row => row with { Failed = true });

    internal void Ready(int processId, string thumbprint)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "ready.json"), JsonSerializer.Serialize(new { RunId, processId, certificateThumbprint = thumbprint }, json));
    }

    private void Update(string? account, Func<AccountEvidence, AccountEvidence> change)
    {
        lock (sync)
        {
            if (account is not null && accounts.TryGetValue(account, out var row))
            {
                accounts[account] = change(row);
                Save();
            }
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "results.json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(new
        {
            schemaVersion = 1, RunId, observedAtUtc = DateTimeOffset.UtcNow,
            scope = "B01 real OIDC middleware and HTTPS cookie roundtrip; no product resource authorization",
            accounts = accounts.Values.OrderBy(row => row.Account).ToArray()
        }, json));
        File.Move(path + ".tmp", path, overwrite: true);
    }

    private sealed record AccountEvidence(string Account, string Subject, bool AuthorizationCodeFlow, bool PkceS256, bool ExactCallback)
    {
        public bool CodeReceived { get; init; }
        public bool CallbackObserved { get; init; }
        public bool IdTokenValidated { get; init; }
        public bool CustomerRole { get; init; }
        public bool CookieHttpOnly { get; init; }
        public bool CookieSecure { get; init; }
        public bool CookieSameSiteLax { get; init; }
        public bool CookieRoundtrip { get; init; }
        public bool TokensAbsent { get; init; }
        public bool Failed { get; init; }
        public bool Passed => !Failed && AuthorizationCodeFlow && PkceS256 && ExactCallback && CodeReceived && CallbackObserved
            && IdTokenValidated && CustomerRole && CookieHttpOnly && CookieSecure && CookieSameSiteLax && CookieRoundtrip && TokensAbsent;
    }
}
