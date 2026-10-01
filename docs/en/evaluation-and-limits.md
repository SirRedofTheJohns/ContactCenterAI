# Evidence and limits

[Español](../es/evaluacion-y-limites.md)

## Safety and correctness

The v0.10 Release baseline recorded **249 deterministic checks** across nine suites, with no build warnings or errors. They cover resource authorization, persistence, source conflicts and duplicate commands, lost-response recovery, worker leases, handoff ownership, closed model contracts, knowledge access/review and operational reporting. See [the report](../progress/demo-v0.10-checks.json).

These checks use controlled clocks and injected failures. They do not prove a live Genesys tenant, end-to-end SQL Server composition, independent bilingual answer quality, or production load. GitHub Actions has a workflow for the same command; publication evidence records whether a remote run actually passed.

## Answer quality

| Evidence | Results | Interpretation |
|---|---|---|
| 200 original local retrieval outputs | 96.5% exact, 39/40 OOD abstentions, no inference errors, p95 4,782 ms | Known regression set |
| 100 additional original outputs | 98% exact, 20/20 OOD abstentions, no inference errors, p95 4,760 ms | Same author; no independent approval |
| Scope-only composite replay | 97% / 98%; 60/60 OOD abstentions | 13 current guard checks plus 287 reused outputs; not fresh model inference |

OOD means outside the demo's supported subject. Eight quality errors remain in the composite results, including an incorrect document for late checkout. The original data, gold expectations, outputs and failures are preserved in [the original report](../../evaluation/reports/demo-v0.10/report.md) and [replay report](../../evaluation/reports/demo-v0.10/scope-replay/report.md).

The reported latency concerns measured retrieval outputs on the original host, not the replay, messaging delivery or end-to-end concurrent traffic. Current documentation changes do not create new model measurements.

## Open work

- Encrypted SQL Server product connectivity and the full SQL repository acceptance, blocked on the original Windows runtime.
- Live Genesys tenant configuration, routing, acknowledgment and transfer behavior.
- Independent bilingual reviewers and genuinely unseen evaluation cases.
- Production HTTPS, revocation, data retention enforcement, distributed telemetry, load and availability.
- Real provider acceptance for WhatsApp and Telegram after their credentials and setup are available.

The project demonstrates engineering decisions and local behavior. It does not establish years of work experience, enterprise certification or every feature in the original vacancy.
