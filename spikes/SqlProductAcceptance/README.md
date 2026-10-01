# .NET SQL product acceptance

Focused diagnostic for the existing `SqlOperationalStore`, using the ignored local restricted credential. Mandatory encryption is preserved; `TrustServerCertificate` remains limited to the synthetic loopback server, as in ADR-011. Does not run DDL, change bindings, rotate credentials or create conversations.

`eng/Invoke-SqlAcceptance.ps1` runs native networking and, if it fails, the [official managed diagnostic switch](https://learn.microsoft.com/en-us/sql/connect/ado-net/appcontext-switches?view=sql-server-ver17). Managed networking is a testing/debugging path, not a production fix. Reports contain safe codes and no connection string, tokens, private key or full exception text.

These checks do not close product HTTPS/OIDC, global revocation, concurrency, worker/retention or SQL cancellation gates. In the agent profile observed on 2026-10-01, native error 20 and the managed SChannel credential failure block the encrypted connection; see the current progress report.
