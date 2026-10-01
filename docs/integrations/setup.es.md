# Conectar Telegram y WhatsApp desde cero

[English](setup.en.md) · [Diseño](channels.es.md) · [Estado y pruebas](../progress/channels-v0.11.md)

Empieza por Telegram. Solo necesitas tu cuenta de Telegram, crear un bot y dejar este PC encendido. WhatsApp tiene más pasos porque Meta debe entregar un número de prueba y recibir mensajes en una dirección HTTPS.

Los adapters están implementados y probados con proveedores simulados. **Todavía no hay cuentas reales conectadas.** Crear la cuenta, aceptar términos y verificar tu teléfono corresponde al dueño. No hay que cambiar las contraseñas ni repetir los logins de la demo web.

## Preparar el archivo local

En la raíz de ContactCenterAI ejecuta una vez:

```powershell
./eng/Start-Channels.cmd -PrepareOnly
```

Esto crea `deploy/channels/.env` con ambos canales apagados, genera una clave de verificación de webhook y restringe el acceso local al archivo y a `.local/channels`. El archivo está excluido de GitHub. Ábrelo con tu editor para colocar los datos; no pegues tokens en el chat, capturas, enlaces ni commits. Usa valores literales sin comillas y sin comentarios al final de la línea. El `.env` de la demo anterior es otro archivo.

## Telegram: seis pasos

