# ADR-024 — Official messaging channels with local AI

Status: Accepted for the local/public slice, 2026-10-01. [English design](../integrations/channels.en.md) / [Diseño español](../integrations/channels.es.md).

## Context / Contexto

The owner wants WhatsApp and Telegram for a portfolio without additional service fees. Reusing the web demo behind a tunnel would expose login and private operations. Provider sender IDs do not establish membership, and reply delivery can remain uncertain.

El dueño solicita WhatsApp y Telegram para el portafolio sin cargos de servicios adicionales. Publicar la demo web por un túnel expondría login y operaciones privadas. El ID del remitente no prueba membresía y un envío puede quedar incierto.

## Decision / Decisión

Use official Telegram Bot API long polling and Meta Cloud API test resources. Create a separate C# ChannelHost on loopback port 7454; expose only signed Meta webhook routes through an optional temporary HTTPS tunnel. Use a separate durable channel store. Model, privacy, knowledge ACL and authority rules remain bounded. v0.11 supports private text, public FAQs, language controls and simulated handoff; no member lookup or transaction from channel identity.

Usar Bot API oficial mediante long polling y recursos de prueba Meta Cloud API. Crear ChannelHost C# separado en loopback:7454; exponer solo webhook Meta firmado mediante túnel HTTPS opcional. Persistencia de canal separada. Conservar límites de modelo, privacidad, ACL y autoridad. v0.11 cubre texto privado, preguntas públicas, idioma y handoff simulado; no consultar miembros ni ejecutar acciones por identidad de canal.

Durable inbound keys prevent duplicate work. Persist Telegram batches before increasing offsets. Commit outbox Sending before dispatch; ambiguous sends become Unknown without blind resend. No provider outbound exactly-once claim. Missing secrets disable live transports. Keep test-recipient allowlists, daily limits, no cloud fallback, no templates and no paid broadcasts.

Claves durables evitan trabajo duplicado. Guardar lote Telegram antes de avanzar offset. Guardar Sending antes de enviar; incertidumbre pasa a Unknown sin reenvío ciego. No afirmar exactly-once del proveedor. Sin claves no se activan transports. Mantener allowlists, cuotas, sin cloud, templates ni paid broadcast.

## Alternatives / Alternativas

- Telegram webhook: valid option, but polling avoids public hosting for this demo. / Webhook válido, polling evita hosting público.
- Unofficial WhatsApp Web libraries: rejected; account/session fragility and weak portfolio evidence. / Rechazadas por fragilidad y menor evidencia.
- Paid BSP/cloud LLM: excluded by requested budget. / Fuera del presupuesto solicitado.
- Tunnel the existing API: rejected; private product surface is unnecessary. / Rechazado por exponer superficie privada.
- Provider IDs as MemberRef: rejected; breaks frozen identity invariants. / Rechazado por romper identidad confiable.

## Consequences / Consecuencias

Requires provider-owned credentials and test-account setup before live evidence. Test resources have availability/terms limits; no permanent free production promise. The dedicated ledger introduces another local backup/cleanup responsibility. Member linking, channel critical actions and real Genesys transfer need later freezes. Existing web state and historical measurements are preserved.

Requiere claves y configuración del dueño antes de evidencia live. Recursos test tienen límites de disponibilidad/términos; no se promete producción gratis permanente. El ledger nuevo añade responsabilidad de copia/limpieza. Vinculación de miembros, acciones críticas y Genesys real necesitan otros freezes. Se conserva estado web y mediciones históricas.
