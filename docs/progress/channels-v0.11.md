# Messaging delivery v0.11 / Entrega de mensajería

Date / Fecha: 2026-10-01. **Local implementation complete; live activation pending owner setup.** / **Implementación local terminada; conexión real pendiente de configuración del dueño.**

[Set up from scratch](../integrations/setup.en.md) · [Prepararlo desde cero](../integrations/setup.es.md) · [English architecture](../integrations/channels.en.md) · [Arquitectura española](../integrations/channels.es.md)

## What works / Qué funciona

| Piece / Pieza | Evidence / Evidencia |
|---|---|
| Dedicated C# host | Loopback:7454, only liveness and Meta webhook; HTTP tests reject product/login paths |
| Telegram adapter | `getMe` / webhook-conflict readiness, private `getUpdates`, durable offsets, plain-text replies with paid broadcast disabled; fake HTTP |
| Meta adapter | Raw-body HMAC, exact GET challenge, fixed phone ID/recipient allowlist, bounded JSON and real HTTP ingress/rate-limit tests; fake outbound HTTP |
| Public responder | ES/EN controls, existing privacy/scope policy, guest Public-only knowledge, citation recheck, no private reservation capability |
| Handoff | Persisted ownership epoch, matching local mock acknowledgment and bot pause; no provider human-agent service |
| Reliability | Isolated inbox/outbox, collision quarantine, lease fencing, committed Sending, Unknown without blind resend, bounded 429 retries and monotonic Meta receipts |
| Setup/retention | Ignored configuration template, preparation/inspection/status CLI, exclusive process lock, tested logical seven/thirty-day cleanup and isolated launcher preparation |

La extensión reutiliza las reglas y el conocimiento de la demo, pero conserva su propia base. `/human` registra una cola simulada y deja el bot pausado; ni un reinicio ni `/start` lo reasignan. Los identificadores de proveedor sirven para routing, nunca para identificar a un miembro. No hay tools de consulta o cancelación de reservas en este host.

## Validation / Validación

The Release solution built with SDK 10.0.401 and locked dependencies: zero warnings/errors. The full verification command passed **324 checks**: the existing nine-suite 249-check baseline plus **75 channel checks**. The separate 40-case simulated evaluation also passed. [Full regression record](channels-v0.11-regression.json) · [Channel cases](channels-v0.11-checks.json).

El build Release pasó con cero warnings/errores y **324 checks**: los 249 anteriores y 75 nuevos de canales. La evaluación simulada de 40 casos también pasó. Se conservaron íntegros los informes históricos y las 300 respuestas reales de IA; esta verificación no produjo nuevas mediciones de calidad ni latencia de modelos.

The launcher preparation was checked in a separate disposable fixture: transports remained disabled, a 48-character random verification token was generated without printing it, and Windows configuration access was protected. Actual provider-account activation and the owner's OS profile still require live acceptance. Cleanup tests establish logical row removal, not encryption or physical erasure.

La preparación del launcher se comprobó en una carpeta aislada: canales apagados, token aleatorio generado sin mostrarlo y ACL de Windows protegida. El perfil real del dueño se comprobará al activar. La limpieza prueba eliminación lógica de filas; no demuestra cifrado ni borrado físico de WAL/copias.

## Publication / Publicación

