# B01 RuntimeCompatibility

Spike C#/.NET 10, SQL Client y OIDC fuera de `src/`; sin comportamiento productivo. Refs: NFR12–13, AC04; ADR-002/006/011; Definition of Ready.

- `--offline`: doce checks de runtime/driver, Code+PKCE/cookie/mapping y JWT RS256 generado en memoria. Rechaza issuer/audience erróneos, expiración, token sin firma y firma de otra clave. No prueba resource auth, CSRF, revocación ni Keycloak.
- `--live`: SQL 2022 Developer, discovery issuer exacto/S256 y JWKS local con RSA. Fallos terminan con exit code 2 sin imprimir credenciales/excepciones del proveedor. No sustituye login real.
- `--live-managed`: comparación de networking para debugging, manteniendo Encrypt Mandatory; no selección de runtime productivo. Errores SQL imprimen fase/códigos y markers allowlist, nunca Message/ConnectionString/secretos. En Windows Compose el endpoint explícito es IPv4 loopback.
- `--serve-login`: diagnóstico HTTPS loopback. `/proof` exige cookie autenticada, valida el subject/rol del fixture y registra Code+PKCE, callback y atributos reales de Set-Cookie sin guardar valores. El resultado solo pasa después del regreso del navegador con esa cookie. Evidencia temporal en `.local/b01-login`, ligada a un run ID nuevo; no tokens ni SQL. El helper Windows abre perfiles separados y exporta un informe únicamente cuando ambas cuentas pasan.

Usar [scripts del entorno local](../../deploy/local/README.md), que fijan SDK, aíslan caches y restauran locked-mode. [Lockfile](packages.lock.json), [descarga SDK](sdk-evidence.json).

**Cierre B01:** build/offline PASS; live SQL/OIDC PASS; login de ambas cuentas con subject/rol esperado; inspección de cookies/callbacks sin exportar secretos; evidencia registrada. Solo entonces B02 inicia según DoR.

Estado: build/offline y conectividad live nativa PASS; login interactivo pendiente. `eng/Start-B01Login.cmd -BuildOnly` verifica la compilación sin certificado/navegador. El helper completo requiere `-TrustLocalCertificate`; ver el runbook para sus efectos y limpieza. Las observaciones del middleware no sustituyen las futuras pruebas de autorización por recurso, CSRF, revocación o segregación entre miembros.

HTTPS Windows: `CreateSelfSigned` directo produjo `0x8009030E` en una reproducción instrumentada. Se recarga PKCS12 en memoria con UserKeySet sin PersistKeySet y se detiene el host normalmente para liberar el contenedor de clave. Un logger dedicado solo emite tipos/HResult/códigos nativos de Kestrel HTTPS; no usa formatters ni mensajes. El preflight cliente conserva la validación HTTPS. La sesión restringida no puede importar el contenedor (`0x80070002`); el resultado corregido real sigue pendiente. Ver [diagnóstico HTTPS](../../docs/progress/b01-https-diagnostic.json).
