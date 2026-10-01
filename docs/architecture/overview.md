# Arquitectura, límites y C4

**Entrega actual:** [demo local v0.6](../progress/demo-v0.6.md): SQLite, intención con IA real local o simulador explícito, contact center mock, evidence por temas, confirmación y recovery. Las secciones empresariales siguientes mantienen el objetivo y sus gates.



## Estilo y unidad de despliegue

Se adopta un monolito modular ASP.NET Core .NET 10 para API/flujo conversacional y un worker .NET independiente para trabajo durable. Los módulos comparten Application/Domain por librerías; son componentes, no microservicios. Domain no depende de HTTP, SDK de Genesys, LLM ni EF Core. API y worker dependen de puertos de Application; Infrastructure implementa los adapters. La separación worker permite recuperar comandos sin mantener abierto un request.

Los diagramas C4 se expresan con `flowchart` Mermaid para portabilidad. Los niveles, fronteras y tipos están rotulados; no se usa la sintaxis C4 experimental. Deployment es una vista complementaria. Todo representa diseño propuesto, no infraestructura desplegada.

## Módulos y autoridad

| Módulo | Responsabilidad y autoridad |
|---|---|
| Identity / Access | Principal verificado, rol, recurso, asignación y ACL; deniega por defecto |
| Conversations | Mensajes sanitizados, estado, secuencia y ownership epoch; workflow determinista |
| AI Orchestration | Contexto mínimo, intent/proposals, límites y respuesta; sin autoridad transaccional |
| Knowledge | Registro de versiones/ACL, recuperación, citas, vigencia y feedback |
| Secure Tool Gateway | Allowlist, schema, permisos y validación; ejecuta puertos de lectura/preview |
| Reservation Policy | CP-001 v1, elegibilidad y oferta; reglas puras y testables |
| Command / Confirmation | Oferta consumible, idempotencia, ledger, leases y reconciliación |
| Handoff | Contexto, solicitud durable, epoch y transición a humano |
| Audit / Telemetry | Decisiones y resultados sanitizados; SQL audit durable, OTel técnico |

El sistema fuente de reservas es autoritativo sobre la transacción. El Policy Engine local puede anticipar elegibilidad, pero la fuente vuelve a validar; el preview no es commit. SQL operativo conserva lo que el middleware observó, sin afirmar que una fila local cambió la reserva externa.

## Puertos previstos

`ILanguageModel`, `IEmbeddingGenerator`, `IKnowledgeIndex`, `IReservationSystem`, `IContactCenterAdapter`, `IIdentityContext`, `IClock`, `IOperationalStore`, `IAuditWriter`. Son nombres de diseño, no interfaces implementadas. Contracts neutrales con DTOs de dominio, reason codes y CancellationToken técnico. Cancelar el token de transporte no implica revertir la transacción fuente.

Modelo/proveedor configura intent y respuesta. No se adopta framework agentic en v0.1: un workflow explícito es más fácil de auditar que un planner autónomo. Structured output estricto ayuda al formato y la validación local protege significado/permisos. Las capacidades del modelo exacto se verifican en spike, sin elegirlo por popularidad.

## Persistencia y concurrencia

SQL Server Operational DB: conversaciones, asignaciones, ofertas, confirmaciones, comandos, conocimiento Markdown/metadata, feedback, inbox/outbox y audit. Simulator DB separada: miembros/reservas sintéticos y command receipts. Mismo motor local puede alojar ambas, con identidades/grants separados; API solo habla con fuente vía HTTP, no hace JOIN ni update en su DB.

Qdrant mantiene vectores y referencias de chunks como proyección reconstruible. SQL guarda el corpus/version activa y permisos. No se autoriza con payload del índice únicamente. No Redis ni message broker: worker consulta inbox/outbox con leases y rowVersion en SQL. Escalar a broker solo cuando backlog/latencia/carga medida lo justifique.

