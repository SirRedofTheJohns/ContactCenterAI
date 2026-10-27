# ContactCenterAI

### Book a fictional resort from a chat, with server-side guarantees

[Español](README.es.md) · [Demo guide](docs/demo-guide.en.md) · [Scenarios](docs/demo-guide.en.md#six-scenarios) · [Architecture and decisions](docs/demo-guide.en.md#architecture)

Ask about rooms and dates, get a priced proposal, confirm it, then change or cancel it. The chat is only the front door. A **C# / .NET** backend decides identity, ownership, availability, price and execution, and every confirmed action leaves a durable receipt.

This is a **portfolio project**. The hotel, prices, guests and bookings are fictional, there are no payments, and the sign-in is a real local Keycloak with synthetic users.

![Resort page with the booking chat, summary and calendar](docs/progress/screenshots/resort-v0.13-desktop.png)

## Try it in five minutes

Requirements: [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker Engine with Compose v2) and Internet access for the first build. You do **not** need the .NET SDK, Python, PowerShell, WhatsApp, a tunnel, a model download or a paid account.

```text
Windows:       start-demo.cmd
macOS / Linux: ./demo.sh start
```

Open **http://127.0.0.1:7452** and sign in as `customer-a` or `customer-b` with password `123456Aa!`. Bookings persist across restarts. `stop-demo.cmd` / `./demo.sh stop` stops the demo, and `reset-demo.cmd` / `./demo.sh reset` deletes only this demo's data after you type `DELETE`.

The first run builds the image from source inside Docker, which takes a few minutes. Ports 7452 (app) and 18080 (sign-in) are published only on `127.0.0.1`; set `CCAI_DEMO_IDP_PORT` if 18080 is taken.

## What to try

| # | Scenario | What you see |
|---|---|---|
| 1 | Complete booking | Chat request → proposal with room, dates, guests, total and expiry → Confirm → calendar and *My bookings* update |
| 2 | Rejected change | Moving a booking onto occupied nights returns `DATES_OCCUPIED`; the original booking is unchanged |
| 3 | Repeated confirmation | Confirming twice returns the same receipt and no duplicate booking |
| 4 | Another customer's booking | `customer-a` cannot cancel `customer-b`'s stay, even with its code |
| 5 | Lost response | A browser-only lab switch drops one confirm response; *Check receipt* recovers the outcome of the same proposal |
| 6 | Ambiguous or out of scope | "2 or 3 guests" is asked about, not assumed; unrelated questions are declined without inventing content |

The page has a *Scenarios* section with buttons that fill the chat for each one, using dates relative to today.

## What is real

| Part | Status |
|---|---|
| Bookings, ownership, offers, confirmation, transactions, idempotent receipts, recovery | **Implemented** in C# with SQLite and covered by deterministic checks |
| Sign-in | **Real** local Keycloak OIDC (Authorization Code + PKCE) with synthetic users |
| Chat interpretation (default) | **Deterministic** rules and regular expressions, no model |
| Chat interpretation (optional) | **Local AI** adapter for the existing pinned Qwen model, closed schema, no fallback. Model not included; real inference **pending** on a host with the pinned model |
| Original assistant with RAG (v0.10) | Implemented; runs from source, not in the Docker demo |
| WhatsApp | Optional integration with its own Meta setup; outside the basic demo |
| Telegram, Genesys, human agents | Adapter present or **simulated**; no live tenant |
| Hotel, prices, payments | **Fictional**; no payments |

## How it works

```mermaid
flowchart LR
    U[Chat message] --> I{Interpreter}
    I -->|default| R[Deterministic rules]
    I -->|optional| M[Local model, closed schema]
    R --> C[Conversation turn]
    M --> C
    S[Verified session] --> C
    C -->|missing details| Q[Clarifying question]
    C -->|complete| O[Priced proposal, 5 min, holds nothing]
    O -->|Confirm button| X[Recheck + transaction + receipt]
    X --> V[Calendar and bookings]
```

The text only **proposes**. Identity comes from the session cookie, never from the message. A proposal is executed only by an explicit confirmation bound to its ID, and execution rechecks availability, price and permissions inside one transaction. See the [demo guide](docs/demo-guide.en.md#architecture) for the deployed topology, the operation states and the design decisions.

## Evidence and limits

- Deterministic C# checks: the original 402 plus 41 web-chat checks, and 28 browser-free UI checks with Node's built-in runner. Results with dates and commands are in [the latest progress note](docs/progress/portfolio-v0.13-2026-10-27.md). Earlier figures are historical.
- CI: the workflow runs these checks and builds the demo image. Remote runs have not started since the GitHub account's billing lock, so there is no remote evidence yet.
- Not claimed: production readiness, security certification, live Genesys, measured model quality for the new chat, or load/performance benchmarks.

## More

| If you want to… | Read |
|---|---|
| Install, use and troubleshoot | [Demo guide](docs/demo-guide.en.md) |
| Review contracts | [Resort v0.13](docs/contracts/resort-v0.13.md), [v0.12 baseline](docs/contracts/resort-v0.12.md) |
| Present it in an interview | [Interview script](docs/interview-script.md) (Spanish) |
| See what changed | [Changelog](CHANGELOG.md) |
| Browse design history and earlier evidence | [Documentation index](docs/README.md) |

Built with AI-assisted tooling; decisions, evidence and limitations are documented for review. Licence: **not chosen yet**. Until the owner adds one, default copyright applies. [Third-party notices](THIRD_PARTY_NOTICES.md).
