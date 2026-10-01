# ADR-016 — Cancelación durable en la presentación local

Estado: Accepted. Baseline: demo v0.4. Fecha: 2026-10-01 UTC.

## Decisión

Mantener las dos fuentes separadas de ADR-015. Añadir en Operational SQLite Offer, Confirmation, BusinessCommand y eventos auditados. La fila Pending funciona como outbox transaccional: se inserta junto a la confirmación consumida, nunca después de devolver 202. Un BackgroundService C# hospedado en la API local reclama leases persistentes; el proceso Worker empresarial continúa como objetivo de despliegue independiente. No se usa una cola en memoria como autoridad.

La oferta dura cinco minutos y liga tenant, principal, conversación, versión, epoch, reserva, versión fuente, política y consecuencias. Una propuesta nueva invalida la anterior. Confirm/reject son actos de UI autenticados con Origin/CSRF, Idempotency-Key e If-Match. Ningún mensaje «sí» ejecuta el comando. Confirm vuelve a leer la fuente; al consumir valida de nuevo identidad, reloj y versión dentro de una transacción IMMEDIATE. La fuente vuelve a decidir al ejecutar.

Antes del POST se persiste Submitted y un lease de veinte segundos. Timeout, caída o resultado inválido produce Unknown. Recuperación consulta el receipt por commandId; solo el 404 de esta fuente local, cuyo receipt y efecto se confirman en una misma transacción, permite reenviar exactamente el mismo comando. No se generaliza esa garantía a Genesys ni a una API externa. Unknown se consulta con espera creciente limitada; tras sesenta segundos queda marcado RequiresHumanReview. Kill switch bloquea confirmaciones y Pending; Submitted/Unknown sigue reconciliando. Sesión vencida después de confirmar no borra la intención; el dispatcher revalida binding/rol activo del principal, no una sesión de navegador permanente.

## Alternativas y límites

Ejecutar dentro del request no resuelve crash/timeout. Reintentar con ID nuevo rompe dedupe. Un broker y un tercer proceso local añaden fricción sin modificar estas invariantes. El worker embebido simplifica el lanzador; sus leases soportan múltiples instancias y restart, y la lógica puede ser hospedada separadamente. Ledger local conserva recibos durante toda la vida de la demo; no se implementa purga automática. Ninguna prueba SQLite cierra los gates SQL Server. No se habilita cancelación con LLM ni cancelación real de viajes.

Refs: FR06–09/18/21, AC07–16/31, INV01–04/07, ADR-003/007/014/015.
