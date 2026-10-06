# WhatsApp activation / Activación de WhatsApp — 2026-10-05

[Privacy notice / Aviso de privacidad](../../PRIVACY.md) · [Channel design](../integrations/channels.en.md) · [Diseño de canales](../integrations/channels.es.md)

## Observed / Comprobado

- The owner created Meta test resources, registered the HTTPS callback, subscribed `messages` at webhook version v26.0, and reported publishing the app after entering privacy/deletion URLs. / El dueño preparó los recursos de prueba, registró el callback, activó `messages` en v26.0 y confirmó la publicación tras guardar los enlaces de privacidad y eliminación.
- Graph accepted the configured token for the test phone; read-only checks confirmed messaging/management permissions and the demo app's subscription to the test account. / Graph aceptó el token y confirmó los permisos de mensajería/gestión y la vinculación de la app a la cuenta de prueba.
- The local host and public tunnel returned HTTP 200. Two owner-sent questions were accepted and processed by the isolated inbox. / El host y el túnel respondieron con HTTP 200. Dos preguntas enviadas por el dueño fueron aceptadas y procesadas.
- The generated reply remained `Failed`, with one attempt and no provider message ID. Delivery is **not** validated. The token was still valid during diagnosis; restarting the PC alone did not explain this observed send failure. / La respuesta quedó en `Failed`, con un intento y sin ID del proveedor. La entrega **no** está validada. El token seguía vigente; el reinicio del PC por sí solo no explica este fallo de envío.
- The second event identified `HTTP 403 / 131005`. Read-only token inspection confirmed the expected app, both WhatsApp scopes, and granular access to the expected test account. These checks do not prove send eligibility. / El segundo evento identificó `HTTP 403 / 131005`. La inspección de solo lectura confirmó la app correcta, ambos permisos y acceso granular a la cuenta de prueba. Esto no demuestra que Meta permita enviar mensajes.

## Provider account gate / Pendiente de la cuenta del proveedor