Conversaciones procesan un turno activo por conversationId usando lease durable/version. Events externos se deduplican por provider+integration+eventId; recepción genera secuencia interna. Hora de evento no otorga precedencia sobre confirmación/handoff. Eventos de negocio comparan requestId y epoch. No se presume orden global de proveedores; si falta un prerrequisito el evento se retiene/rechaza con código en vez de aplicar una transición imposible.

## Ruta de mutación

La cancelación interna nace de una confirmación autenticada del control UI; no es una tool de escritura expuesta al modelo. SQL consume oferta e inserta intención antes del dispatch. Worker aplica kill switch, revalida recurso y llama fuente con commandId. Resultado incierto se reconcilia. Solo source Completed genera mensaje de éxito por plantilla. La IA puede reformular lectura/explicación, pero no cambiar estado/veredicto.

## Perfiles

- **Local deterministic:** IdP de pruebas, source/contact mocks, corpus sintético y LLM stub. Reproducible sin cuenta externa; no demuestra calidad de IA real.
- **Local live AI:** mismas fuentes mock, un proveedor/modelo y embeddings fijados, budget; mide calidad/costo del modelo sin datos reales.
- **Sandbox Genesys posterior:** adapter real y Architect configurados en tenant, sin reemplazar simulador de reservas inicialmente. Requiere gate de integración.
- **Producción:** diseño v0.1 no cubre HA, regulaciones, infraestructura corporativa ni políticas comerciales reales; requiere nuevo baseline.

## Dependencias y spikes previos a cada slice

| Dependencia | Selección / prueba de compatibilidad |
|---|---|
| .NET | .NET 10 LTS; ASP.NET Core y EF Core del mismo major; SDK/patch fijados en M1 |
| SQL Server | Imagen/edición local compatible y licencia revisada en M1; migrations separadas; no optimización vectorial en SQL |
| OIDC local | Discovery, issuer/audience, PKCE y claim mapping; no IdP escrito a mano |
| Qdrant | Imagen y cliente .NET fijados; prueba ACL filter, IDs/version y rebuild |
| AI provider | Schema support, tool responses, embeddings, retention y timeout; modelo por eval, no alias móvil |
| Genesys | Tenant/region/licencia, transporte, auth y handoff observables; mock por contrato neutral |
| Python / Angular | Versiones soportadas y locks al implementar evaluación/UI; no son dependencias del Domain |

El número de patch y imágenes se determina al ejecutar cada spike con fuentes actuales, no se congela a ciegas meses antes del código. Si falla una capacidad necesaria, se modifica ADR/baseline antes del slice afectado.

<!-- C4 diagrams are embedded from canonical .mmd files during packaging. -->

<!-- BEGIN GENERATED DIAGRAMS -->

## C4 L1 — Contexto

```mermaid
flowchart LR
  customer["Person: Customer ES or EN"]
  agent["Person: Assigned agent or supervisor"]
  editor["Person: Knowledge editor and reviewer"]
  ops["Person: Operations admin"]
  platform["Software System: ContactCenterAI"]
  center["External System: Contact center mock or future Genesys"]
  source["External System: Member and reservation simulator"]
  identity["External System: OIDC identity provider"]
  model["External System: LLM and embedding provider"]
  customer -->|"Web chat and explicit confirmation"| platform
  customer -->|"Future managed digital channels"| center
  agent -->|"Authorized knowledge and handoff context"| platform
  agent -->|"Accept interaction"| center
  editor -->|"Govern synthetic knowledge"| platform
  ops -->|"Observe and stop mutations"| platform
  center -->|"Authenticated events and identity evidence"| platform
  platform -->|"Responses and handoff request"| center
  platform -->|"Authorized read and versioned command"| source
  platform -->|"Verify principal and claims"| identity
  platform -->|"Minimal context and untrusted proposals"| model
```

## C4 L2 — Contenedores

