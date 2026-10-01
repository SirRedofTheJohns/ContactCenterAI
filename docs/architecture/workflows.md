# Flujos, estados e invariantes

Estos diagramas definen transiciones y fronteras del MVP. No son llamadas a implementaciones existentes. RAG y tools producen evidencia/propuestas; solo el workflow servidor puede cambiar estado. Un turno tiene una propuesta activa; las tools son secuenciales.

## Invariantes

| ID | Invariante |
|---|---|
| INV01 | Identidad y permisos proceden del backend/IdP, nunca del LLM o texto de usuario. |
| INV02 | Cancelación requiere recurso autorizado, oferta vigente, confirmación propia consumida, estado/versión/política actuales y validación fuente. |
| INV03 | Solo resultado fuente Completed puede comunicar éxito. Submitted, Pending y Unknown nunca equivalen a cancelado. |
| INV04 | Un commandId no cambia payload ni representa dos efectos; una oferta se consume como máximo una vez. |
| INV05 | Documento/chunk no autorizado, revocado o vencido no entra al modelo ni se devuelve por cita. |
| INV06 | HumanOwned bloquea nuevos mensajes/tools de IA; callback viejo no cambia ownership. |
| INV07 | Ningún HTTP 202 reconoce evento/comando antes de persistencia SQL durable. |
| INV08 | Reglas, ACL y consecuencias se conservan al cambiar idioma/modelo/prompt. |

## Particularidades de transición

`ActionPending` es estado de conversación; Command tiene su propia máquina. Transferir o cerrar el navegador no borra ni revierte un comando Submitted. Unknown permanece hasta observación autoritativa o intervención humana documentada; abrir un caso humano no implica resolver la reserva.

HandoffPending permite solo mensajes deterministas de estado mientras se solicita la transferencia; la IA deja de proponer nuevas operaciones. Tras acknowledgment, cambia epoch y HumanOwned. Los outbox anteriores verifican epoch antes de emitir. No hay reactivación automática del bot en v0.1; retomar IA exige nueva conversación/control y un diseño posterior de reclaim.

Eventos de aceptación de handoff contienen requestId/epoch actuales; no se autentican por esos IDs únicamente. La máquina se aplica con compare-and-swap y event dedupe. Para canal de entrega sin dedupe verificable, el mock ensaya Unknown y recuperación; no se promete exactamente una vez.

La secuencia de cancelación muestra ruta feliz y dos fallos. Los checks locales antes de confirmar reducen conflictos, pero la última validación fuente sigue siendo obligatoria. El resumen y la respuesta final usan el ledger de hechos; no se deducen de conversación libre.

<!-- Workflow diagrams are embedded from canonical .mmd files during packaging. -->

<!-- BEGIN GENERATED DIAGRAMS -->

## Secuencia — RAG

```mermaid
sequenceDiagram
  actor Customer
  participant API
  participant Access
  participant Knowledge
  participant Qdrant
  participant SQL
  participant LLM
  Customer->>API: Ask public or authorized policy question
  API->>Access: Verify principal or public scope
  Access-->>API: Server-derived tenant and ACL
  API->>Knowledge: Query with permitted scope and language
  Knowledge->>Qdrant: Filter tenant, publication and ACL, return IDs
  Qdrant-->>Knowledge: Candidate version and chunk IDs
  Knowledge->>SQL: Revalidate ACL, validity, active version, load text
  SQL-->>Knowledge: Only authorized evidence and citation IDs
  alt Adequate non-conflicting evidence
    Knowledge-->>API: Evidence set and allowed citations
    API->>LLM: Minimal prompt with evidence treated as data
    LLM-->>API: Draft answer and citation IDs
    API->>Knowledge: Check references and factual coverage
    Knowledge-->>API: Validated answer or reject
    API-->>Customer: Grounded answer or safe abstention
  else Missing or conflicting evidence
    API-->>Customer: Abstain and offer human handoff
  end
```

## Secuencia — Cancelación

