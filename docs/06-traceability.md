# Trazabilidad de requisitos, controles y decisiones

Esta tabla relaciona requisitos con evidencia futura. Documentación trazada no equivale a pruebas aprobadas. AC se define en `05-use-cases-acceptance.md`, INV en workflows, TH en threat model y B en backlog.

| FR | UC / AC | Componentes / diagramas | ADR | Amenazas | Backlog |
|---|---|---|---|---|---|
| FR01 | UC02/05/06, AC04/16/19 | Conversations, state-conversation | 001/002/007 | TH10/18 | B02/B04 |
| FR02 | UC01, AC01/11/27 | AI workflow, sequence-rag | 008/009 | TH03/09 | B09/B10/B13 |
| FR03 | UC02, AC04/05 | Identity, c4-components | 006 | TH01 | B03 |
| FR04 | UC02/07, AC05/06/21 | Access, sequence-rag | 006 | TH05/16 | B03/B08 |
| FR05 | UC02, AC05/06 | Reservation adapter, sequence-cancellation | 002/003 | TH01/05 | B05/B06 |
| FR06 | UC03/04, AC07/11/12 | Policy + Offer, sequence-cancellation | 003 | TH07/08 | B06 |
| FR07 | UC03/04, AC08/10/13 | Confirmation, sequence-cancellation | 003/007 | TH07/08 | B06 |
| FR08 | UC03/05, AC09/12/15/16 | Worker + source, state-command | 003/007 | TH08/09 | B06/B07 |
| FR09 | UC05, AC14–16 | Inbox/outbox, sequence-reconciliation | 007 | TH08/09/10 | B04/B07 |
| FR10 | UC01/07, AC02/21/23 | Knowledge, sequence-rag | 004/006 | TH04/05/17 | B08/B09 |
| FR11 | UC01, AC01–03 | Citation validator, sequence-rag | 004/009 | TH03/04/17 | B09/B13 |
| FR12 | UC07, AC22/23 | Registry/worker, sequence-ingestion | 004/006 | TH04/14/17 | B08 |
| FR13 | UC07, AC24 | Feedback + registry, data-model | 004 | TH04 | B08 |
| FR14 | UC08, AC25/26 | Orchestrator/gateway, c4-components | 003/008 | TH02/03/11/12 | B10 |
| FR15 | UC06, AC17/18/20 | Handoff, sequence-handoff | 005/007 | TH10/18 | B11 |
| FR16 | UC06, AC18–20 | Ownership/epoch, state-conversation | 005/007 | TH10/18 | B11 |
| FR17 | UC09, AC27–29 | Summary from fact ledger | 008/009/010 | TH06/09 | B12 |
| FR18 | UC03/05, AC08/09/16/28 | Audit SQL, data-model | 007/010 | TH06/13 | B04/B15 |
| FR19 | UC06, AC17–20 | Contact adapter, c4-context/containers | 005 | TH10/18 | B11/B16/B17 |
| FR20 | UC02/07, AC06/21 | Assignment/ACL, data-model | 006 | TH05/16 | B03/B08/B12 |
| FR21 | UC10, AC31 | Operations controls / worker | 007/010 | TH08/11 | B07/B15 |
| FR22 | UC10, AC32 | Offline Python evaluator | 009/010 | TH15 | B13/B15 |

## Invariantes y evidencia de seguridad

| INV | AC | Tests futuros |
|---|---|---|
| INV01 | AC04–06/25 | Forged identity/role, cross-account, direct proposal |
| INV02 | AC07–13/31 | Confirm binding, expiry, policy/source version and kill switch |
| INV03 | AC09/15/16/27 | Commit then timeout, summary false-success, restart |
| INV04 | AC13/14/16 | Unique offer, duplicate keys, concurrent workers |
| INV05 | AC02/21/23 | Revoked/stale ACL index and direct citation |
| INV06 | AC17–20 | Handoff race, old epoch and outbound suppression |
| INV07 | AC08/14/16/30 | SQL failure before 202 and worker recovery |
| INV08 | AC01/11/32 | ES/EN pairs across model/prompt/document changes |

## Requisitos no funcionales

| NFR | Método de verificación futuro | Gate |
|---|---|---|
| NFR01/02 | API security corpus, canaries, auth/resource/ACL matrix | G1/G2/G3 |
| NFR03/04/09 | Source receipts, crash failpoints, 100 replays/2 workers | G2 |
| NFR05/06 | >=200 measured turns, mocked versus live provider overhead | G3/G4 |
| NFR07/08/15 | Budget/rate/circuit/retry boundaries, tariffs/tokens manifest | G2/G3 |
| NFR10 | Audit completeness and end-to-end traces without PII/cardinality | G2/G4 |
| NFR11 | Paired bilingual holdout with counts, gates and confidence | G3 |
| NFR12 | Dependency direction and port contract checks | G1 |
| NFR13/14 | Clean checkout demo; keyboard/accessibility confirmation review | G4 |
| NFR16 | Retention/deletion/cache/backup policy tests on synthetic records | G2/G4 |
| NFR17 | Dependency outage/recovery scenarios, no unsupported uptime claim | G4 |
| NFR18 | Release bundle, changed-asset regression, scans and rollback rehearsal | G1–G4 |

