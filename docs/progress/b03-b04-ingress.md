# B03/B04 — Sesión y conversación durable

Estado: **candidato implementado y verificado parcialmente; B03/B04 IN PROGRESS**. DoR delimitado para código/pruebas del slice; integración del producto NO liberada. Freeze v0.1 CLOSED; refinamiento compatible ADR-013 Accepted.

Actualización posterior: el usuario autorizó un perfil de presentación distinto mediante ADR-014 y freeze v0.2. [La demo local](demo-local.md) tiene UI servida, SQLite persistente real y challenge OIDC; no cierra los gates SQL/HTTPS de este reporte. La restricción histórica de no liberar una demo antes de esos gates se mantiene para el perfil empresarial; v0.2 permite la presentación sintética separada.

| DoR | Evidencia previa a handlers |
|---|---|
| Requisitos/actores | FR01/03/04/09/18, AC04–06/14/28/30, Customer A/B, guest, Agent asignado/no asignado |
| Componentes/autoridad | C4 API/Application/Operational SQL; binding servidor, fuente de reservas separada |
| Flujo | [Sesión y recepción](../diagrams/sequence-session-ingress.mmd), estados conversación existentes |
| Contrato | conversations-v0.1 actualizado, session-v0.1; DTO cerrado, códigos y errores |
| Decisión | [ADR-013](../adr/013-session-and-operational-ingress.md), ADR-006/007 |
| Amenazas | Spoofing, CSRF, fixation, cross-tenant, IDOR, replay, pérdida de commit, PII |
| Oráculos | Distintos owners, binding revocado, asignación vencida, 100 duplicados, payload diferente, version stale, SQL caído |
| Dependencias | B01 real PASS; paquetes pin/hash; limitación Windows del agente explícita |
| Release/rollback | No demo funcional ni cierre B03/B04 hasta prueba live del producto. DDL aditivo; no eliminar volúmenes ni datos |

Entrega delimitada: cookies/OIDC configurados, política determinista, sesión SQL, creación/lectura de conversación y recepción de mensajes sanitizados en inbox. Reservas requieren B05; no se crea una copia operacional que finja ser la fuente. Worker todavía no consume inbox. Las pruebas aisladas pueden sustituir puertos desde la composición del test, nunca por una opción del producto.

Gates restantes: roundtrip HTTPS/OIDC del producto con SQL usando su credencial restringida; revocación global IdP; durabilidad y concurrencia del repositorio .NET en SQL live; leases/dispatch/reinicio worker; retención/limpieza. Cada reporte separará evidencia de política/pipeline, DDL SQL real y driver/producto live.

## Entrega y evidencia observada

| Resultado | Evidencia y alcance |
|---|---|
| Build/locks | Release con cero warnings/errors, restore locked; .NET 10, cinco proyectos runtime y dos ejecutables de checks |
| Política/pipeline | [33 PASS](b03-b04-ingress-evidence.json): ownership, tenant, asignación/revocación/expiry, CSRF/Origin, cookie segura, DTO cerrado, replay, conflicto, PII/pago, SQL outage simulado y liveness |
| SQL real | [Provisioning](b04-sql-provisioning.json): base Operations, ocho principals ficticios, app login sin DDL/sysadmin/DELETE, sin escritura de bindings/asignaciones/audit, conexión cifrada |
| Constraints SQL reales | [6 PASS](b04-schema-evidence.json): tenant FK, owner CHECK, client-message UNIQUE, key-scope UNIQUE, inbox-message FK y rollback de fixtures |
| IdP real | [Callback exacto](b03-identity-provisioning.json) 7451 registrado, 7443 preservado, confidencial, S256, sin wildcard/password grant BFF |
| Dependencias C# | [26 paquetes resueltos](b03-b04-dependencies.json), hashes oficiales/locks verificados, cero coincidencias con advisories conocidos; no incluye OS/SDK/containers/Python |
| Foundation regresión | 21 PASS y worker start/stop; HTTP de producto 426, liveness 200, ready 503 sin configuración |

Los cien replays de creación verifican el contrato HTTP con fixture; **no prueban aislamiento ni un único commit del repositorio SQL**. El catálogo SQL prueba constraints, no ejecución de cada método C#. B01 validó su diagnóstico real, no este host de producto. No hay respuesta del LLM ni agente humano conectado.

## Límites de operación

El perfil interactivo del usuario pasó SQL/OIDC en B01. En esta sesión del agente el driver SQL cifrado falla en networking nativo y la clave TLS Windows no puede crearse; acceso adicional a la carpeta de claves no fue concedido. No se desactivó cifrado ni se habilitó auth fake. Python se usó únicamente para provisioning y validación de datos local.

Una comprobación inicial de cierre del smoke encontró una carrera en la tabla TCP de Windows. Se acotó la espera de teardown a dos segundos y se verificó luego la parada del host. Los ejecutables de checks ahora capturan fallos y retornan exit code normal para evitar Windows Error Reporting. Dos procesos fallidos iniciales pudieron quedar suspendidos con DLLs antiguas; su identificación/cierre no pudo confirmarse desde este perfil restringido. No se detuvieron procesos ajenos; los builds actuales usan `.local/build-ingress`. Esto es housekeeping local, no evidencia de un API de producto en ejecución.

Data Protection usa archivos locales excluidos del repositorio/ZIP sin cifrado de archivo; solo fixture local, pendiente gestión de claves para producción. Detector de PII es acotado y puede omitir formatos: no acredita cumplimiento ni uso de datos reales. Transcript devuelve los primeros 100 mensajes; paginación, cuotas y limpieza de retención son gates futuros. Logout cierra la sesión local; no promete logout global del IdP.

## Reproducción del slice

PowerShell 7: `eng/Invoke-IngressChecks.ps1` para locks/build/policy/pipeline y `eng/Invoke-Foundation.ps1` para arquitectura/smoke. No hacen login al producto ni cambios de certificados. Python aislado: instalar `eng/requirements-data.txt`, ejecutar `eng/Provision-Operational.py --tooling-path <isolated-library-directory>` y `eng/Check-OperationalSchema.py` con el mismo argumento. El provisioning conserva datos, genera una nueva credencial app solo cuando falta y nunca imprime su valor. `eng/Configure-ProductIdentity.py` actualiza solo callbacks/perfil del cliente local.

Variables de runtime: `CCAI_OPERATIONAL_DB_PASSWORD`, `CCAI_BFF_CLIENT_SECRET`, `CCAI_TLS_CERTIFICATE_PATH` y `CCAI_TLS_CERTIFICATE_PASSWORD`. Deben suministrarse al proceso; `.env` no se carga automáticamente. TLS material válido y trusted localhost HTTPS son un gate previo a demo. API no ejecuta DDL ni usa sa. Reserva/simulador B05 continúa pendiente.
