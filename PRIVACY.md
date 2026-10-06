# ContactCenterAI Demo — Privacy notice / Aviso de privacidad

Updated / Actualizado: 2026-10-05

[English](#english) · [Español](#español)

## English

### Who runs this demo

ContactCenterAI Demo is a personal software portfolio project maintained by [SirRedofTheJohns](https://github.com/SirRedofTheJohns). It is not operated by GBS, Meta, Telegram or Genesys. Messaging access is limited to participants invited by the maintainer and explicitly allowed in the local configuration.

The bot answers public questions about fictional services in English and Spanish. A request for a human pauses the bot and records a simulated handoff; it does not connect a real contact-center employee. Messaging does not give access to a member account or private reservations.

### Data used

For an allowed conversation, the demo processes your WhatsApp phone identifier or Telegram sender/chat identifier, message text, language preference, timestamps, message identifiers and delivery status. It stores the information needed to route replies, prevent duplicate work, manage a paused conversation and diagnose delivery problems. The phone identifier remains personal data even when some event keys are hashed.

The application sanitizes supported sensitive text patterns before storing accepted messages. This is not a guarantee that every piece of personal information will be removed. Use fictional examples. Do not send passwords, payment details, identity documents, health information or real customer records.

### Processing and providers

Accepted messages and generated replies are stored in a separate database on the maintainer's computer, with restricted operating-system access. The database is not encrypted by this application.

Replies can use an explicitly selected simulator or local AI models. In local AI mode, the message is processed on that computer; there is no automatic fallback to a paid cloud model. Demo conversations are not used by this project to train models, sold, used for advertising or deliberately published in the GitHub repository.

WhatsApp messages pass through Meta's services. The temporary HTTPS webhook tunnel uses Cloudflare, which carries incoming webhook traffic and may process network metadata. Telegram uses its own messaging services when that adapter is enabled. These providers have their own privacy policies and retention practices: [WhatsApp](https://www.whatsapp.com/legal/privacy-policy), [Cloudflare](https://www.cloudflare.com/privacypolicy/) and [Telegram](https://telegram.org/privacy). This notice does not control their copies of messages or metadata.

### Retention and deletion

The running host performs cleanup at startup and approximately every hour. It clears stored input text and removes related reply records older than seven days, removes conversation routing records after seven days without activity, and keeps deduplication records for up to thirty days. Cleanup waits until the host runs again if the computer or service is stopped. Logical deletion does not guarantee physical erasure from SQLite journals, backups or provider systems.

For a privacy question or a request to stop processing or delete local demo data, contact the person who invited you through the same private contact channel. Identify the demo conversation privately; the maintainer can ask you to demonstrate control of the sending account before handling its records. Do not put your phone number or messages in public GitHub issues. Stopping participation does not automatically erase earlier records. A request is handled by the maintainer; the bot does not provide an automated deletion command or a guaranteed response time.

## Español

### Quién mantiene la demo

ContactCenterAI Demo es un proyecto personal de portafolio mantenido por [SirRedofTheJohns](https://github.com/SirRedofTheJohns). No es un servicio de GBS, Meta, Telegram ni Genesys. Los canales solo admiten participantes invitados por el responsable y autorizados expresamente en la configuración local.

El bot responde preguntas públicas sobre servicios ficticios en español e inglés. Pedir un humano pausa el bot y registra una transferencia simulada; no conecta con un empleado real. Estos canales no dan acceso a cuentas de miembros ni a reservas privadas.

### Datos utilizados

En una conversación autorizada se procesan el identificador telefónico de WhatsApp o el identificador de remitente/chat de Telegram, el texto, el idioma, las fechas, los identificadores de mensajes y su estado de entrega. Se guarda lo necesario para responder, evitar duplicados, conservar una pausa y revisar problemas de entrega. El identificador telefónico sigue siendo un dato personal aunque algunas claves de eventos se guarden como hashes.

La aplicación limpia ciertos patrones de datos sensibles antes de guardar mensajes aceptados. Esto no garantiza detectar toda la información personal. Usa ejemplos ficticios. No envíes contraseñas, datos de pago, documentos de identidad, información médica ni registros reales de clientes.

### Procesamiento y proveedores

Los mensajes aceptados y las respuestas se guardan en una base separada en el equipo del responsable, con acceso restringido por el sistema operativo. La aplicación no cifra esa base.

Las respuestas usan un simulador o modelos locales, según el modo elegido expresamente. Con IA local, el texto se procesa en ese equipo; no hay un cambio automático a un modelo cloud de pago. Este proyecto no usa las conversaciones para entrenar modelos, vender datos, hacer publicidad ni publicarlas deliberadamente en GitHub.

WhatsApp transporta los mensajes mediante Meta. El túnel HTTPS temporal usa Cloudflare, que transporta los webhooks entrantes y puede procesar metadatos de conexión. Telegram usa sus propios servicios cuando se activa ese adapter. Cada proveedor aplica sus propias políticas y plazos: [WhatsApp](https://www.whatsapp.com/legal/privacy-policy), [Cloudflare](https://www.cloudflare.com/privacypolicy/) y [Telegram](https://telegram.org/privacy). Este aviso no controla las copias conservadas por ellos.

### Conservación y solicitudes de eliminación

El host limpia los registros al arrancar y aproximadamente cada hora mientras está funcionando. Vacía el texto de entrada y elimina las respuestas relacionadas de más de siete días, elimina rutas de conversación tras siete días sin actividad y conserva registros de deduplicación hasta treinta días. Si el servicio está detenido, la limpieza espera al siguiente arranque. La eliminación lógica no garantiza borrar físicamente los datos de journals de SQLite, copias de seguridad ni sistemas del proveedor.

Para consultar sobre privacidad, dejar de participar o solicitar eliminar datos locales, contacta a la persona que te invitó por el mismo medio privado. Identifica la conversación en privado; el responsable puede pedir una prueba de control de la cuenta remitente antes de gestionar sus registros. No publiques tu teléfono ni tus mensajes en issues de GitHub. Dejar de participar no borra automáticamente los registros anteriores. El responsable atiende las solicitudes: el bot no incluye un comando automático de eliminación ni garantiza un plazo de respuesta.
