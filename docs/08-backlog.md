# Backlog posterior al freeze

## Extensión de canales v0.11

Diseño previo al código: [freeze](architecture-freeze-v0.11-channels.md), [ES](integrations/channels.es.md), [EN](integrations/channels.en.md), ADR-024 y contratos versionados. CH01: ledger/contratos/fixtures. CH02: Telegram privado polling/replies. CH03: Meta firmado/test number/túnel opcional. CH04: conocimiento Public y handoff mock. Partes locales Ready; live depende de credenciales/configuración/evidencia. CH05 vinculación de miembro y acciones privadas excluido y Not Ready. Sin modificación de mediciones, cuentas ni bases v0.10.

Estimaciones S/M/L expresan tamaño relativo, no fechas. Orden M1→M5; M6 opcional. Aplicar Definition of Ready antes de cada ticket. Estado actual: **B01 CLOSED**, 12 checks offline, siete checks SQL/OIDC live y dos logins reales PASS. Diagnóstico detenido; retiro del certificado del perfil Windows original no confirmado tras cerrar el terminal, registrado como nota operativa. **B02 DONE en su scope estructural**, con cinco proyectos .NET, contratos y 21 checks PASS más arranque worker; [DoR/evidencia](progress/b02-foundation.md). B03/B04 son los siguientes slices y requieren sus propios gates. Ver [evidencia B01](progress/b01-runtime-identity.md).

| Ticket | Slice y resultado revisable | Refs | Depende | Tamaño |
|---|---|---|---|---|
| B01 | Spike runtime/SQL/OIDC: compatibilidad, licencias, claim mapping y locks | NFR12–13, AC04 | Freeze | S |
| B02 | Skeleton API/Domain/Application/Infrastructure/Worker, contratos OpenAPI, pruebas de dependencias | FR01, NFR12 | B01 | M |
| B03 | Login BFF/IdP sintético, resource auth y sesión anónima a verificada | FR03–04, AC04–06 | B02 | M |
| B04 | SQL operacional, inbox/outbox/audit, clock y leases/restart | FR01/09/18, AC14/16 | B02 | M |
| B05 | DONE en demo: fuente .NET/SQLite separada, CP-001 y command receipts; SQL Server pendiente | FR05/08, AC11–14 fuente | B01/B02 | M |
| B06 | DONE demo SQLite: oferta, confirmación y command durable; enterprise SQL pendiente | FR06–09, AC07–16 | B03–B05 | L |
| B07 | DONE demo SQLite: dispatch/leases/Unknown/reconcile/kill switch; worker embebido | FR08–09/21, AC15–16/31 | B06 | L |
| B08 | Knowledge registry/review/publication/expiry/ACL/feedback y Qdrant spike | FR10–13/20, AC21–24 | B03/B04 | L |
| B09 | RAG bilingüe baseline, citation resolver, abstention y corpus sintético | FR02/10–11, AC01–03/21 | B08 | M |
| B10 | LLM/model spike, budget/schema, gateway read/preview/handoff y intent | FR14, AC25–26 | B06–B09 | M |
| B11 | Contact center mock, requestId/epoch, handoff/context y delivery fault modes | FR15–16/19, AC17–20 | B04/B07 | L |
| B12 | Resumen desde hechos y contexto para agente asignado | FR17/20, AC27–29 | B10/B11 | M |
| B13 | Python evaluator, 200 casos/splits, manifest y reporte honesto | FR22, NFR11, AC32 | B09–B12 | L |
| B14 | UI Angular mínima de chat, oferta/confirmación/status y agente | NFR14, AC08/18/21 | B06/B11/B12 | M |
| B15 | CI, OTel/dashboard/alerts, restore/rollback y guion reproducible | NFR10/18, AC30–32 | B07/B13/B14 | M |
| B16 | Spike Genesys tenant, wire contract/Architect/auth/capabilities y ADR actualizado | FR19, AC17–20 | B11 + acceso tenant | L |
| B17 | Sandbox adapter real y contract/smoke/failure tests sanitizados | FR19, gate M6 | B16 | L |

