# CI/CD, observabilidad y soporte

**Entrega actual:** [demo local v0.6](../progress/demo-v0.6.md): SQLite, intención con IA real local o simulador explícito, contact center mock, evidence por temas, confirmación y recovery. Las secciones empresariales siguientes mantienen el objetivo y sus gates.


Diseño de entrega posterior al freeze; en Sprint 0 se verifican documentos, enlaces y consistencia. No existe pipeline de aplicación ejecutado ni entorno desplegado.

## Version bundle y CI/CD

Release identifica commit + DB migrations + source contract + policyVersion + promptVersion + modelId + toolSchemaVersion + corpus/indexGeneration + embedding/chunking + adapterVersion. Actualizar un prompt/corpus también es un cambio de producto.

| Gate | Checks obligatorios | Fallo implica |
|---|---|---|
| G0 — documentación | Referencias, Mermaid, trazabilidad FR→AC→ADR, freeze y cambios de alcance | No implementar requisito inconsistente |
| G1 — PR de código | Build .NET, formatting/static analysis, unit rules, API/schema tests y secret/dependency scan | No merge |
| G2 — integración local | SQL/source/adapter mocks, idempotencia, crash, concurrency, ACL, handoff | No promover |
| G3 — IA | Mock contract eval + ES/EN live holdout y costo/latencia; safety completa | No release si gate crítico falla |
| G4 — staging/demo | Migraciones, smoke, restore rehearsal, dashboards/alerts, rollback compatible | No entregar demo como verificada |
| G5 — Genesys real opcional | M6 tenant gates, Architect export y evidencia sanitizada | No marcar TenantValidated |
| G6 — producción | Revisión negocio/seguridad, DPA/retention/region, HA y soporte | No datos reales ni deployment productivo |

Plan GitHub Actions: permisos mínimos read por defecto, entornos separados y revisión de PR; acciones fijadas a commits, dependencias lock, SBOM y escaneo de vulnerabilidades. Secretos de live eval solo en entorno protegido, nunca PR de fork no confiable. Despliegue usa identidad OIDC del workload cuando hosting lo permita. No seleccionar cloud ni publicar desde esta fase.

Migraciones: expand/contract y compatibilidad temporal API/worker; no drop de columnas en mismo release que deja atrás workers. Rollback revierte app/config/prompt/index pointer compatibles, no un rollback automático destructivo de DB ni de una cancelación ya ejecutada. Fuente conserva command receipt. Ensayar upgrade/rollback bajo carga mock y restauración local; medir RPO/RTO antes de prometerlos.

## Observabilidad y auditoría

OpenTelemetry en API/worker/adapters; traceparent en llamadas cuando sea soportado, correlationId y links para jobs asincrónicos. SQL audit persiste eventos de intención, auth denial, offer/confirmation, dispatch, outcome, reconcile y handoff. OpenTelemetry es técnico y no sustituye auditoría durable.

Campos permitidos: traceId/correlationId, opaque aggregateRef, action/reasonCode, intent enum, language, provider/model/prompt/rules versions, tool enum, status, latency, retries, token counts y estimatedCost. Sin payload HTTP, cuerpo libre, tokens, nombres/emails, secretos o chain-of-thought. Auditoría runtime append-only; acceso y retención gobernados. No afirmar tamper-proof sin almacenamiento específico.

Métricas de cardinalidad limitada: turn duration, source errors, retrieval no-evidence, invalid citations, denied tool attempts, command outcomes, Unknown age, inbox/outbox depth/oldest age, lease conflicts, handoff request/accept/failure, token counts, costo estimado y fallback reason; etiquetas por idioma/status/tool enum, no conversationId/memberId. Containment cuenta resolución segura sin humano **sobre interacciones elegibles**; no recompensa evitar handoff necesario. Adoption usa usuarios sintéticos/activos, no identifica personas en dashboards.

## Alertas locales futuras

| Alerta | Umbral inicial | Acción |
|---|---|---|
| Comando incierto | Unknown >60 s | Caso humano; reconciliar, sin repetir POST |
| Handoff pendiente | >30 s o failure explícito | Estado claro, retry durable/control y fallback |
| Backlog worker | oldest pending >30 s | Revisar leases/worker/SQL |
| Fuente no disponible | 5 fallos /30 s | Circuit open, bloquear nuevas mutaciones |
| ACL/tool denial anómalo | >10 por min por scope acotado | Rate limit/revisar session; sin publicar identidad |
| Budget agotado | 80% warning, 100% stop live calls | Plantillas/handoff; recovery independiente del LLM |
| Safety violation observada | Cualquier false-success/leak/bypass | Kill switch inmediato y análisis incidente |

Son defaults a calibrar; no pretender que cubren una operación 24/7 real.

## Runbooks

### RB01 — Resultado Unknown

Buscar commandId en audit/source receipt con acceso autorizado; no copiar PII en tickets. Consultar outcome fuente y verificar sourceReference/rowVersion. Completed→persistir resultado y mensaje solo si epoch permite; Rejected/Conflict→explicar sin éxito; Pending/unavailable→mantener Unknown y caso humano. Solo reenviar same commandId cuando fuente pruebe not-accepted y dedupe, con checks actuales. No cambiar estado manualmente a Completed sin evidencia.

### RB02 — Handoff pendiente / doble propietario

Verificar requestId, epoch, webhook auth, queue/capability y asignación. No reactivar bot por callback tardío. Retry del mismo requestId si modalidad lo soporta; mostrar Pending. Si source command ya Submitted, reconciliar y anexar resultado al contexto humano. Tras Accepted, invalidar ofertas y suprimir outbox de epochs anteriores. Validar entrega tardía con destino/mocks; documentar incertidumbre si protocolo no puede evitarla.

### RB03 — Documento malicioso o política incorrecta

Reviewer revoca SQL, invalida cache y retira puntos de índice. Bloquear respuestas afectadas, identificar evidence IDs y validar versión de CP-001 fuente. No editar política ejecutable para «alinearla» al texto malicioso. Corregir con review distinta, generation nueva y evals. Auditar autores y scopes sin volcar contenido sensible.

### RB04 — LLM o índice caído

Usar plantillas deterministas para estado/transacción y handoff. Qdrant caído impide respuestas de conocimiento no verificadas; no usar contexto viejo como fuente automática. Reintentos de lecturas dentro del budget; ninguna llamada nueva al modelo con circuito abierto/cuota agotada. Restaurar y correr smoke/evals afectados antes de volver al perfil live.

### RB05 — SQL / worker / release

SQL caída: fail closed, no 202 ni nueva mutación. Worker caída: conservar comandos durable y retomar leases al volver; Submitted viejo se reconcilia antes de reenviar. Restore local debe preservar ledger/inbox/outbox coherentes; comparar fuente receipts tras restore para no repetir efectos externos. Si release provoca fallo safety, kill switch y rollback compatible; source effects ya committed no se «deshacen» con rollback de aplicación.
