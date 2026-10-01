# ADR-017 — Asistente reproducible y adapter local de contact center

Estado: Accepted. Demo v0.5, 2026-10-01 UTC.

Para completar una presentación sin credenciales externas, usar un proveedor de intención **simulado**, detrás de IIntentProvider, que produce el mismo JSON cerrado que un futuro LLM. La UI y los reportes lo nombran simulado. No declarar calidad, tokens, costos ni benchmark de un modelo real. El gateway valida intent/language/topic/reservationId, rechaza campos extra, herramientas desconocidas y campos de identidad. Un turno usa una propuesta y una tool, con deadline de ocho segundos; ninguna tool ejecuta cancelaciones. La confirmación del workflow v0.4 sigue siendo exclusivamente UI.

FAQ usa recuperación léxica sobre un corpus bilingüe pequeño, versionado y curado en SQLite. Publicación, vigencia, tenant y ACL se revalidan antes de cargar texto y al resolver citas. Respuesta extractiva en plantilla con versión/sección; si falta evidencia, abstenerse. No se introduce Qdrant/embeddings ni se llama a este baseline dense RAG. Se mantienen como experimento empresarial posterior. Versiones publicadas iniciales son fixtures revisadas, no evidencia de un proceso humano real. Métodos de gobierno exigen Editor y Reviewer distintos y no alteran CP-001.

Inbox genera un TurnJob durable junto al mensaje, sin procesar automáticamente mensajes históricos previos a este slice. BackgroundService reclama leases. Resultado es único por turnId y se publica solo si ownership/epoch aún admite IA. Handoff persiste requestId/epoch, invalida ofertas y bloquea propuestas; IContactCenterAdapter neutral tiene un mock que devuelve acknowledgment explícito. Solo acknowledgment actual pasa a HumanOwned e incrementa epoch. No hay DTO Genesys ni llamada real. Mock fault tests ejercitan failure, duplicates y callbacks viejos. Endpoint de agente asignado lee contexto y estados desde hechos del ledger. Ninguna traducción/resumen convierte Unknown en Completed.

Esta elección produce una demo funcional del workflow y de sus límites, fácil de presentar sin costos/proveedores. El LLM real, Qdrant, Angular, callbacks de un tenant Genesys y SQL Server completo permanecen fuera de este baseline; no se cierran sus tickets por equivalencia. API usa ActivitySource/Meter y audit de códigos sin chat/secretos; export OTel y dashboard externo son gates posteriores. Python evalúa outputs reales del simulador sobre escenarios declarados, separando el resultado de seguridad del rendimiento lingüístico de un LLM.

Refs: FR02/10–22, AC01–03/17–28/30/32, INV01–08, ADR-004/005/008/009/010.