```mermaid
sequenceDiagram
  actor Customer
  participant API
  participant Gateway
  participant Source
  participant Policy
  participant SQL
  participant Worker
  Customer->>API: Request cancellation for own reservation
  API->>Gateway: Validated proposal with verified principal
  Gateway->>Source: Authorized read of reservation
  Source-->>Gateway: Confirmed state, rowVersion and checkInUtc
  Gateway->>Policy: Evaluate CP-001 v1 with server clock
  Policy-->>Gateway: Eligible, zero penalty or denial
  Gateway->>SQL: Persist bound offer with five-minute expiry
  Gateway-->>Customer: Display offer and confirmation control
  Customer->>API: Authenticated confirm offerId and idempotency key
  API->>SQL: Atomically revalidate, consume offer, insert command, audit and outbox
  SQL-->>API: Durable Pending operationId
  API-->>Customer: 202 Pending, no success claim
  Worker->>SQL: Lease command and persist Submitted
  Worker->>Source: Cancel with commandId and expected version
  Source->>Source: Revalidate authorization, active policy and state
  alt Authoritative completed result
    Source-->>Worker: Completed and source reference
    Worker->>SQL: Commit result, audit and response outbox
    Customer->>API: Poll operation or conversation events
    API-->>Customer: Verified cancellation confirmation
  else Version or policy conflict
    Source-->>Worker: Conflict or Rejected
    Worker->>SQL: Persist failure and safe response
    API-->>Customer: Explain and request fresh preview if appropriate
  else Timeout after possible source commit
    Worker->>SQL: Persist Unknown and reconciliation job
    API-->>Customer: Outcome uncertain, do not repeat action
  end
```

## Secuencia — Reconciliación

```mermaid
sequenceDiagram
  participant Worker
  participant SQL
  participant Source
  participant Case
  Worker->>SQL: Claim expired Submitted lease or Unknown command
  Worker->>Source: Query outcome by stable commandId
  alt Source confirms Completed
    Source-->>Worker: Completed and reference
    Worker->>SQL: Persist Completed, audit and permitted delivery
  else Source confirms Rejected
    Source-->>Worker: Rejected or Conflict
    Worker->>SQL: Persist final non-success and explanation
  else Source guarantees NotFound and dedupe
    Source-->>Worker: Definitively not accepted
    Worker->>Source: Resend same commandId after current checks
    Source-->>Worker: Authoritative result or uncertain timeout
    Worker->>SQL: Persist observed result
  else Still uncertain or source unavailable
    Source-->>Worker: Pending or timeout
    Worker->>SQL: Keep Unknown and bounded next query
    Worker->>Case: After sixty seconds create human review
    Case-->>SQL: Assigned review with operationId
  end
```

## Secuencia — Handoff

```mermaid
sequenceDiagram
  actor Customer
  participant API
  participant SQL
  participant Worker
  participant Adapter
  participant Human
  Customer->>API: Request human assistance
  API->>SQL: Persist HandoffPending, requestId, epoch and minimum context
  SQL->>SQL: Invalidate unused offer and stop new tools
  API-->>Customer: Transfer requested, not yet connected
  Worker->>SQL: Lease handoff outbox and verify epoch
  Worker->>Adapter: Request handoff with stable requestId and contextRef
  alt Adapter acknowledges matching request and epoch
    Adapter->>API: Authenticated handoff accepted event
    API->>SQL: CAS to HumanOwned and increment epoch
    Adapter->>Human: Route interaction
    Human->>API: Fetch assigned context and pending operation
    API-->>Human: Verified facts and explicit uncertainty
  else Timeout or failure
    Worker->>SQL: Keep HandoffPending and record failure
    API-->>Customer: Transfer pending and safe fallback
  end
  Adapter->>API: Duplicate or stale callback
  API->>SQL: Dedupe and reject stale epoch, no AI reactivation
```

## Secuencia — Ingesta

