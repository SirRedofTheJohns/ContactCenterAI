# Changelog

Entries before v0.13 are summarized from historical progress notes in `docs/progress/`.

## v0.13.1 — 2026-10-27 (unreleased)

- **Local AI usable from the Docker demo**: `start-demo.cmd ai` / `./demo.sh start-ai`; the container reaches the host's loopback Ollama through `host.docker.internal`; startup warm-up of the pinned model; one prompt clarification for vague booking requests.
- Development sample of 22 ES/EN cases through the Docker app: local model 22/22 after one prompt round (21/22 before), deterministic rules 19/22; UI walkthrough and explicit unavailability error verified. See `docs/progress/local-ai-chat-2026-10-27.md`.
- Known: the registry's `qwen3-vl:latest` no longer resolves to the pinned digest, so local AI works only where that digest is already installed. The round-1 prompt includes an example derived from one development case.
- Presentation material: `docs/presentation.md` (60–90 s script, 2-minute explanation, recording guide); README states the checked platform and the local AI limitation first.

## v0.13 — 2026-10-27 (unreleased)

Self-contained portfolio demo with a web chat.

- **Docker demo profile** (`deploy/demo`): app, Keycloak and SQLite volumes; idempotent secret initialization; `start-demo.cmd` / `stop-demo.cmd` / `reset-demo.cmd` and `demo.sh`; ports on loopback only. No SDK, Python, PowerShell, WhatsApp, tunnel or model needed on the host.
- **Resort web chat** (`POST /v2/resort/chat`) sharing one turn logic with the WhatsApp bridge: clarification instead of defaults, policy and out-of-scope answers, dynamic example dates, change keeps the stay's guests.
- **Optional local AI interpreter** for the existing pinned model, with a closed schema and explicit errors (no silent fallback). Real inference not run in this release.
- **Chat-first UI**: proposal summary, repeated-confirmation and receipt lookup, six scenarios, browser-only lab fault, technical panel, keyboard and mobile improvements.
- **Fixes**: room pick kept across login; stale quotes retired; logout independent of link cleanup with honest unverified/rejected messages; times in America/Santo_Domingo; patched `System.Security.Cryptography.Pkcs` 9.0.21 so clean restores pass NuGet audit.
- **Checks**: new `ResortChatChecks` (44) and Node UI checks (28); CI runs them with pinned Node and builds the demo image.
- WhatsApp bridge key is now optional.

## v0.12 — 2026-10-06

Fictional resort: catalog, calendar, booking/change/cancellation offers with explicit confirmation, receipts, recovery, verified WhatsApp linking, maintenance and rates. See `docs/progress/resort-v0.12.md`.

## v0.11 and earlier

Messaging channels (v0.11), portfolio presentation with governed RAG and local model (v0.10) and earlier foundations. See `docs/progress/`.
