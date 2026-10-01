# Architecture Decision Records

Estado **Accepted** significa decisión del arquitecto para el MVP local v0.1, no aprobación de GBS, Genesys, seguridad corporativa o negocio real. Revisar decisiones cuando cambien sus supuestos; no editar silenciosamente un ADR histórico aceptado.

| ADR | Decisión |
|---|---|
| [001](001-modular-monolith.md) | Monolito modular más worker durable |
| [002](002-dotnet-sql-and-data-ownership.md) | C#/.NET, SQL autoritativo y fuentes separadas |
| [003](003-deterministic-transactions.md) | Reglas y confirmación fuera del LLM |
| [004](004-governed-rag.md) | RAG gobernado y Qdrant reconstruible |
| [005](005-contact-center-adapter.md) | Adapter neutral y validación Genesys por fases |
| [006](006-identity-and-resource-access.md) | OIDC, RBAC y autorización de recurso |
| [007](007-idempotency-and-recovery.md) | Inbox/outbox, idempotencia y Unknown |
| [008](008-bounded-llm-and-provider.md) | Workflow acotado y port de proveedor |
| [009](009-bilingual-evaluation.md) | Evals bilingües y gates deterministas |
| [010](010-delivery-observability-and-privacy.md) | Release versionado, privacidad y operación |
| [011](011-local-runtime-and-identity-profile.md) | Perfil local B01; compatibilidad runtime SQL/OIDC pendiente |
| [012](012-foundation-and-readiness.md) | Foundation .NET y readiness separada de liveness |
| [013](013-session-and-operational-ingress.md) | Sesión SQL, CSRF, resource authorization y recepción transaccional |
| [014](014-simple-local-demo.md) | Presentación sintética con clave común, OIDC real y SQLite persistente en loopback |
| [015](015-source-simulator-demo.md) | Fuente C# independiente, SQLite separada, CP-001 y receipts internos |

Actualización de evidencia 2026-09-30: ADR-011 mantiene su contexto histórico; la compatibilidad B01 ya está verificada en el [reporte actual](../progress/b01-runtime-identity.md).



| [016](016-local-durable-transactions.md) | Confirmación/ledger y dispatcher local con recovery |
| [017](017-reproducible-assistant-and-contact-center-mock.md) | Asistente simulado, knowledge por temas y adapter neutral/handoff |
| [018](018-repeatable-demo-fixtures.md) | Preparación repetible con archivo del dataset anterior |

| [019](019-local-live-intent-provider.md) | LLM local real con intención no confiable, pin, budget y rollback |


## Presentación v0.7/v0.8

| [020](020-local-semantic-retrieval-and-operational-evidence.md) | BGE-M3, índice local, operación y evaluación |
| [021](021-bounded-evidence-selection.md) | Selección acotada de evidencia, sin autoridad de negocio |
| [022](022-retrieval-recall-and-final-delivery.md) | Recall revisado, evidencia final y gates externos |
| [023](023-query-scope-before-semantic-inference.md) | Alcance determinista antes de inferencia tras fallos del candidato |

| [024](024-demo-messaging-channels.md) | WhatsApp oficial de prueba y Telegram polling: host separado, preguntas públicas, outbox y límites de costo |