Actualización B03/B04: [candidato empresarial](progress/b03-b04-ingress.md) implementado con 33 checks de política/pipeline, esquema SQL y credencial restringida reales. Ambos tickets siguen IN PROGRESS: fixture tests no cierran repositorio SQL/live OIDC, revocación global, leases ni restart. El usuario autorizó una [presentación v0.2](progress/demo-local.md) para facilitar el acceso: C# + SQLite real, OIDC real y clave común de cuentas ficticias. Tiene 17 checks de almacenamiento y 14 de API PASS; UI y logins reales A/B ya observados, historial separado y persistencia sanitizada comprobados. Esta excepción permite abrir la presentación sin certificados y conserva los gates empresariales. Reservas pertenecen a B05, sin datos fuente duplicados en Operational DB.

La entrega local vigente ya completa B05/B06/B07 en SQLite: fuente separada, propuesta, confirmación y recovery. El asistente simulado se añadió después de probar los invariantes del negocio. Ver [entrega v0.5](progress/demo-v0.5.md) y el desglose de los subsets siguientes; los gates empresariales permanecen abiertos. No implementar features fuera del scope.

## Layout futuro orientativo

`src/ContactCenterAI.Api`, `.Application`, `.Domain`, `.Infrastructure`, `.Worker`, `.Simulator`; adapters AI/Knowledge/Genesys inicialmente como módulos de Infrastructure, extraer librerías solo por necesidad de dependencias. `tests/Unit`, `Integration`, `Contracts`, `Security`; `frontend/`; `evaluation/evaluators`; `deploy/local`; `.github/workflows`. No se crean `.csproj`, compose o workflows de producto en Sprint 0.



## Entrega local v0.5

[Estado y evidencia](progress/demo-v0.5.md). B06/B07 DONE dentro de los límites SQLite; 25 checks. B08/B09: corpus fixture de diez secciones, publicación/reviewer distintos, expiry/revoke/ACL y lookup por temas con citas; Qdrant/indexing/traducción-editor UI siguen abiertos. B10: gateway JSON/budget y proveedor simulado; LLM live pendiente. B11/B12: adapter neutral mock, handoff/epoch/contexto asignado y plantilla de hechos; wire protocol/ingress Genesys y edición humana del resumen pendientes. B13: Python eval ejecutada sobre 40 casos públicos; holdout de 200, judge/human agreement y modelo real pendientes. B14: UI sencilla HTML/CSS/JS; Angular pendiente. B15: audit, Activity/Meter y CI declarada; ejecución cloud/exporter/dashboard/deploy/restore pendientes. No cerrar el ticket empresarial completo por un subset local.

El texto anterior conserva la secuencia del plan v0.1. La presentación vigente ya confirma/cancela/reconcilia y responde con proveedor simulado; la lectura sola fue el hito v0.3 histórico. Gate M6 B16/B17 exige tenant y permanece sin ejecutar.

## Avance B10 local v0.6

[Inferencia real local](progress/demo-v0.6.md), freeze v0.6 y ADR-019 previos. Provider/gateway, digest, límites y rollback explícito probados; 28 checks nuevos, 182 totales. Corrida real de 40 casos públicos: 40 intents correctos, 38 propuestas exactas; no holdout empresarial ni calidad general de RAG. Otros gates permanecen abiertos.


## Entrega local v0.7/v0.8

[Evidencia final](progress/demo-v0.8.md): B08/B09 local con 40 documentos lógicos/80 variantes y embeddings reales; B13 con 200 casos de recuperación y comparación de regresión conocida; B15 con exporter/panel local, backup/restore, inventario de paquetes y workflow SHA-pinned. Mantener abiertos Qdrant/SQL/Angular/tenant Genesys/CI cloud, calidad independiente y carga/retención. No renombrar todos los tickets empresariales como DONE por los subsets de presentación.


## Cierre de entrega local v0.10

[Estado vigente](progress/demo-v0.10.md). Subsets locales de B05–B15 completados dentro de la excepción de presentación: source/transacciones, asistente/IA/retrieval gobernado, mock handoff, UI, eval, panel y restore. 249 checks y cierre compuesto 97%/98%; errores conservados. B03/B04 empresarial y M6 no se cierran por un mock. SQL cifrado bloqueado en el entorno actual, GitHub destino/Genesys tenant no proporcionados y revisión humana pendiente. Entrega final facilita apertura, evaluación e entrevista sin ampliar el scope a pagos o datos reales.
