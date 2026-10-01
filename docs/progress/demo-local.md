# Presentación local — estado actual

La entrega vigente es **[demo funcional v0.6](demo-v0.6.md)**. Login A/B y agente asignado, FAQ ES/EN con citas, propuesta, confirmación, cancelación ficticia persistente y handoff quedaron observados en navegador. Real: Keycloak, C# y SQLite/HTTP fuente. La selección actual añade un LLM real local para intención; contact center sigue simulado. Evidencia anterior v0.5 conserva su modo simulado.

El perfil sigue usando HTTP loopback 7452/7453 y bases separadas de ADR-014/015. Cookie HttpOnly/Lax, Host/Origin/CSRF, identidad servidor, binding/roles y ownership se conservan. Ocho cuentas ficticias comparten 123456Aa!; claves privadas se cargan automáticamente, quedan en .env ignorado y nunca van al ZIP. No se cambió confianza de certificados ni volúmenes de Docker en esta entrega.

[18 smoke HTTP](demo-api-evidence.json), [17 storage checks](demo-storage-evidence.json), [22 source checks](b05-source-evidence.json), [25 transacciones](b06-b07-evidence.json), [36 asistente/handoff](b08-b12-evidence.json); los 33 checks de HTTPS con fixtures y 21 de foundation son gates separados. [Evaluación](../../evaluation/reports/demo-v0.5/report.md) declara proveedor simulado.

Se resolvió el bloqueo anterior usando acciones normales en la pestaña abierta, sin saltar barreras. El antiguo intento de navegar directamente a un recurso privado extranjero no se observó como 404; su aislamiento lo prueban suites de repositorio/API, y no se reintentó como verificación redundante.

B01 permanece cerrado en su scope; la nota histórica de retiro de confianza temporal del certificado Windows original no confirmado tras cerrar terminal sigue en su reporte. Enterprise SQL/TLS/global IdP revocation y tenant Genesys no se cierran por equivalencia con SQLite. [Guía sencilla](../../DEMO.md).
