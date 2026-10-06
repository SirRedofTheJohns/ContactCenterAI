# WhatsApp activation / Activación de WhatsApp — 2026-10-05

[Privacy notice / Aviso de privacidad](../../PRIVACY.md) · [Channel design](../integrations/channels.en.md) · [Diseño de canales](../integrations/channels.es.md)

## Current result / Resultado actual

**A real Spanish cancellation-policy question received a reply through Meta's test number. The local ledger reached `Read` from a signed provider receipt.** This validates the inbound webhook, public-policy handler, outbound API and receipt correlation for this test. Outbound Graph uses v25.0 and `messages` webhooks use v26.0; the receipt is evidence for this configured pair, not other versions. The successful output has one attempt and a provider ID. Both previous failed outputs remain unchanged.

**Una pregunta real sobre la política de cancelación recibió respuesta por el número de prueba de Meta. La base local llegó a `Read` mediante un recibo firmado del proveedor.** Esto comprueba recepción, consulta de políticas públicas, envío y asociación del recibo para esta prueba. Graph usa v25.0 y los webhooks `messages` usan v26.0. La respuesta exitosa tiene un intento e ID del proveedor. Los dos intentos fallidos anteriores se conservan.

After `/en`, the English cancellation-policy question also received the exact approved English text with a citation and reached `Read`. At the final snapshot, six incoming events were `Completed`; four outgoing replies were `Read` (including the language-command acknowledgement), and the original two were `Failed`. Every output had one attempt; only the successful four had provider IDs. [Aggregate evidence](whatsapp-live-evidence-2026-10-05.json) contains no participant identifiers or message bodies.

Después de `/en`, la pregunta en inglés también recibió el texto aprobado en inglés con referencia y llegó a `Read`. Al cierre había seis eventos entrantes `Completed`, cuatro respuestas `Read` (incluida la confirmación del cambio de idioma) y los dos intentos originales `Failed`. Cada salida tuvo un intento; solo las cuatro exitosas tenían ID del proveedor. La [evidencia agregada](whatsapp-live-evidence-2026-10-05.json) no incluye identificadores de participantes ni textos de mensajes.

The host remains explicitly `simulated`, using approved local knowledge without paid model tokens. Telegram is disabled. This result does not establish production readiness or model quality. No payment method, production number or business-verification submission was added.

El host sigue en modo `simulated`: responde desde conocimiento local aprobado, sin tokens de un modelo de pago. Telegram está apagado. Este resultado no demuestra preparación para producción ni calidad de un modelo. No se añadió método de pago, número productivo ni solicitud de verificación empresarial.

## Initial diagnosis, before the fix / Diagnóstico inicial, antes de la corrección

- The owner created Meta test resources, registered the HTTPS callback, subscribed `messages` at webhook version v26.0, and reported publishing the app after entering privacy/deletion URLs. / El dueño preparó los recursos de prueba, registró el callback, activó `messages` en v26.0 y confirmó la publicación tras guardar los enlaces de privacidad y eliminación.
- Graph accepted the configured token for the test phone; read-only checks confirmed messaging/management permissions and the demo app's subscription to the test account. / Graph aceptó el token y confirmó los permisos de mensajería/gestión y la vinculación de la app a la cuenta de prueba.
- The local host and public tunnel returned HTTP 200. Two owner-sent questions were accepted and processed by the isolated inbox. / El host y el túnel respondieron con HTTP 200. Dos preguntas enviadas por el dueño fueron aceptadas y procesadas.
- The generated reply remained `Failed`, with one attempt and no provider message ID. Delivery is **not** validated. The token was still valid during diagnosis; restarting the PC alone did not explain this observed send failure. / La respuesta quedó en `Failed`, con un intento y sin ID del proveedor. La entrega **no** está validada. El token seguía vigente; el reinicio del PC por sí solo no explica este fallo de envío.
- The second event identified `HTTP 403 / 131005`. Read-only token inspection confirmed the expected app, both WhatsApp scopes, and granular access to the expected test account. These checks do not prove send eligibility. / El segundo evento identificó `HTTP 403 / 131005`. La inspección de solo lectura confirmó la app correcta, ambos permisos y acceso granular a la cuenta de prueba. Esto no demuestra que Meta permita enviar mensajes.

