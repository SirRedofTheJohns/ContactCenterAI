# Portfolio delivery 1 / Entrega 1 — 2026-10-27

Base `36018a62803c50c6335f6efe76e4cdfdaf96ef14` (main, 2026-10-06), working branch `fix/resort-web-interactions`, uncommitted at the time of writing. Host: Windows 11, .NET SDK 10.0.401 in ignored `.local/dotnet`, PowerShell 7.6.9 in ignored `.local/pwsh`, Python 3.12.5, Node 24.20. The Docker engine was not running. Earlier reports in this folder are historical and were not rewritten.

Status labels: **tested now** = run on this host on this date with the command shown. **Pending** = not run. **Historical** = earlier evidence, not repeated.

## Baseline: clean restore broken / restauración limpia rota

Tested now, before any change, using bash steps equivalent to `eng/Verify-Demo.ps1` because PowerShell 7 was not yet installed:

- Locked restore failed. NuGet audit, made fatal by `TreatWarningsAsErrors`, reported NU1902/NU1903 for `System.Security.Cryptography.Pkcs` 9.0.18, which comes in transitively from `Microsoft.Data.SqlClient` 7.1.1 in Infrastructure. The advisories are GHSA-2fmv-rv2c-59x7 (moderate) and GHSA-846f-f764-76mx (high). Both were published on 2026-10-13, after the base commit, and are patched in 9.0.21. Any fresh restore after that date fails, including CI.
- A diagnostic-only run with `-p:NuGetAudit=false` on the command line (repository unchanged) built the solution and passed 402/402 checks plus both evaluators. That isolated the dependency as the only blocker. It is not release verification.

## A — Dependency fix / corrección de dependencia

The dependency graph has one entry point. Only `ContactCenterAI.Infrastructure` references SqlClient; Api, Worker, Channels and nine check projects get Pkcs through project references. No SqlClient release newer than 7.1.1 exists. The fix is therefore a direct `System.Security.Cryptography.Pkcs` 9.0.21 reference in `ContactCenterAI.Infrastructure.csproj` only. That version is the lowest patched 9.x and adds no dependencies for net9.0+. Locks were regenerated with `restore --force-evaluate`. Only the 13 solution locks that already contained Pkcs changed, and only in the Pkcs entry. The `spikes/` projects are outside the solution and keep their historical locks.

Tested now: `.local/pwsh/pwsh.exe -NoProfile -File eng/Verify-Demo.ps1 -DotNetExe <repo>/.local/dotnet/dotnet.exe` exited 0. Locked restore ran with audit enabled. The Release build had 0 warnings and 0 errors. 402 PASS / 0 FAIL: Demo 17, Ingress 33, Source 22, Workflow 25, Assistant 36, LocalAi 28, Retrieval 58, Operations 9, Channel 83, Resort 70, Foundation 21. `demo_eval` 40/40 and `resort_eval` 28/28 PASS. No new C# checks were added.

## B — Resort web interactions / interacciones web del resort

All changes are in `wwwroot/resort.js`. The acceptance oracles are in the [contract](../contracts/resort-v0.12.md#web-ui-refinement--2026-10-27). No server, schema or authority change.

- A room picked before login is kept in tab `sessionStorage` and restored once after the fixed `/resort.html` OIDC return.
- Any new draft retires the visible quote, and late quote responses from older drafts are discarded.
- Logout no longer depends on unlink. A rejected logout reports the session as still active; a network error or 5xx reports sign-out as unverified.
- Times use `America/Santo_Domingo`.

Tested now: `node --test "tests/web/*.test.mjs"` gives 19/19 PASS. The same tests against the pre-fix `resort.js` (`RESORT_JS=<copy of HEAD version>`) give 1 PASS (the static login-form check) and 18 FAIL, each on the targeted behavior. These are isolated UI checks: the real script and markup run in a simulated DOM with a fake fetch, not a browser.

The full `Verify-Demo.ps1` run with A+B applied gave the same result as above: 402/402, both evaluators PASS, exit 0.

Verification rewrites tracked evidence files under `docs/progress/` and `evaluation/`. In each run those 12 generated files were copied outside the repository and then restored from git. Before restoring, the working tree was compared with its pre-run state, so no prior change was discarded.

## Still pending / pendiente

- Real browser walkthrough with Keycloak login: room pick → OIDC round trip → restored form; stale quote; logout with injected unlink failure. Not run because the Docker engine was unavailable. Until it is recorded, there is no end-to-end claim.
- `Start-Resort.ps1` and Keycloak startup on this host: not run.
- Remote CI: the check-run annotation for `local-contracts` at the base commit says the job was not started because the GitHub account is locked due to a billing issue. That is an account action, not a code defect. No remote run exists for this branch.
- The Node UI checks are not yet part of `Verify-Demo.ps1` or the CI workflow.

## Scope notes / notas de alcance

- The original v0.10 profile (`/`) has a web assistant UI with simulated or pinned local-model AI/RAG. The resort (`/resort.html`) has catalog, calendar and forms but no web chat. Its conversation exists only through WhatsApp and uses deterministic interpretation (`ResortLanguage` rules/regex), not a model. A resort web chat is a later delivery.
- Out of this delivery: the web chat, the portable profile, the hard-coded WhatsApp help example date (`ResortApi.cs`), default-guest clarification, receipt lookup after a lost response, docs/status cleanup and licence.
