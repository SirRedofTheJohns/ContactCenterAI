# Contratos internos de API, tools y transacciones v0.1

**Entrega actual:** [demo local v0.6](../progress/demo-v0.6.md): SQLite, intención con IA real local o simulador explícito, contact center mock, evidence por temas, confirmación y recovery. Las secciones empresariales siguientes mantienen el objetivo y sus gates.


Contrato objetivo de diseño; el subset de sesión y recepción ya tiene handlers, mientras reservas/tools/eventos siguen pendientes. JSON sobre HTTPS fuera de la excepción de presentación loopback. Versionado `/v1`; UTF-8, UTC ISO 8601, IDs opacos generados por servidor. DTOs cerrados, límites de longitud y allowlists validados en backend. Los contratos machine-readable delimitan la superficie efectivamente entregada.

B02 formaliza [OpenAPI del host](host-v0.1.openapi.json) y [borrador de conversaciones](conversations-v0.1.openapi.json), usando [OpenAPI 3.1.0](https://spec.openapis.org/oas/v3.1.0.html). Solo health se implementa en B02; `/v1` permanece planned. Límite inicial del DTO de mensaje: 2.000 caracteres y clientMessageId UUID. CSRF/bootstrap de sesión se completa en B03 y persistencia en B04 antes de promover ese borrador a handlers. El contrato no habilita recepción en memoria ni identidad enviada por cliente.

Actualización: B03/B04 implementan ese subset con sus gates empresariales pendientes. La [especificación de demo v0.2](demo-v0.2.openapi.json) combina health/sesión/conversación y documenta el origen `http://127.0.0.1:7452`, cookie demo sin Secure en loopback y etiqueta de sesión derivada del servidor. SQLite persistente reemplaza al adapter SQL solo en ese perfil, según ADR-014. No hay contratos de cancelación o LLM implementados por añadir la UI.

## API del producto

B05 entrega [demo v0.3](demo-v0.3.openapi.json) y [contrato interno fuente](source-v0.3.openapi.json), con [evidencia](../progress/b05-source.md). Consulta de reservas bajo conversación propia y Customer verificado; MemberRef procede del servidor. Fuente C# independiente con SQLite separada y clave de servicio privada. Comandos fuente existen para el futuro worker; producto todavía no expone cancelación ni oferta consumible. Los endpoints de negocio restantes de la tabla son objetivo, no handlers entregados.

| Operación | Input relevante | Output / permiso |
|---|---|---|
| POST /v1/conversations | `{language: es|en}`; Idempotency-Key | 201 `{conversationId,version,ownership}`; anónimo permitido, sesión opaca segura |
| POST /v1/conversations/{id}/messages | `{clientMessageId,text}`; If-Match version | 202 `{messageId,turnId,status: Pending}` después de inbox SQL; dueño/asignado |
| GET /v1/conversations/{id}/events?after={cursor} | cursor opaco | 200 eventos sanitizados ordenados por secuencia interna, dueño/asignado |
| POST /v1/conversations/{id}/cancellation-offers | `{reservationId}`; principal verificado | 201 offer o 422 ineligible; propio/asignado; sin efecto de negocio |
| POST /v1/conversations/{id}/confirmations | `{offerId,decision: confirm|reject}`; Idempotency-Key; If-Match offer version | 202 `{operationId,status: Pending}` si confirm; 200 Rejected si reject; principal de oferta |
| GET /v1/operations/{id} | operationId | 200 `{status,reasonCode,sourceReference?}`; mismo propietario/asignación |
| POST /v1/conversations/{id}/handoff | `{reasonCode}`; Idempotency-Key | 202 `{requestId,status: HandoffPending}`; dueño/asignado |
| GET /v1/conversations/{id}/context | conversationId | 200 hechos/resumen/operaciones, solo empleado asignado; sin credenciales |
| GET /v1/knowledge/{docId}/versions/{v}/sections/{s} | IDs de cita | 200 extracto permitido; mismos ACL/vigencia que retrieval, 404 si prohibido |
| POST /v1/feedback | `{answerId,rating,reasonCode,comment?}` | 201 revisión; quien puede ver respuesta; comentario sanitizado <=500 chars |
| POST /v1/knowledge/versions | Markdown curado + metadata | 201 Draft; KnowledgeEditor, <=256 KB; sin URL remota arbitraria |
| POST /v1/knowledge/versions/{id}/submit | Versión Draft validada | 200 PendingReview; creador KnowledgeEditor; contenido congelado para revisión |
| POST /v1/knowledge/versions/{id}/review | `{decision: approve|reject}` | 200; KnowledgeReviewer distinto de creador; publish worker si approve |
| POST /v1/knowledge/versions/{id}/revoke | `{reasonCode}` | 200 revocación SQL inmediata; Reviewer, audit |
| PUT /v1/operations-controls/mutations | `{enabled,reasonCode}` | 200; OperationsAdmin; no override de negocio |
| GET /v1/evaluation-exports/{runId} | Run sintético y manifest | 200 artefactos sanitizados; OperationsAdmin del perfil local, sin acceso a DB fuente |

Errores comunes: 400 schema/límites; 401 token inválido; 403 acción de rol denegada; 404 recurso ausente/no visible; 409 versión/idempotency/ownership conflict; 422 regla o confirmación vencida; 429 cuota; 503 no aceptación durable. Error tipo Problem Details con `code`, `correlationId`, `retryable`; sin stack, token ni PII. 202 significa aceptación durable, nunca éxito de negocio.

Para navegador: login OIDC Authorization Code + PKCE; session/BFF con cookie HttpOnly, Secure y SameSite; mutaciones con protección CSRF y origin permitido. Tokens no se almacenan en localStorage ni se entregan al modelo. Servicio adapter autentica máquina y transporta evidencia de identidad separada, no suplanta a un miembro por conocer conversationId.

## Oferta de cancelación

```json
{
  "offerId": "offer-demo-001",
  "conversationId": "conv-demo-001",
  "reservationId": "RES-001",
  "reservationVersion": "rv-7",
  "policyVersion": "CP-001:v1",
  "eligible": true,
  "reasonCode": "FREE_CANCELLATION_WINDOW",
  "penaltyMinorUnits": 0,
  "currency": "USD",
  "expiresAt": "2026-10-01T12:05:00Z",
  "version": 1
}
```

El servidor vincula además `tenantId`, principal autenticado, membership/asignación, ownership epoch, hora fuente y hash del contenido mostrado. No permite que el modelo o cliente reemplacen esos valores. La oferta vence a 5 min y solo vive bajo el epoch de IA actual. Una confirmación escrita por IA no puede crear el acto de confirmación. UI re-renderiza el contenido servidor; requiere acción confirm/reject autenticada. En chat libre «sí» muestra/recuerda ese control. El v0.1 de un canal Genesys mock usa el mismo control; canal real debe diseñar prueba equivalente de consentimiento, no asumir compatibilidad.

## Tools expuestas al modelo

| Tool | Argumentos propuestos | Condición / resultado mínimo |
|---|---|---|
| search_knowledge | `{query,language}` | gateway inyecta tenant/ACL; IDs/secciones permitidas, extractos y versiones |
| get_reservation | `{reservationId}` | identidad y recurso autorizados; status, instantes y propertyCode mínimos |
| preview_cancellation | `{reservationId}` | identidad + recurso; oferta CP-001 y reasonCode |
| request_handoff | `{reasonCode}` enum cerrado | gateway decide queue interna según idioma/reason; LLM no manda queueId |

No tools de login, grant_role, arbitrary_http, SQL, pago, listar todos los miembros o cancelación ejecutable por el modelo. `CancelReservation` es un comando interno del worker, disparado por confirmación servidor. Proposal inválida no se repara silenciosamente en una mutación; se deniega y pide información si procede. Output schema del modelo incluye `intent`, `language`, `proposal` o `answer`, `citationIds`; no `authorized=true` ni permisos. Principal, fechas actuales, budget y commandId vienen del backend.

Se solicita schema estricto cuando el proveedor lo soporte, `additionalProperties=false`, enums cerrados, y validación semántica adicional local. Las recomendaciones del proveedor sobre function calling no sustituyen permisos ni reglas de negocio. [OpenAI function calling](https://developers.openai.com/api/docs/guides/function-calling).

## Comando fuente y resultados

Comando `CancelReservation`: `{commandId,reservationId,expectedReservationVersion,policyVersion,requestedAtUtc}` más contexto de autorización validado en el canal servicio. Fuente revalida propiedad/delegación, estado, CP-001 activa y ventana. No confía en `eligible=true` local.

Resultado fuente: `Accepted|Completed|Rejected|Conflict|NotFound|Unknown` + commandId + currentVersion + reasonCode + sourceReference cuando Completed. Lectura de resultado por commandId soporta `Pending|Completed|Rejected|NotFound`. NotFound solo permite reenvío si la fuente **garantiza** que no aceptó el comando y conserva dedupe; ausencia temporal/eventual de registro se trata Unknown. Fuente sin tal garantía obliga reconciliación humana.

## Protocolo durable e idempotencia

1. Confirmación: transacción SQL atómica valida principal/ACL/epoch/versión/vencimiento, consume oferta, inserta Confirmation, Command(Pending), AuditEvent e Outbox. Unique `(tenantId,principalId,route,idempotencyKey)` y unique offerId consumido. Repetición con mismo hash devuelve misma operación; distinto hash 409.
2. Worker reclama lease con compare-and-swap/rowVersion. Antes de enviar fija Submitted durable. Si cae a partir de ese punto, no deduce si envió: consulta resultado fuente antes de nuevo envío.
3. Fuente usa unique commandId y constraint sobre transición Confirmed→Cancelled para efecto único. La clave no es solo conversationId; distintas pestañas no deben duplicar negocio.
4. Worker registra Completed/Rejected/Conflict/Unknown y outbox de mensaje/contexto en una transacción. No hay transacción distribuida ni promesa de entrega exactly-once.
5. Dispatcher verifica ownership epoch antes de enviar respuesta. Outbox e inbox son at-least-once; dedupe source/client evita efectos repetidos. Si destino no garantiza dedupe, estado de entrega Unknown se reconcilia o se muestra por polling; no asumir mensaje exactamente una vez.
6. Idempotency records operacionales se conservan 30 días. Fuente conserva tombstone de commandId durante toda la vida demo de la reserva; comandos completados no se reutilizan. Un replay fuera de ventana no obtiene nueva autorización: oferta consumida/expirada y estado fuente siguen bloqueándolo.

Reconciliación: consultar a 5, 15, 30 y 60 s desde timeout, con límite y jitter; luego caso humano. No prolongar el deadline de turno. Cancelación de conversación/HTTP no cancela un efecto fuente ya Submitted.


## Contrato vigente de presentación

[OpenAPI v0.8](demo-v0.8.openapi.json) añade modo de recuperación y agregados de operación restringidos a OperationsAdmin. No hay endpoint que acepte member/roles para la búsqueda ni tool de cancelación del modelo. Respuestas factuales siguen siendo extractos aprobados y plantillas fuente.