## Account diagnosis and configuration fix / Diagnóstico de cuenta y corrección

[Meta error reference](https://developers.facebook.com/documentation/business-messaging/whatsapp/support/error-codes/) describes 131005 as a permission rejection. [Health Status](https://developers.facebook.com/documentation/business-messaging/whatsapp/support/health-status/) returned overall `BLOCKED`, with the app and phone messaging status `AVAILABLE`, and the account/business `BLOCKED`. The business profile lacks legal name, country and website; the owner dashboard independently showed empty business details. Account health also reports missing verification and a payment-method warning specifically for business-initiated conversations. Calling/SIP warnings are outside this text-only slice.

La [referencia de Meta](https://developers.facebook.com/documentation/business-messaging/whatsapp/support/error-codes/) clasifica 131005 como rechazo de permisos. [Health Status](https://developers.facebook.com/documentation/business-messaging/whatsapp/support/health-status/) devolvió `BLOCKED` para cuenta/negocio y `AVAILABLE` para mensajería de app/número. El perfil empresarial carece de nombre legal, país y web; el panel también mostró los datos vacíos. Meta informa además de verificación pendiente y una advertencia de pago para conversaciones iniciadas por el negocio. Los avisos de llamadas/SIP no pertenecen a este alcance.

The developer dashboard labels the configured number as a **test number with free messages for 90 days**. No payment method, production number, verification submission or identity/business details were added. Health findings did not establish that billing or full business verification was required for this test reply. Overall health remained `BLOCKED`, yet the new reply reached `Read`. Do not invent legal business details to silence unrelated warnings, or reset the two failed outputs.

El panel identifica el número como **de prueba con mensajes gratuitos durante 90 días**. No se añadió método de pago, número productivo, solicitud de verificación ni datos de identidad/negocio. El estado global seguía en `BLOCKED`, pero la nueva respuesta llegó a `Read`. Por tanto, ese estado no demostraba que esta respuesta necesitara pago o verificación empresarial completa. No hay que inventar datos legales para eliminar avisos ajenos a la prueba. Los dos intentos fallidos se conservan.

The [current Meta getting-started guide](https://developers.facebook.com/documentation/business-messaging/whatsapp/get-started) uses a system-user token for its non-template reply example after the temporary-token template test. With explicit owner approval, an Employee system user was created and a 60-day `SYSTEM_USER` token generated, with `whatsapp_business_messaging` and `whatsapp_business_management`. Asset access covers only the demo app and test WhatsApp account: **Manage app / Full access** for the app, **Messages** and the automatically selected **Phone numbers (view only)** for WhatsApp. Manage app can change app configuration and roles, so that permission level was separately approved. WhatsApp **Everything** was not granted.

La [guía actual de Meta](https://developers.facebook.com/documentation/business-messaging/whatsapp/get-started) utiliza un token de usuario de sistema para responder texto tras la prueba inicial con token temporal. Con autorización explícita se creó un usuario técnico Employee y un token `SYSTEM_USER` de 60 días, con `whatsapp_business_messaging` y `whatsapp_business_management`. Solo accede a la app de demo y a la cuenta de prueba: **Manage app / Full access** en la app, **Messages** y **Phone numbers (view only)** en WhatsApp. Manage app puede cambiar configuración y roles; se aprobó expresamente ese nivel. No se otorgó **Everything** en WhatsApp.

The token was saved only in ignored, access-restricted local configuration. After restarting the channel host, a new owner question produced a reply and `Read` receipt. This configuration resolved the observed test failure; the test does not isolate token-type effects from asset-grant effects or guarantee identical behavior for other accounts. Credentials, participant identifiers and raw provider payloads are excluded from this report.

El token se guardó solo en la configuración local excluida de GitHub y con acceso restringido. Tras reiniciar el host, una pregunta nueva produjo respuesta y recibo `Read`. La configuración resolvió el fallo observado, aunque esta prueba no separa el efecto del tipo de token del efecto de los permisos sobre recursos. No se publican claves, identificadores de participantes ni payloads del proveedor.

## Diagnostic change / Cambio de diagnóstico

Before changing code, CH-N06 and ADR-024 were clarified to allow only HTTP status and integer Meta error/subcode in a definitive rejection diagnostic. Provider messages, details, trace IDs, credentials and participant information remain excluded. Parsing or logging failures preserve `Failed`; ambiguous failures still become `Unknown`. The original failed output is kept and is not automatically resent.

Antes del código se aclararon CH-N06 y ADR-024. El diagnóstico solo puede registrar estado HTTP y códigos/subcódigos numéricos de Meta. No registra mensajes de error completos, claves ni datos personales. Si falla el diagnóstico, se conserva `Failed`; los resultados inciertos siguen en `Unknown`. El intento original se conserva y no se reenvía automáticamente.

The Release build passed without warnings/errors. The channel suite passed **83 checks**, including eight additional cases for numeric-only output, malformed/string/oversized/unreadable diagnostic bodies, a failing diagnostic sink, preserved ambiguity and no automatic resend. [New fixture report](channels-activation-checks.json). These use synthetic data and fake HTTP. The historical 75-channel/324-total reports and model measurements are preserved; the other suites were not rerun for this focused change.

El build Release pasó sin errores ni advertencias. Pasaron **83 pruebas de canales**, incluidas ocho nuevas de privacidad y conservación de estados ante fallos del diagnóstico. Son pruebas con datos ficticios y HTTP simulado. Se preservan los informes históricos de 75/324 pruebas y las mediciones de modelos; no se repitieron las demás suites para este cambio acotado.

## Still pending / Pendiente

Spanish and English reply/receipt acceptance passed. Send `/en` before English questions and `/es` to return to Spanish. Restarting this host preserved the two failed outputs and allowed a fresh successful reply; a full PC/tunnel restart recovery drill is still pending. Expired-token handling and simulated handoff have fixture coverage but are not recorded here as live acceptance. Local-model activation and Telegram remain separate gates. No paid templates or cloud inference have been enabled. This credential/documentation update did not change productive code or rerun unrelated suites.

Pasaron las respuestas y recibos en español e inglés. `/en` cambia a inglés y `/es` vuelve a español. Reiniciar este host conservó los dos intentos fallidos y permitió responder una pregunta nueva; queda pendiente el ensayo completo de reinicio del PC/túnel. Credencial vencida y transferencia humana simulada tienen pruebas automatizadas, pero este informe no las presenta como aceptación en vivo. IA local y Telegram conservan pendientes separados. No se activaron plantillas de pago ni inferencia cloud. Esta actualización no cambió código productivo ni repitió suites ajenas.

## After a PC restart / Después de reiniciar el PC

The messaging host and tunnel must be started again. A new Quick Tunnel normally changes the public callback, which must then be updated in Meta. Configuration and the database are retained. Health uses `/health/live`. Docker serves separate web-demo dependencies; it does not start this C# host or tunnel. Running without Docker is expected in `simulated` messaging mode.

Hay que arrancar de nuevo el host y el túnel. Un Quick Tunnel nuevo normalmente cambia el callback y hay que actualizarlo en Meta. Se conservan configuración y base. La disponibilidad se comprueba en `/health/live`. Docker corresponde a dependencias de la demo web: no inicia este host C# ni el túnel. El canal en modo `simulated` puede funcionar sin Docker.