Public repository: [SirRedofTheJohns/ContactCenterAI](https://github.com/SirRedofTheJohns/ContactCenterAI). English/Spanish entry guides, source, versioned contracts and reviewed synthetic screenshots are included. Credentials, databases, weights and logs are excluded. The architecture freeze was published in [commit 598b753](https://github.com/SirRedofTheJohns/ContactCenterAI/commit/598b753791540945f4f7b1e5ece82fd28ed61768) before channel implementation.

The first remote [Actions run](https://github.com/SirRedofTheJohns/ContactCenterAI/actions/runs/36920196939) could not start a runner because of a platform/account restriction. **Remote CI is not passed.** Local verification passed; no remote execution result or dependency compatibility on a GitHub runner is claimed. The workflow remains enabled for future account availability.

El repositorio ya es público y las guías principales están en ambos idiomas. La primera corrida de Actions no inició el runner por una restricción de plataforma/cuenta; CI remota sigue sin validar. Se conserva el workflow y la evidencia local. Los informes v0.10 que indicaban que aún no existía destino GitHub describen el estado anterior a esta publicación.

## Remaining live gates / Pendientes reales

- Telegram: owner-created bot/token, verified bot ID, deliberately selected participant IDs, real ES/EN request/reply pairs, restart and credential-failure behavior.
- Meta: owner account/app, test Phone Number ID, token/App Secret/API version, verified test recipients, cost-resource review, HTTPS callback and real signed messages/status receipts. Optional opaque callback correlation is fixture-validated, not provider-version validated.
- Runtime: actual local model readiness in the new host; fixture tests reused deterministic providers. The real web model evidence remains separate.
- Privacy: observe restricted storage and scheduled cleanup under the owner's profile before live activation; no production compliance claim.

Falta crear los recursos del dueño y probar mensajes reales. Esta entrega no resuelve SQL empresarial, tenant Genesys, calidad independiente, producción ni vinculación de miembros por canales. [CH05 sigue excluido](../architecture-freeze-v0.11-channels.md). Esas limitaciones no impiden mostrar la demo web ni el código y las pruebas de integración como portafolio.

## Meta setup update / Avance de Meta — 2026-10-05

The owner created the Meta app and test resources and received the dashboard sample message. The saved access token was accepted by Graph for the configured test phone. The channel host built in Release without warnings/errors and now runs separately in `simulated` mode. A temporary HTTPS tunnel to port 7454 passed the exact verification challenge; unsigned public POST requests returned HTTP 401. This verifies connectivity, not a complete WhatsApp conversation.

El dueño creó la app y los recursos de prueba y recibió el mensaje de ejemplo del panel. Graph aceptó el token guardado para el número configurado. El host de canales compiló en Release sin errores ni advertencias y está en modo `simulated`. El túnel HTTPS temporal al puerto 7454 devolvió el challenge exacto y rechazó POST sin firma con HTTP 401. Falta registrar el callback en Meta, suscribir `messages`, revisar las condiciones de publicación del panel y comprobar mensajes firmados, respuestas y recibos reales. Telegram sigue apagado y la IA local en este host sigue pendiente.

The Windows launcher now writes only access permissions, avoiding an unnecessary security-privilege requirement. When run by a sandbox, it preserves configuration access for the desktop profile only when that account already has Modify access to the repository. A protected-DACL probe and actual preparation confirmed this account can still edit the file. NuGet uses the same local AppData isolation as the other launchers. These launcher checks and this build do not represent a new run of the historical 324-check suite.

Se corrigieron dos problemas del iniciador de Windows: la actualización de permisos ya no requiere escribir información de seguridad privilegiada y NuGet usa una carpeta local. Se comprobó que el archivo conserva permisos restringidos y que el dueño puede editarlo. Los secretos, la dirección temporal y el estado de los procesos se guardan solo en archivos locales excluidos de GitHub. Este avance no sustituye las pruebas históricas ni cierra los pendientes de integración real.

Later that day, the owner registered the callback and enabled `messages`; the dashboard showed Subscribed at webhook version v26.0. Publishing remained disabled until a privacy-policy URL was supplied. The bilingual [demo privacy notice](../../PRIVACY.md) describes actual data handling and deletion requests. Incoming signed messages and delivery receipts remain unverified. / Más tarde, el dueño registró el callback y activó `messages`; el panel mostró Subscribed en v26.0. Publicar seguía deshabilitado hasta indicar una URL de privacidad. El [aviso bilingüe](../../PRIVACY.md) describe el uso real de datos y las solicitudes de eliminación. Los mensajes firmados y recibos siguen pendientes de comprobar.
