# Communication recovery / Recuperación de comunicación — 2026-10-06

The owner reported a PC restart. Local probes found the channel host, web/source/identity and model endpoints unavailable. Only the existing WhatsApp FAQ host and its dedicated temporary tunnel were restored for this task. The channel remains in `simulated` mode; Telegram is disabled. Web, Docker identity/source dependencies and models were not started as a prerequisite for the FAQ channel.

El dueño informó de un reinicio del PC. El host de canales y los servicios web/fuente/identidad/modelos no estaban disponibles. Se restauraron el host de preguntas públicas de WhatsApp y su túnel dedicado. El canal sigue en `simulated`; Telegram está apagado. Web, dependencias Docker y modelos no se arrancaron como requisito del FAQ.

- Loopback channel `/health/live`: HTTP 200 after start. / Host local disponible tras arrancar.
- The new public tunnel `/health/live`: HTTP 200. / Nuevo túnel disponible.
- Updated the Meta callback using the existing verification nonce; after a dashboard reload, the new URL persisted and `messages` remained subscribed at v26.0. / Meta guardó el callback nuevo y conservó `messages` en v26.0, comprobado tras recargar.
- Preserved inbox/outbox and credentials. Current stored route was already `BotOwned`; no ownership reset, history deletion or replay was performed. / Se conservan base y claves. El chat ya estaba `BotOwned`; no se cambió ownership ni se borraron/reenviaron mensajes.
- **No fresh owner-sent WhatsApp reply was observed in this recovery task.** Earlier Spanish/English `Read` results remain historical evidence. / **No se observó una respuesta nueva de WhatsApp en esta recuperación.** Los recibos anteriores ES/EN se conservan como evidencia histórica.

The [resort v0.12 packet](../resort-booking-v0.12.en.md) / [propuesta ES](../resort-booking-v0.12.es.md) is a separate open design. Calendar, prices, new bookings and private channel writes are not implemented by this recovery. No new provider permissions or payment method were added; only the existing callback target changed.

El calendario, catálogo de precios y operaciones privadas del resort son un diseño nuevo abierto. Restaurar la comunicación no implementa esa ampliación. No se añadieron permisos del proveedor ni pagos; solo cambió el destino del callback existente.