[Meta error reference](https://developers.facebook.com/documentation/business-messaging/whatsapp/support/error-codes/) describes 131005 as a permission rejection. [Health Status](https://developers.facebook.com/documentation/business-messaging/whatsapp/support/health-status/) returned overall `BLOCKED`, with the app and phone messaging status `AVAILABLE`, and the account/business `BLOCKED`. The business profile lacks legal name, country and website; the owner dashboard independently showed empty business details. Account health also reports missing verification and a payment-method warning specifically for business-initiated conversations. Calling/SIP warnings are outside this text-only slice.

La [referencia de Meta](https://developers.facebook.com/documentation/business-messaging/whatsapp/support/error-codes/) clasifica 131005 como rechazo de permisos. [Health Status](https://developers.facebook.com/documentation/business-messaging/whatsapp/support/health-status/) devolvió `BLOCKED` para cuenta/negocio y `AVAILABLE` para mensajería de app/número. El perfil empresarial carece de nombre legal, país y web; el panel también mostró los datos vacíos. Meta informa además de verificación pendiente y una advertencia de pago para conversaciones iniciadas por el negocio. Los avisos de llamadas/SIP no pertenecen a este alcance.

The developer dashboard still labels the configured number as a **test number with free messages for 90 days**. No payment method, production number, verification submission or identity/business details were added. Health findings identify provider prerequisites to investigate; they do not establish that billing or full business verification is required for every test-number reply, or prove which prerequisite caused 131005. Do not claim successful delivery or reset the two failed outputs.

El panel sigue identificando el número como **de prueba con mensajes gratuitos durante 90 días**. No se añadió método de pago, número productivo, solicitud de verificación ni datos de identidad/negocio. El diagnóstico orienta la revisión de requisitos de Meta; no demuestra que haya que pagar o verificar una empresa para toda respuesta de prueba, ni cuál requisito produjo 131005. Los dos intentos fallidos se conservan.

The [current Meta getting-started guide](https://developers.facebook.com/documentation/business-messaging/whatsapp/get-started) uses a system-user token for its non-template reply example after the initial temporary-token template test. The configured token is type `USER`; the portfolio has no system users. The next bounded setup step is an Employee system user for the existing demo app/test WhatsApp account, with limited token duration where offered, followed by a new real inbound question. This is an official-guide alignment, **not yet a proven fix** for 131005. Meta displayed a policy-acceptance notice before system-user creation; acceptance, asset grants and credential creation remain subject to owner approval. No system user or new token was created.

La [guía actual de Meta](https://developers.facebook.com/documentation/business-messaging/whatsapp/get-started) utiliza un token de usuario de sistema para el ejemplo de respuesta de texto, después de la prueba inicial de plantilla con token temporal. El token configurado es `USER` y el portafolio carece de usuarios de sistema. El siguiente paso acotado es un usuario técnico Employee para la app/cuenta de prueba existentes, con duración limitada del token cuando Meta lo permita, y un mensaje entrante nuevo. Esto alinea la configuración con la guía; **todavía no demuestra que resuelva** 131005. Meta mostró un aviso de aceptación de políticas antes de crearlo. La aceptación, los permisos y la creación de claves requieren aprobación del dueño. No se creó ningún usuario de sistema ni token nuevo.

## Diagnostic change / Cambio de diagnóstico

Before changing code, CH-N06 and ADR-024 were clarified to allow only HTTP status and integer Meta error/subcode in a definitive rejection diagnostic. Provider messages, details, trace IDs, credentials and participant information remain excluded. Parsing or logging failures preserve `Failed`; ambiguous failures still become `Unknown`. The original failed output is kept and is not automatically resent.

Antes del código se aclararon CH-N06 y ADR-024. El diagnóstico solo puede registrar estado HTTP y códigos/subcódigos numéricos de Meta. No registra mensajes de error completos, claves ni datos personales. Si falla el diagnóstico, se conserva `Failed`; los resultados inciertos siguen en `Unknown`. El intento original se conserva y no se reenvía automáticamente.

The Release build passed without warnings/errors. The channel suite passed **83 checks**, including eight additional cases for numeric-only output, malformed/string/oversized/unreadable diagnostic bodies, a failing diagnostic sink, preserved ambiguity and no automatic resend. [New fixture report](channels-activation-checks.json). These use synthetic data and fake HTTP. The historical 75-channel/324-total reports and model measurements are preserved; the other suites were not rerun for this focused change.

El build Release pasó sin errores ni advertencias. Pasaron **83 pruebas de canales**, incluidas ocho nuevas de privacidad y conservación de estados ante fallos del diagnóstico. Son pruebas con datos ficticios y HTTP simulado. Se preservan los informes históricos de 75/324 pruebas y las mediciones de modelos; no se repitieron las demás suites para este cambio acotado.

## Still pending / Pendiente

Resolve the provider account gate with owner-supplied information and verify real ES/EN replies and delivery receipts. The messaging host is explicitly `simulated`; local-model activation and Telegram remain separate gates. No paid templates or cloud inference have been enabled.

Falta resolver el pendiente de la cuenta con información del dueño y comprobar respuestas ES/EN y recibos reales. El host está expresamente en `simulated`; IA local y Telegram conservan sus pendientes separados. No se activaron plantillas de pago ni inferencia cloud.

## After a PC restart / Después de reiniciar el PC

The messaging host and tunnel must be started again. A new Quick Tunnel normally changes the public callback, which must then be updated in Meta. The saved configuration and channel database are retained. Docker is used by separate web-demo dependencies; it does not start this C# messaging host or its tunnel. Running without Docker is expected in `simulated` messaging mode.

Hay que arrancar de nuevo el host y el túnel. Un Quick Tunnel nuevo normalmente cambia el callback público y hay que actualizarlo en Meta. Se conservan la configuración y la base. Docker corresponde a dependencias de la demo web: no inicia este host C# ni su túnel. El canal en modo `simulated` puede funcionar sin Docker.