```mermaid
sequenceDiagram
  actor Editor
  actor Reviewer
  participant API
  participant SQL
  participant Worker
  participant Embedding
  participant Qdrant
  Editor->>API: Submit curated Markdown and metadata
  API->>SQL: Validate size, content, ACL, translation group, save Draft
  Editor->>API: Submit validated Draft for review
  API->>SQL: Freeze content and mark PendingReview
  Reviewer->>API: Review as different verified principal
  API->>SQL: Persist ApprovedForIndexing and job
  Worker->>SQL: Lease ingestion and load immutable version
  Worker->>Embedding: Embed bounded approved chunks
  Embedding-->>Worker: Vectors and model version
  Worker->>Qdrant: Stage complete version with payload indexes
  Qdrant-->>Worker: Index generation verified
  Worker->>SQL: Atomically publish active version and supersede previous
  SQL-->>API: Published evidence available
  Reviewer->>API: Revoke compromised version
  API->>SQL: Immediately mark Revoked
  Worker->>Qdrant: Remove stale projection asynchronously
```

## Estado — Conversación

```mermaid
stateDiagram-v2
  [*] --> New
  New --> Active: session established
  Active --> IdentityRequired: private action requested
  IdentityRequired --> Active: trusted login completed
  IdentityRequired --> HandoffPending: failed identity or human request
  Active --> WaitingForConfirmation: eligible offer displayed
  WaitingForConfirmation --> Active: reject or expire
  WaitingForConfirmation --> ActionPending: authenticated confirmation consumed
  ActionPending --> Active: authoritative final outcome
  Active --> HandoffPending: explicit request or safe escalation
  WaitingForConfirmation --> HandoffPending: invalidate unused offer
  ActionPending --> HandoffPending: include in-flight operation
  HandoffPending --> HandoffPending: timeout or retry same request
  HandoffPending --> HumanOwned: matching accepted event and epoch
  HumanOwned --> Closed: human closes interaction
  Active --> Closed: explicit close with no pending operation
  Closed --> [*]
```

## Estado — Comando

```mermaid
stateDiagram-v2
  [*] --> Pending
  Pending --> Submitted: durable dispatch lease and current checks
  Pending --> Rejected: authorization lost or kill switch
  Submitted --> Completed: source confirms completed
  Submitted --> Rejected: source confirms rejection
  Submitted --> Conflict: source version or policy changed
  Submitted --> Unknown: timeout or worker crash
  Unknown --> Completed: authoritative reconciliation
  Unknown --> Rejected: authoritative reconciliation
  Unknown --> Conflict: authoritative reconciliation
  Unknown --> Submitted: definitive not-accepted plus source dedupe
  Unknown --> Unknown: unresolved with human review
  Completed --> [*]
  Rejected --> [*]
  Conflict --> [*]
```

## Sesión y recepción B03/B04


```mermaid
sequenceDiagram
  actor Customer
  participant BFF
  participant IdP
  participant SQL
  Customer->>BFF: HTTPS GET CSRF bootstrap
  BFF-->>Customer: Antiforgery token and secure cookie
  Customer->>BFF: POST anonymous session with Origin and CSRF
  BFF->>SQL: Persist opaque guest session
  BFF-->>Customer: HttpOnly Secure session cookie
  Customer->>BFF: POST login with Origin and CSRF
  BFF->>IdP: Authorization Code and PKCE S256
  IdP-->>BFF: Validated callback with state and nonce
  BFF->>SQL: Bind trusted issuer and subject; rotate session; adopt owned guest conversations
  BFF-->>Customer: New cookie; no saved tokens
  Customer->>BFF: POST conversation with new CSRF and idempotency key
  BFF->>SQL: Revalidate active session and binding
  BFF->>SQL: SERIALIZABLE conversation plus idempotency plus audit
  SQL-->>BFF: Committed conversation ID
  BFF-->>Customer: 201 and version
  Customer->>BFF: POST message with If-Match and clientMessageId
  BFF->>SQL: Revalidate owner or active assignment; deduplicate
  BFF->>SQL: Commit sanitized message plus Pending inbox plus audit
  SQL-->>BFF: Committed receipt
  BFF-->>Customer: 202 Pending; no business success
```
