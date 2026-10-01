# Architecture Freeze v0.11 — Public messaging channels

Status / Estado: **CLOSED for local contracts, provider fixtures and public-channel implementation**, 2026-10-01, before channel code. This is an engineering design review, not an invented customer or security approval.

**Live activation remains gated.** This freeze does not close credential setup, provider-account cost verification, cleanup acceptance or real transport evidence. It does not replace the existing v0.10 freeze.

## Review packet / Paquete de revisión

- Scope, CH-F01–10, CH-N01–08, acceptance oracles, trust boundaries, deployment/component diagrams, sequences, state machines, ERD, threat model, evaluation and rollout: [English](integrations/channels.en.md) / [Español](integrations/channels.es.md).
- Versioned [neutral schema](contracts/channels-v0.11.schema.json) and [webhook OpenAPI](contracts/channels-v0.11.openapi.json).
- [ADR-024](adr/024-demo-messaging-channels.md), Accepted.

## Decisions that must precede handlers

1. C# dedicated loopback host; no public tunnel to the existing product or identity provider.
2. Telegram private long polling and Meta test-number signed webhooks; official APIs only.
3. Configured tenant/endpoint and allowlisted recipient; no role or MemberRef from metadata or model text.
4. Public guest knowledge and mock human queue only. Source reads/writes have no capability in this slice.
5. Independent channel storage; persistent dedupe, checkpoint, ownership epoch and outbox state before network calls.
6. Provider outbound uncertainty is Unknown, not automatic exactly-once delivery or blind retry.
7. Local-only AI, no paid broadcast/templates; account test mode and quotas gate real sending.
8. Tests use signed fixtures, controlled clocks and fake HTTP. Preserve all old source state and inference reports.

## Definition of Ready by slice

CH01 and the mock/local portions of CH02–04 are Ready: contracts, failure policy, authority and deterministic oracles are specified; fake providers are the first implementation dependency. Live CH02 needs bot identity/token/allowlist; live CH03 needs Meta resources/secrets/API pin/tunnel setup. CH05 member linking is **Not Ready and excluded**, needing another freeze with dual identity proof and authenticated confirmation.

## Rollback and release evidence

Stop the new host/tunnel to return to the existing web presentation. Do not reset web DBs, passwords or providers. New progress reports label MockValidated or ProviderValidated per behavior. Architecture completeness is not live release completion. Freeze reopening is required for new private channel capabilities, topology changes or weaker uncertainty/security rules.

## Revisión en español

El diseño queda cerrado para implementar contratos, fixtures y preguntas públicas en un host nuevo. No se autoriza tomar la identidad del remitente como miembro, modificar las bases anteriores ni exponer el producto web. Credenciales, configuración gratuita de cuenta, limpieza y evidencia real siguen siendo condiciones de activación; no hace falta pedir otra aprobación para implementar el slice local ya solicitado.
