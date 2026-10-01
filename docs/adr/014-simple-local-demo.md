# ADR-014 — Presentación local sencilla

Estado: Accepted por instrucción explícita del usuario de simplificar la demo y sus claves. Fecha: 2026-09-30.

**Problema.** El perfil Windows del agente impide claves TLS y conexión nativa SQL Server. El usuario necesita mostrar habilidades, no gestionar certificados ni claves aleatorias en cada paso. Una contraseña nueva no corrige esas restricciones.

**Decisión.** Agregar un perfil de presentación `--demo-local`: C#/.NET, SQLite persistente en `.local/demo`, UI servida por el mismo host y login OIDC real Code+PKCE con Keycloak existente. Un solo origen fijo `http://127.0.0.1:7452`, socket loopback y rechazo de Host/origin remotos. Sin modificaciones al almacén de certificados Windows. Cookie demo HttpOnly/Lax sin Secure exclusivamente en loopback HTTP; formulario callback GET mediante response_mode=query para no depender de cookies cross-site HTTP. Perfil empresarial mantiene HTTPS/cookie Secure/SQL Server.

Todas las cuentas ficticias usan `123456Aa!`, clave pública de demostración. Roles y MemberRef siguen vinculados en el servidor por subject validado, nunca por texto. Claves internas de SQL/client/admin permanecen automatizadas y excluidas del repo; no se cambian indiscriminadamente porque los contenedores persistidos las usan en sus health checks. El usuario no necesita verlas ni recordarlas.

SQLite conserva sesiones, rotación/revocación local, idempotencia, versiones, inbox y audit en transacciones. No es store en memoria ni auth de fixture habilitada en el runtime. Se conserva el adapter SQL Server para mostrar el objetivo empresarial; evidencia de SQLite no cierra sus pruebas específicas. No simular éxito de IA/reservas cuando solo hay mensaje Pending.

**Tradeoff.** HTTP loopback/clave conocida son aceptables solo para este entorno sintético de presentación, sin exposición de red/datos reales. SQLite no prueba concurrencia/privilegios SQL Server, ni HA. Esta excepción de transporte/almacenamiento requiere [baseline de demo v0.2](../architecture-freeze-v0.2-demo.md); no reescribe el cierre histórico v0.1 ni promete producción.

**Oráculos.** Browser login A/B reales; dueño A permitido y B 404; logout invalida cookie; contraseña equivocada rechazada por IdP; SQL SQLite persiste tras nueva instancia; misma clave devuelve mismo ID; payload distinto/version stale conflict; host remoto/Origin extra rechazados; empresarial conserva rechazo HTTP.

Fuentes: [ASP.NET OIDC](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-oidc-web-authentication?view=aspnetcore-10.0), [transacciones SQLite](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions).
