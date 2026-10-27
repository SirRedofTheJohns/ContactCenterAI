# Demo guide

[Español](demo-guide.es.md) · [README](../README.md)

This is the current guide for installing and using the resort demo (v0.13). Older setup and walkthrough pages are kept as history and say so at the top.

## Install and start

You need Docker Desktop, or Docker Engine with Compose v2, and Internet access the first time. From the repository root:

| Action | Windows | macOS / Linux |
|---|---|---|
| Start (build on first run) | `start-demo.cmd` | `./demo.sh start` |
| Stop, keeping data | `stop-demo.cmd` | `./demo.sh stop` |
| Status / logs | `docker compose -f deploy/demo/compose.yaml ps` / `logs` | `./demo.sh status` / `./demo.sh logs` |
| Delete this demo's data | `reset-demo.cmd` (type `DELETE`) | `./demo.sh reset` |

The start script runs `docker compose -f deploy/demo/compose.yaml up -d --build --wait`. Three services start:

- `init` runs once and writes random service secrets (OIDC client secret, Keycloak admin password, internal service key) to the `demo-config` volume. If the file already exists, it keeps it.
- `keycloak` imports the synthetic realm from `deploy/local/keycloak` on first start (users, roles, exact callback) and keeps it in `keycloak-data`.
- `app` is the ASP.NET Core API and web page. It is built from source inside Docker with the exact SDK from `global.json`, locked NuGet restore and NuGet audit enabled. SQLite data lives in `app-data`.

Open **http://127.0.0.1:7452**. Use exactly `127.0.0.1` for the app and let it redirect you to `localhost:18080` for sign-in: those are the addresses the OIDC configuration expects. Accounts: `customer-a`, `customer-b` (customers) and `operations-admin` (inventory admin), password `123456Aa!`. These are public synthetic fixtures; the generated service secrets are not.

### Troubleshooting

| Symptom | What to do |
|---|---|
| "Docker is installed but not running" | Start Docker Desktop and run the start script again |
| Port 7452 already in use | Stop the other program; the app port is fixed because the realm's callback is `http://127.0.0.1:7452/signin-oidc` |
| Port 18080 already in use | Set another port before starting, e.g. `set CCAI_DEMO_IDP_PORT=18081` (Windows) or `CCAI_DEMO_IDP_PORT=18081 ./demo.sh start` |
| Sign-in page does not open | Check `docker compose -f deploy/demo/compose.yaml ps`: `keycloak` must be `healthy` |
| "Session expired" | Sessions last 15 minutes. Sign in again; bookings are kept |
| Old dates in the scenarios | Seed bookings are relative to the day the demo was created. `reset-demo` recreates them for today |

## Use

- **Chat.** Ask in Spanish or English. Without signing in you can see rooms, prices, availability and the policy. Bookings need a customer session. When a detail is missing (room, dates, an exact guest count, which booking) the chat asks and offers an example you can edit.
- **Proposal.** Shows the room, dates and nights, guests, fictional total, expiry (five minutes, Santo Domingo time) and "not executed yet". It holds nothing. *Back* discards it.
- **Confirm.** Only this button executes. The server rechecks availability, price, ownership and session in one transaction and returns a receipt. Pressing it again returns the same receipt.
- **Unverified result.** If the confirm response is lost, the page shows "unverified" and *Check receipt* asks the server for the receipt of that same proposal. If there is none yet, it was not executed and can be confirmed again safely.
- **My bookings** and the **calendar** show the server's current state. The **manual form** gives the same operations without the chat.
- **Technical panel** (collapsed, at the bottom): active mode, last code, the interpretation (which never authorizes anything), missing details, assumptions, proposal, receipt, server-side validations and the last calls (method, path and status only).

## Six scenarios

The *Scenarios* section of the page has buttons that fill the chat with text for each scenario, using dates relative to today. Nothing is sent until you press *Send*.

1. **Complete booking.** Sign in as `customer-a` and send `Reservar Deluxe <date> a <date> para 2 personas`. Review the proposal and press *Confirm*. The nights turn *Occupied* and the booking appears under *My bookings*.
2. **Rejected change.** Signed in as a customer, ask to move your booking onto nights the calendar shows as occupied or under maintenance (for example `Cambiar STAY-… suite <date> a <date>`). The answer is `DATES_OCCUPIED` and the original booking keeps its dates and nights. The same rule applies if the nights become occupied between the proposal and the confirmation: the confirmation is rejected and nothing changes.
3. **Repeated confirmation.** After scenario 1, press *Repeat confirmation*. You get the same receipt ("same receipt as before") and *My bookings* shows a single booking.
4. **Another customer's booking.** As `customer-a`, send `Cancelar STAY-B0000001`, the seeded booking of `customer-b`. The answer is `RESOURCE_NOT_FOUND` and reveals nothing. Knowing a code grants no access.
5. **Recovered result, with an injected fault.** Turn on the *Lab* switch under the scenarios, prepare a proposal and confirm it. The server processes the confirmation, but the page discards the response and shows "unverified". *Check receipt* returns the stored receipt of the same proposal, and the operation is not repeated. The fault exists only in this browser page and never changes the server.
6. **Ambiguous or out of scope.** `Reservar Suite <date> a <date> para 2 o 3 personas` asks for an exact count instead of assuming one. `¿Qué tiempo hace en París?` is declined without invented content.

## Modes: deterministic and local AI

