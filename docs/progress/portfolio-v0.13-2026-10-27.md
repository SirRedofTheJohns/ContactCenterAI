# Portfolio v0.13 / Demo independiente — 2026-10-27

Branch `feat/portable-demo` (from `fix/resort-web-interactions`), local commits only: nothing was pushed, merged or published. This note records what was **tested now**. Earlier notes in this folder are historical and were not rewritten. Delivery 1 (dependency fix and UI corrections) is in [portfolio-2026-10-27.md](portfolio-2026-10-27.md).

Host: Windows 11, Docker Desktop (engine 27.4.0), .NET SDK 10.0.401 and PowerShell 7.6.9 in the ignored `.local/`, Node 24.20.0, Chrome and Edge for headless browsing.

## What changed

- **Docker demo** (`deploy/demo`, `start-demo.cmd`, `stop-demo.cmd`, `reset-demo.cmd`, `demo.sh`): app, Keycloak and SQLite volumes, idempotent secret initialization, loopback-only ports, sign-in on 18080 (configurable). The visitor's host needs only Docker.
- **Web chat** over the same turn logic as the WhatsApp bridge, with clarification, policy and out-of-scope answers, structured turns, and a [v0.13 contract](../contracts/resort-v0.13.md).
- **Optional local AI interpreter** with a closed schema and no fallback.
- **Chat-first UI**: proposal summary, receipt lookup, scenarios, lab fault, technical panel, keyboard and mobile.
- **Docs**: [demo guide](../demo-guide.en.md), bilingual README, interview script, changelog and release notes, plus status notes on historical guides.

## Results

| Check | Command / method | Commit | Result |
|---|---|---|---|
| Full regression | `pwsh eng/Verify-Demo.ps1 -DotNetExe .local/dotnet/dotnet.exe` | `993b7db` | Exit 0. Locked restore with NuGet audit, Release build with 0 warnings and 0 errors. **446 PASS / 0 FAIL**: the original 402 plus 44 `ResortChatChecks`. `demo_eval` 40/40, `resort_eval` 28/28 |
| UI checks | `node --test "tests/web/*.test.mjs"` | `993b7db` | **28/28** |
| Later code change (localized stay status, text only) | `ResortChatChecks` + `ResortChecks` | `95eda1b` | 44/44 and 70/70 |
| Real browser walkthrough, phase A | Package `ContactCenterAI-demo-v0.13-95eda1b9.zip` extracted to an empty folder, `start-demo.cmd`, headless Chrome over CDP | `95eda1b` | **16/16** |
| Restart and phase B | `stop-demo.cmd`, `start-demo.cmd`, then the same driver | `95eda1b` | **3/3** |

Walkthrough details: [portfolio-v0.13-walkthrough.json](portfolio-v0.13-walkthrough.json).

- **Phase A** covered page load and the deterministic mode badge, a catalog question, a visitor booking request answered with sign-in required, sign-in through Keycloak from the chat with the typed message restored, and a proposal without a booking. Then: confirm, repeated confirm returning the same receipt, a change onto occupied nights rejected with the original kept, a change to free dates with guests kept, a cancellation, `customer-b`'s booking denied, the lab fault recovered from the same receipt, an ambiguous guest count asked, an out-of-scope question declined, the technical panel without the CSRF token, the English UI and chat, and logout (afterwards `/v2/resort/stays` returns 401).
- **Phase B** covered bookings still present after the restart, the full keyboard path (skip link → chat input → Enter sends → focus on the proposal → Tab to Confirm → Enter executes) and the mobile viewport at 390 px (`scrollWidth` 390, 7-day calendar, chat usable).

Screenshots from that run: [desktop](screenshots/resort-v0.13-desktop.png) and [mobile](screenshots/resort-v0.13-mobile.png).

Notes on the runs:

- One earlier attempt ran phase A twice on the same data by mistake. Its second pass failed one step because the first pass had already booked those nights (`DATES_OCCUPIED`, correct behavior). The data was reset and each phase was run once for the results above.
- The commit that adds this note changes only documentation and screenshots relative to `95eda1b`.
- `Verify-Demo.ps1` rewrote 12 tracked historical evidence files. Copies were kept outside the repository and the files were restored from git; nothing was overwritten in the commits.

## Pending / not done

- **Local AI inference** for the resort chat was not run: Ollama on this host has no models, and nothing was downloaded. The contract is covered by fixtures, including explicit errors when the model is missing or unreachable. No quality figures exist for this chat.
- **Remote CI**: the workflow now runs the Node checks and builds the demo image, but no remote run exists because the GitHub account is locked for billing.
- **Other platforms**: macOS, Linux and ARM hosts, and other browsers beyond headless Chrome and Edge, were not tested.
- **Not attempted** in this delivery: a screen reader pass and zoom at 200 %.
- **Licence**: the owner has not chosen one.
- Chat answers keep the language in which they were sent; switching language does not re-translate earlier bubbles.