```mermaid
flowchart TB
  customer["Person: Customer or assigned employee"]
  idp["External: OIDC IdP"]
  llm["External: LLM and embeddings"]
  source["External: Reservation simulator HTTP"]
  center["External: Contact center mock or Genesys"]
  subgraph systemBoundary["ContactCenterAI system boundary"]
    ui["Container: Angular web UI"]
    api["Container: ASP.NET Core API and BFF"]
    worker["Container: .NET durable worker"]
    sql[("Container: SQL Server Operational DB")]
    index[("Container: Qdrant index")]
    telemetry["Container: OTel collector and local dashboard"]
  end
  eval["Offline tool: Python evaluator"]
  customer --> ui
  ui -->|"Session and API"| api
  api --> idp
  center -->|"Authenticated ingress"| api
  api -->|"State, commands, inbox and outbox"| sql
  worker -->|"Lease and commit durable jobs"| sql
  api -->|"RAG candidates"| index
  worker -->|"Versioned indexing"| index
  api -->|"Intent and response"| llm
  worker -->|"Embedding and summaries"| llm
  api -->|"Read and preview"| source
  worker -->|"Command and reconciliation"| source
  worker -->|"Outbound and handoff"| center
  api --> telemetry
  worker --> telemetry
  eval -->|"Synthetic scenarios"| api
  api -->|"Sanitized evaluation exports"| eval
```

## C4 L3 — Componentes API

```mermaid
flowchart TB
  subgraph apiBoundary["ASP.NET Core API process - components"]
    ingress["HTTP and contact ingress adapters"]
    access["Identity and resource authorization"]
    state["Conversation workflow and ownership"]
    ai["Bounded AI orchestrator"]
    knowledge["Knowledge retrieval and citation validator"]
    gateway["Secure tool gateway"]
    policy["Deterministic cancellation policy"]
    confirmation["Confirmation and command ledger"]
    handoff["Handoff application service"]
    store["SQL repositories, inbox, outbox and audit"]
  end
  model["LLM adapter: proposals only"]
  rag["Qdrant adapter: candidate IDs"]
  reservation["Reservation HTTP adapter"]
  worker["Separate worker: dispatch and reconcile"]
  ingress --> access
  access --> state
  state --> ai
  ai --> model
  ai --> knowledge
  ai --> gateway
  gateway --> access
  gateway --> policy
  gateway --> reservation
  gateway --> handoff
  knowledge --> access
  knowledge --> rag
  knowledge --> store
  state --> store
  policy --> store
  ingress -->|"Authenticated confirmation control"| confirmation
  confirmation --> access
  confirmation --> store
  handoff --> store
  store -->|"Durable commands and events"| worker
```

## Deployment — Perfil local

```mermaid
flowchart TB
  browser["Browser: customer and assigned agent"]
  cloud["Optional external LLM provider"]
  subgraph machine["Local machine: proposed container network"]
    api[".NET API and BFF - localhost ingress"]
    worker[".NET worker - no public port"]
    idp["Local test OIDC provider"]
    sql[("SQL Server: Operations DB and Simulator DB")]
    qdrant[("Qdrant - internal access")]
    source[".NET source simulator - internal HTTP"]
    center[".NET contact center mock"]
    stub["Deterministic LLM stub"]
    otel["OTel collector and dashboard"]
    python["Python offline evaluation runner"]
  end
  future["Future Genesys sandbox: separate gated profile"]
  browser --> api
  api --> idp
  api -->|"Operations DB only"| sql
  worker -->|"Operations DB only"| sql
  source -->|"Simulator DB only"| sql
  api --> qdrant
  worker --> qdrant
  api --> source
  worker --> source
  center --> api
  worker --> center
  api --> stub
  worker --> stub
  api -.->|"Live AI profile"| cloud
  worker -.->|"Live AI profile"| cloud
  api --> otel
  worker --> otel
  python --> api
  future -.->|"Not deployed in v0.1"| api
```
