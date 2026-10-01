# Adapter Genesys Cloud y mocks locales

**Entrega actual:** [demo local v0.6](../progress/demo-v0.6.md): SQLite, intención con IA real local o simulador explícito, contact center mock, evidence por temas, confirmación y recovery. Las secciones empresariales siguientes mantienen el objetivo y sus gates.


## Decisión y nivel de evidencia

v0.1 congela un **contrato neutral de contact center**, con mock local. Candidato para bot digital en Genesys: Digital Connector, sujeto a disponibilidad/capacidades del tenant. La ruta real se decide en un spike antes de M6, sin contaminar Domain/Application con DTOs o SDK del proveedor. No hay tenant, llamadas API, export de Architect ni pruebas reales en esta entrega.

La página oficial consultada presenta Digital Connector con comunicación asincrónica, mTLS opcional y sin garantía de orden de mensajes; también conserva advertencias sobre su disponibilidad. No se infiere acceso desde esa página. [Overview de Genesys](https://help.genesys.cloud/articles/genesys-digital-bot-connector-overview/).

## Elegir el mecanismo correcto

| Mecanismo | Uso y límite |
|---|---|
| Digital Connector | Candidato para diálogo con bot de terceros en canales digitales gestionados por Genesys; comprobar contract/session/end-of-bot/routing en tenant. |
| Web Services Data Actions | Llamadas JSON acotadas para lookup/preview/contexto y branching de Architect. No alojar el loop LLM largo ni interpretar Data Action como flujo streaming. |
| Open Messaging | Conectar un canal externo hacia Genesys y manejar mensajes/recibos. No equivale al contrato bot para todos los canales nativos. |
| Platform API / SDK .NET | Operaciones concretas permitidas sobre interacción/configuración. Verificar endpoint, tipo de participante, permisos y estado; no asumir transferencia genérica. |
| Architect | Autoridad de routing/fallback de Genesys: al terminar el bot, la rama correspondiente enruta a cola/humano según diseño verificado. |

Data Actions se basa en servicios JSON y puede participar en flujos de Architect. Los timeouts/contratos concretos se verificarán en M6; el diseño usa operaciones cortas y polling de Pending. [Web Services Data Actions](https://help.genesys.cloud/articles/about-web-services-data-actions-integration/).

## Puerto neutral IContactCenterAdapter

| Capacidad | Contrato de diseño |
|---|---|
| NormalizeInbound | providerEventId, integrationId, externalConversationId, eventType, languageHint, timestamp, body sanitizado, identidad máquina y evidencia de miembro separada |
| SendResponse | deliveryId, externalConversationId, ownershipEpoch, payload seguro y replyTo; output Delivered/Pending/Unknown/Failed |
| RequestHandoff | requestId estable, conversationRef, reasonCode, language, contextRef y epoch; output Requested, no HumanConnected |
| PublishContext | ContextEnvelope versionado y mínimo, o contextRef autorizado; no tokens, datos completos de contrato o chain-of-thought |
| ObserveHandoff | Evento Accepted/Failed/Closed con requestId, provider eventId y evidencia de participante/asignación |
| GetDeliveryStatus | Recuperación de un envío incierto cuando el protocolo lo permita |

Application modela la solicitud, no un `TransferToAgent()` que suponga API universal. Adapter real traduce RequestHandoff a la terminación/resultado bot y routing Architect aplicables, o a operación Platform API verificada para esa interacción. No se afirma identidad de agente/handoff Completed hasta acknowledgment verificable. Callback requiere autenticación proveedor y matching request/epoch; una URL con conversationId no autentica.

## Envelope de contexto

```json
{
  "schemaVersion": "1.0",
  "conversationRef": "conv-demo-001",
  "handoffRequestId": "handoff-demo-001",
  "ownershipEpoch": 3,
  "language": "es",
  "reasonCode": "SOURCE_OUTCOME_UNKNOWN",
  "identityStatus": "Verified",
  "memberRef": "MEM-001",
  "verifiedFacts": ["Reservation read from source", "Cancellation submitted"],
  "pendingOperations": [{"operationId": "cmd-demo-001", "status": "Unknown"}],
  "citationRefs": ["CP-001:v1:section-3"],
  "summaryStatus": "GeneratedFromVerifiedFacts"
}
```

memberRef solo cuando empleado asignado tiene permiso; contexto en atributos Genesys debe ser mínimo. Si límites/tipos del canal no soportan envelope, enviar contextRef opaco corto al endpoint autenticado de agente, no comprimir PII en atributos o URLs públicas.

## Semántica local y resiliencia

Mock neutral con escenario parametrizable: eventos normales, duplicados, disorder, webhook inválido, tenant/integration incorrecto, 429, expiración de token, respuesta perdida, handoff aceptado/fallido, delivery Unknown y callback viejo. Acepta después de inbox durable; sourceMember del payload nunca autentica. Flujo es at-least-once; leases/version/epoch evitan transiciones incorrectas. El mock valida nuestro contrato, no wire compatibility ni permisos de Genesys.

Security: TLS al salir de loopback, autenticación específica por modalidad, secretos fuera de repo, region/base URL allowlist, rotation y permisos mínimos. Client credentials solo donde Genesys lo permita; no convertir credencial de integración en identidad del cliente. IP allowlist es defensa adicional y no sustituye autenticación. Validar schema/límites, rechazar callbacks no autenticados y evitar SSRF con URLs fijas.

## Open Messaging: deprecación verificada

La nota de Genesys del 2026-03-02 y el tracker anuncian el retiro del catch-all `POST /api/v2/conversations/messages/inbound/open` para **2026-10-05**. No diseñar una futura implementación contra él. [Release note oficial](https://help.genesys.cloud/release-notes/genesys-cloud/march-2-2026/), [tracker](https://help.genesys.cloud/announcements/?theme=simplified).

Los reemplazos documentados distinguen `/api/v2/conversations/messages/{integrationId}/inbound/open/message`, `/event` y `/receipt`. Estos endpoints aplican a Open Messaging si esa modalidad se elige; no son endpoints de nuestro producto ni prueban el contrato Digital Connector. [Aviso de endpoints](https://help.genesys.cloud/?p=338177).

## Gate de integración real M6

1. Documentar tenant, región, licencias, modalidad de canal, versiones API/SDK y permisos exactos con enlaces/fecha; obtener sandbox y credenciales por secret store.
2. Capturar wire contract vigente de Digital Connector o alternativa soportada. La especificación Developer Center consultada exige JS y no entregó contenido legible en esta sesión; no inventar campos/endpoints. [Página a validar](https://developer.genesys.cloud/commdigital/textbots/digital-botconnector-customer-api-spec).
3. Crear export/version del flujo Architect: inicio bot → llamada → estado pendiente/terminación → cola ES/EN → fallback por failure/timeout. Revisar con operador real de Genesys.
4. Contrato Data Action para lookup/preview/contextRef: prueba con botón de test y tiempos medidos; nunca cancelación sin confirmación/evidencia válida.
5. Verificar cómo se acredita identidad de miembro y consentimiento en el canal, cómo se asigna el agente y qué evento confirma handoff; si no hay prueba segura, conservar mutaciones solo en web autenticado.
6. Probar real: auth inválida, 429, mensaje duplicado/desordenado, bot termina, cola sin agente, contextRef ACL, timeout y recepción de agente. Guardar evidencia sanitizada; source mock sigue aislado.
7. Actualizar ADR-005, diagramas/contratos y baseline con hallazgos antes de marcar TenantValidated. No publicar credenciales/IDs sensibles en portafolio.
