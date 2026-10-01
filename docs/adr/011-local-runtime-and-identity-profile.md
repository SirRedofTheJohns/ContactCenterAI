# ADR-011 — Perfil de compatibilidad local

Fecha: 2026-09-30. Estado: **Accepted para preparación local; SQL/OIDC runtime pendientes**. Complementa ADR-002/006 sin cambiar autoridad ni reabrir Freeze v0.1.

## Decisión y razón

Fijar SDK .NET 10.0.401/runtime 10.0.12, SQL Server 2022 Developer y Keycloak 26.7.5 por digest; SQL Client 7.1.1/OIDC 10.0.12 con transitivas lock. Validar mediante spike B01 separado de `src/` antes de handlers productivos.

Keycloak implementa el IdP OIDC previsto: cliente confidencial Code+PKCE S256, callback HTTPS exacto, `sub`/`ccai_role`. Mapping member/tenant sigue servidor. SQL 2022 permite probar el motor empresarial en laboratorio; build real se registrará al conectar. Edición Developer no constituye una licencia de producción.

## Alternativas y consecuencias

Un IdP externo añade cuenta/red innecesarias al primer slice. JWT mock exclusivamente no prueba login: mantener gate interactivo. SQL Express limita el laboratorio; Developer permite probar el motor completo. Distribuciones dev/H2/HTTP loopback no son perfil remoto.

Digest/pins hacen el entorno reproducible; no prueban seguridad ni compatibilidad. Revisar patches/advisories en cada hito y repetir spike al cambiar locks. Términos Developer se revisan antes de iniciar SQL.

## Evidencia

SDK oficial HTTPS/SHA-512 verificados; Release compila, doce checks offline pasan. Inventario de 17 paquetes resueltos y consulta oficial de advisories sin coincidencias conocidas: [evidencia](../progress/b01-dependencies.json). Excluye OS/SDK/capas de contenedor y no es certificación.

Docker named pipe devuelve acceso denegado y TLS .NET falla. SQL, realm import y login no están probados; B01 sigue abierto/B02 no Ready. [Reporte](../progress/b01-runtime-identity.md), [runbook](../../deploy/local/README.md).