| Mode | How the chat is interpreted | Requirements | Status |
|---|---|---|---|
| Deterministic (default, Docker demo) | `resort-rules-v0.12`: keywords and regular expressions, plus guest, policy and reference checks | None | Implemented and tested |
| Local AI (optional) | `OllamaResortInterpreter`: the existing pinned model `qwen3-vl` (digest `901cae73…`) proposes six fields under a closed JSON schema | Source checkout, .NET SDK, PowerShell 7, Ollama on `127.0.0.1:11434` with that exact digest | Contract tested with fixtures. Real inference **not run** in this delivery |

In both modes the interpreter only proposes. Clarification, ownership, offer, confirmation and receipts are the same C# code. In local AI mode, an unavailable model, a digest mismatch, a timeout or a response outside the schema returns an explicit code. The demo never falls back silently to the rules and never downloads a model.

To try local AI from source, after the [source setup](en/getting-started.md) and with the pinned model already in Ollama, run `$env:CCAI_RESORT_INTERPRETER='local-llm'` and then `./eng/Start-Resort.ps1` in PowerShell 7. The header shows *Local AI*. The WhatsApp bridge always uses the deterministic rules. The original v0.10 assistant with governed RAG runs with `./eng/Start-Demo.ps1 -IntentProvider local-llm`. Its earlier measurements are historical and do not describe the new chat.

## Architecture

### Deployed demo

```mermaid
flowchart LR
    B[Browser] -->|"127.0.0.1:7452 page + API"| A[app: ASP.NET Core]
    B -->|"localhost:18080 sign-in"| K[keycloak: OIDC]
    A -->|"keycloak:8080 discovery, token, keys"| K
    A --> R[("resort.db: inventory, offers, receipts")]
    A --> O[("operations.db: sessions")]
    I[init: random secrets] --> C[(demo-config)]
    C --> A
    C --> K
    A -. "optional, source profile" .-> L[Ollama local model]
    A -. "optional, own Meta setup" .-> W[WhatsApp ChannelHost]
```

Both ports are published only on the host loopback. Inside Docker the app reaches Keycloak as `keycloak:8080` and still validates tokens against the browser issuer `http://localhost:18080/realms/contactcenterai-local`. That port is configurable because 8080 is often taken, and the issuer must remain an `http://localhost` URL of the demo realm. In container mode the in-process network check accepts the Docker bridge, because the loopback-only published port is now the outer boundary. Host, Origin and antiforgery checks are unchanged.

### Booking journey

```mermaid
sequenceDiagram
    participant U as Visitor
    participant P as Page
    participant S as Server (C#)
    participant D as SQLite
    U->>P: "Book Suite 12-03 to 12-05 for 2"
    P->>S: POST /v2/resort/chat (session cookie, CSRF, Idempotency-Key)
    S->>S: interpret (rules or model) - proposes only
    S->>S: actor from session; ask if details missing
    S->>D: validate dates, capacity, price; store 5-min offer
    S-->>P: proposal (no booking yet)
    U->>P: press Confirm
    P->>S: POST /v2/resort/offers/{id}/confirm
    S->>D: one transaction: recheck, allocate each night once, receipt
    S-->>P: receipt (same receipt if repeated)
    Note over P,S: Response lost? GET /v2/resort/operations/{id} returns the stored receipt
```

### Operation states

```mermaid
stateDiagram-v2
    [*] --> Pending: proposal stored (5 min)
    Pending --> Queued: explicit confirmation
    Pending --> Invalidated: link revoked or account re-linked
    Pending --> [*]: expires (confirm returns OFFER_EXPIRED)
    Queued --> Completed: recheck passes, nights allocated
    Queued --> Rejected: occupied, price or version changed
    Queued --> Queued: restart - recovery worker resumes the same ID
    Completed --> Completed: repeated confirm returns the same receipt
    Rejected --> Rejected: repeated confirm returns the same receipt
```

### Decisions and their limits

- **The model or parser proposes; C# decides.** The interpretation is a closed request and never selects the account. Limit: language coverage of the deterministic rules is narrow (declared examples, not open understanding).
- **Proposal then confirmation.** The proposal holds nothing and expires in five minutes. Confirmation rechecks everything in one SQLite transaction with a unique `(room, night)` allocation. Limit: single-node SQLite, sized for a demo.
- **Idempotency and receipts.** The same message key returns the same proposal, and the same proposal ID returns the same receipt. A lost response is recovered by reading the receipt, never by running the action again.
- **Identity from the session only.** OIDC with Keycloak gives the session, and roles and member come from the server's binding. Text such as "I am customer-b" is ignored.
- **Ask instead of assume.** A missing room, dates or an exact guest count produces a question. A change keeps the booking's own guest count.
- **Container profile.** Configuration is explicit and opt-in, and the protections stay in place. Limit: local demo only. A public deployment would need HTTPS, external secrets, per-visitor isolation and a different identity setup.
- **Not done.** Payments, real hotel systems, live Genesys, load testing, security certification and measured model quality for this chat.

## For developers

| Command | What it checks |
|---|---|
| `./eng/Verify-Demo.ps1 -DotNetExe <sdk>` | Locked restore with audit, Release build, every C# check suite, Python evaluators |
| `node --test "tests/web/*.test.mjs"` | Resort page behavior over the real HTML in a simulated DOM; no browser |
| `docker compose -f deploy/demo/compose.yaml build` | Demo image builds from source |

`Verify-Demo.ps1` and several older suites rewrite tracked evidence files. Review `git status` afterwards and do not commit those rewrites by accident. The [resort v0.13 contract](contracts/resort-v0.13.md) lists the acceptance criteria.