1. Abre el [BotFather oficial](https://t.me/BotFather) en Telegram y escribe `/newbot`. Elige un nombre y un usuario que termine en `bot`, por ejemplo `contactcenter_juan_demo_bot`, si está disponible. BotFather te entrega el token. [Referencia oficial](https://core.telegram.org/bots/features#creating-a-new-bot).
2. Copia ese token **solo** en `CCAI_CHANNEL_TELEGRAM_ACCESS_TOKEN`. Coloca el número que aparece antes de `:` en `CCAI_CHANNEL_TELEGRAM_ENDPOINT_ID`; el launcher comprobará que corresponde al bot mediante `getMe`.
3. Abre el enlace a tu nuevo bot que muestra BotFather y envíale `/start` en un chat privado. Por ahora no responderá: el host no está activado.
4. Ejecuta `./eng/Start-Channels.cmd -InspectTelegram`. Muestra los IDs de chats privados recibidos, sin mensajes ni tokens. No envía nada ni avanza el checkpoint. Copia **tu propio ID** en `CCAI_CHANNEL_TELEGRAM_RECIPIENTS`. Si aparecen varios, no los autorices todos por defecto. Esta inspección requiere que el host esté detenido.
5. Cambia `CCAI_CHANNEL_TELEGRAM_ENABLED=false` a `true`. Deja WhatsApp apagado. Ejecuta `./eng/Start-Channels.cmd` y mantén abierta esa ventana. El bot consulta mensajes por HTTPS; no necesita hosting público ni abrir puertos de tu router. [Bot API](https://core.telegram.org/bots/api).
6. Prueba `/es`, `¿Cuál es la política de cancelación?`, `/en` y `What is the cancellation policy?`. La respuesta debe incluir una cita. Prueba `/human` al final: se registra una cola simulada y el bot queda en pausa para ese chat. Reiniciar no elimina esa pausa ni los mensajes.

Puedes autorizar otro participante de demo agregando su ID, separado por coma, tras una inspección deliberada. El bot no responde a grupos ni a personas fuera de la lista.

El modo inicial `CCAI_CHANNEL_AI_MODE=simulated` demuestra el flujo sin modelos. Para usar la IA local ya preparada, inicia los dos modelos con los launchers existentes y cambia ese valor a `local-llm`. Se conserva Qwen/BGE-M3 con sus pins, se prepara un índice en la nueva base y no se descarga nada ni se usa una API de pago. La elección de canales no cambia el perfil de la demo web.

## WhatsApp: recursos de prueba de Meta

El [Developer Hub oficial](https://whatsappbusiness.com/developers/developer-hub/) ofrece acceso a números y recursos de prueba. La disponibilidad y los nombres del panel dependen de tu cuenta; este proyecto no promete que una línea de producción sea gratis.

1. Entra a [Meta for Developers](https://developers.facebook.com/apps/), inicia sesión y crea una app con el caso de uso/producto WhatsApp. Completa tú los términos y verificaciones que solicite la plataforma.
2. En la configuración de API/pruebas de WhatsApp, selecciona el **número de prueba que proporciona Meta**. Añade tu teléfono como destinatario de prueba y completa su verificación. Usa el formato internacional de dígitos, sin `+`, espacios ni guiones. No migres tu WhatsApp personal ni contrates un BSP para esta demo.
3. Copia en el `.env` estos valores del panel:

| Campo local | Qué colocar |
|---|---|
| `CCAI_CHANNEL_META_ENDPOINT_ID` | Phone Number ID del número de prueba, no el número visible ni el WABA ID |
| `CCAI_CHANNEL_META_ACCESS_TOKEN` | Token de acceso autorizado para ese recurso; el temporal puede vencer |
| `CCAI_CHANNEL_META_APP_SECRET` | App Secret de la app, desde su configuración básica |
| `CCAI_CHANNEL_META_API_VERSION` | Versión Graph que muestra el ejemplo del panel, con formato `vNN.0` |
| `CCAI_CHANNEL_META_RECIPIENTS` | Tu teléfono de prueba ya verificado, solo dígitos |
| `CCAI_CHANNEL_META_VERIFY_TOKEN` | Conservar el valor aleatorio que generó la preparación; se usará al registrar el webhook |

4. Comprueba en tu panel que se trata de recursos gratuitos de prueba. Solo entonces cambia `CCAI_CHANNEL_META_TEST_RESOURCES_CONFIRMED` y `CCAI_CHANNEL_META_ENABLED` a `true`. El código limita destinatarios, no incluye templates/campañas y no activa pagos. Esa bandera es una confirmación del dueño, no una consulta automática a la facturación de Meta.
5. Inicia `./eng/Start-Channels.cmd`. El host nuevo escucha únicamente en `127.0.0.1:7454`. Para recibir webhooks, necesitas un túnel HTTPS hacia ese puerto. Instala `cloudflared` siguiendo las [instrucciones oficiales](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/) y abre, en otra ventana:

```powershell
cloudflared tunnel --url http://127.0.0.1:7454
```

6. Copia el hostname HTTPS que entregue el túnel y registra en Meta el callback `https://HOST-DEL-TUNEL/webhooks/whatsapp` con tu `META_VERIFY_TOKEN`. Suscribe el campo `messages` y la app al WABA de prueba conforme a las instrucciones de tu panel. La comprobación GET debe devolver el challenge; los POST usan una firma diferente con el App Secret. [Webhooks de Meta](https://whatsapp.github.io/WhatsApp-Nodejs-SDK/api-reference/webhooks/start/).
7. Desde tu teléfono de prueba, escribe al número de prueba de Meta; si el panel requiere iniciar el contacto, sigue su procedimiento de prueba. Luego prueba las preguntas ES/EN anteriores. El host solo responde dentro de las 24 horas desde un mensaje elegible recibido y no genera templates para abrir otra ventana. El túnel es temporal: al cambiar de dirección, actualiza el callback. [Quick Tunnels](https://developers.cloudflare.com/tunnel/get-started/quick-tunnels/).

Publica **solo el puerto 7454**. La demo web 7452, reservas 7453, Keycloak 8080 y modelos 11434/11435 quedan locales. No hace falta que Docker publique más puertos para Telegram o para el host de canales.

## Leer el estado y detener

`Ctrl+C` detiene el host; detén también el túnel. Después, `./eng/Start-Channels.cmd -Status` muestra conteos de inbox, outbox y ownership. `Sent` significa respuesta aceptada con ID del proveedor; `Unknown` significa resultado incierto y no se reenvía automáticamente. El estado puede leerse con el host detenido porque hay un lock exclusivo por base.

Los textos y rutas se limpian lógicamente tras siete días, con ejecución al iniciar y cada hora; las claves de deduplicación duran treinta días. Esto no cifra SQLite ni asegura borrado físico de WAL/copias. La base de canales es independiente; no borres las bases de la demo anterior para arreglar un canal.

## Si algo falla

| Mensaje/síntoma | Acción concreta |
|---|---|
| `CHANNEL_CONFIG_REQUIRED` | Revisar token, ID numérico y lista de destinatarios en el archivo local |
| `TELEGRAM_READINESS_FAILED` | Revisar token/bot ID y conexión; comprobar que el bot no tenga un webhook configurado; el código no lo elimina |
| Inspección sin IDs | Enviar texto privado a tu propio bot y volver a inspeccionar una vez |
| `META_TEST_CONFIG_REQUIRED` | Falta confirmación de recursos test, versión o secretos; mantener WhatsApp apagado hasta completar el panel |
| WhatsApp recibe pero no contesta | Revisar destinatario autorizado, token vigente, ventana de 24 horas y conteos locales |
| `CHANNEL_START_OR_STORAGE_FAILED` | Comprobar que no haya otro host/inspección activo y que el usuario tenga acceso a los archivos |
| `LOCAL_MODEL_PIN_MISMATCH` / error al preparar índice | Completar el arranque de los modelos fijados o seleccionar `simulated` explícitamente |
| `/human` deja el bot callado | Es el comportamiento previsto: ownership humano persistente. El reinicio no reasigna al bot; el reset autenticado queda para otro slice |

Para declarar cada canal conectado faltan mensajes y respuestas reales ES/EN, reinicio, fallo por credencial vencida y revisión de costos/configuración. Las pruebas automáticas usan fixtures y no envían mensajes a teléfonos.
