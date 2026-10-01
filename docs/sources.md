# Fuentes, fecha y uso

Consultadas el 2026-09-30. Son referencias para contrastar capacidades actuales; las decisiones del proyecto son propuestas propias. Revalidar APIs, soporte y condiciones al implementar.

| Fuente | Qué respalda / límite |
|---|---|
| Descripción AI Agent Developer, GBS, aportada por el usuario | Competencias del rol; no verificación de estado actual de la oferta |
| [Política de soporte .NET](https://dotnet.microsoft.com/en-us/platform/support/policy) | Selección .NET 10 LTS; fijar SDK/patch posteriormente |
| [Microsoft HTTP resilience](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience?tabs=dotnet-cli) | Configuración de resiliencia HTTP; política por operación del proyecto |
| [Genesys Digital Connector](https://help.genesys.cloud/articles/genesys-digital-bot-connector-overview/) | Modalidad bot asincrónica; no prueba de acceso/licencia/GA en un tenant |
| [Genesys Web Services Data Actions](https://help.genesys.cloud/articles/about-web-services-data-actions-integration/) | Integración JSON y Architect; timeout/esquema real por validar |
| [Genesys release 2026-03-02](https://help.genesys.cloud/release-notes/genesys-cloud/march-2-2026/) | Cambio anunciado de fecha del endpoint Open Messaging |
| [Genesys tracker](https://help.genesys.cloud/announcements/?theme=simplified) | Fecha anunciada 2026-10-05; revalidar al conectar |
| [Aviso Open Messaging](https://help.genesys.cloud/?p=338177) | Endpoints separados message/event/receipt |
| [Digital bot customer API spec](https://developer.genesys.cloud/commdigital/textbots/digital-botconnector-customer-api-spec) | Página localizada, contenido JS no extraíble; wire contract NO verificado |
| [Qdrant payload](https://qdrant.tech/documentation/concepts/payload/) | Filtros e índices de metadata; SQL recheck es decisión del proyecto |
| [OWASP prompt injection](https://cheatsheetseries.owasp.org/cheatsheets/LLM_Prompt_Injection_Prevention_Cheat_Sheet.html) | Amenazas y defensas en capas; no garantía de inmunidad |
| [RFC 9700](https://www.rfc-editor.org/rfc/rfc9700.html) | Prácticas actuales OAuth; implementación IdP por probar |
| [OpenAI function calling](https://developers.openai.com/api/docs/guides/function-calling) | Esquemas estrictos; no autorización semántica |
| [OpenAI data controls](https://developers.openai.com/api/docs/guides/your-data) | Distinguir entrenamiento, retención y elegibilidad; no prueba de configuración de cuenta |

No se consultaron ni importaron datos internos de GBS. No se afirma que GBS use SQL Server, Qdrant, Angular o el mecanismo Genesys elegido; son selecciones del portafolio.



## CI local v0.5 — referencias oficiales consultadas 2026-10-01 UTC

La declaración usa las versiones de ejemplo vigentes de [checkout](https://github.com/actions/checkout), [setup-dotnet](https://github.com/actions/setup-dotnet), [setup-python](https://github.com/actions/setup-python) y [upload-artifact](https://github.com/actions/upload-artifact). Runner/configuración no se ejecutaron en GitHub. Se usan tags de versión; pinning por commit SHA y revisión del runner quedan para la activación del release empresarial.

## IA local v0.6

Contrato: [generate/raw/format](https://docs.ollama.com/api/generate), [modo local y loopback](https://docs.ollama.com/faq), [Windows](https://docs.ollama.com/windows). Versión/digest, template y timings proceden de observación local registrada; no inferir compatibilidad de otro modelo/runtime.
