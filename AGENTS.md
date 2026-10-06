# ContactCenterAI engineering instructions

## Current phase

New request 2026-10-06: resort catalog (Standard/Deluxe/Suite), fictional nightly prices/amenities, live calendar, booking/reschedule/cancellation through WhatsApp. Read docs/resort-booking-v0.12.es.md and .en.md, proposed ADR-025 and OPEN architecture-freeze-v0.12-resort.md. These are design proposals, not implementation/evidence. Finish contracts/schema/session-link/revocation/negative-oracle review before productive code. Preserve current v0.11 private-action denial until the new baseline closes; never authenticate from provider IDs. Separate live source inventory from RAG policy and preserve historical source/operational/channel data. Current restoration only restarts existing channel/tunnel and updates Meta callback after PC restart.

Messaging architecture v0.11 is frozen for local/public implementation: read docs/architecture-freeze-v0.11-channels.md, docs/integrations/channels.en.md and ADR-024 before channel changes. Dedicated loopback ChannelHost, official APIs, independent ledger, no private reservation capability, no provider-ID authentication, no paid templates/broadcast/cloud fallback. Provider live gates remain open until credentials/account setup and evidence exist. Core documentation entry guides are bilingual; preserve historical reports as historical evidence.

v0.11 local channel implementation completed historically: 75 isolated checks plus the previous 249, full Release regression 324 PASS. Activation diagnostic change: 83 focused channel checks PASS; preserve historical reports. Read docs/progress/channels-v0.11.md, docs/progress/whatsapp-activation-2026-10-05.md and setup guides. A scoped Employee SYSTEM_USER token (60 days, two WhatsApp scopes, owner-approved demo app Manage app and test WhatsApp Messages/phone read-only) resolved the observed 403/131005 test failure. Real ES/EN approved-policy replies with citations reached Read; six incoming Completed, four outputs Read, original two Failed preserved, one attempt each. Outbound v25.0/webhook messages v26.0 receipt correlation passed for this setup. Broader Meta health warnings remained but did not prevent the eligible test replies; do not invent business details or add payment. Full PC/tunnel restart recovery, live expired-token/handoff acceptance and local-model activation remain open. AI mode simulated; Telegram disabled; remote CI runner did not start due to account/platform restriction. Preserve inference reports, databases and failed outputs. Logical retention is not encryption/physical erasure.

Portfolio presentation v0.10 is implemented; read docs/progress/demo-v0.10.md, CLOSED freezes v0.9/v0.10 and ADR-022/023 before changes. C#/.NET runtime, real Keycloak OIDC, two SQLite databases, HTTP source, explicit confirmation, receipts/leases/reconciliation, governed bilingual dense retrieval and closed local evidence selection. Final Release build and 249 deterministic checks passed. 300 real initial RAG outputs plus a clearly labeled scope-only composite replay: 97%/98%, 60/60 OOD abstentions; eight errors preserved. No independent quality approval. W3C local exporter/admin and historical backup/restore evidence. Keep SQL/TLS host blocker, tenant Genesys, remote CI, production and independent human gates open; no claim for Qdrant/Angular. B01/B02 closed historically, enterprise B03/B04 in progress. Preserve databases, accounts, model pins and earlier measurements. Never overwrite historical inference results with current code or repeat B01/certificates as a presentation prerequisite.

## Boundaries

- Main runtime: C# / .NET. Python: offline evaluation and data tools only.
- Model output is untrusted. Authorization, ownership, confirmation, eligibility, money, state transitions and success reporting are deterministic.
- Never derive an authenticated member or employee role from chat text, caller-supplied metadata or an LLM claim.
- Never announce a successful transaction before authoritative source confirmation. A timeout after submission becomes Unknown and requires reconciliation.
- Use synthetic data. Do not commit secrets, real member data, provider logs containing PII or hidden model reasoning.
- Keep Genesys DTOs and SDK types outside Application and Domain. Local mocks must model duplicates, disorder, timeouts and uncertain side effects.
- A safety regression blocks release. A passing LLM judge cannot override a failed deterministic invariant.
- New behavior requires requirement/acceptance/ADR updates and impact review before implementation. Breaking a frozen invariant requires a new baseline.
- Do not claim live Genesys validation, security certification or measured benchmark performance without evidence.

## Delivery

Use small vertical slices from `docs/08-backlog.md`. Every change states behavior, evidence and limitations. Keep contracts versioned. Use the documented Definition of Ready / Done. Preserve the distinction between simulated, measured locally and tenant-validated evidence.

Local AI: fixed digest/endpoint/raw wrapper, eight-second deadline and closed proposals. Never parse thinking as output. Preserve the public 40-case run/errors; it is not a holdout. Explicit mode; no silent simulator fallback or download.
