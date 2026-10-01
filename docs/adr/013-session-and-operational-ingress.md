# ADR-013 — Sesiones y recepción durable de conversaciones

Estado: Accepted — implementación local incremental. Fecha: 2026-09-30.

**Contexto.** B01 demostró OIDC/SQL desde el perfil interactivo; B02 contiene solo status. B03/B04 requieren identidad servidor, rotación, CSRF, propiedad y persistencia antes de responder aceptación. El proceso de Codex no puede usar actualmente la clave TLS de Windows ni el driver SQL nativo cifrado, aunque B01 pasó en el perfil del usuario.

**Decisión.** BFF Code+PKCE y cookie `__Host-ccai.session`, Secure/HttpOnly/Lax, vencimiento absoluto de 15 minutos. Una sesión opaca existe en SQL; cada solicitud verifica vigencia y binding activo. Login revoca la sesión previa y adopta sus conversaciones anónimas en una transacción; incrementa version/epoch. Logout local revoca SQL y cookie. Ningún token se guarda en SQL ni cookie. Los roles del token validado se intersectan con el registro sintético servidor. Agent/Supervisor requieren asignación vigente en cada acceso; OperationsAdmin no tiene lectura privada implícita.

GET CSRF emite solo material antiforgery. POST anónimo/login/logout/create/message exige Origin exacto y token antiforgery; callback OIDC conserva state/nonce/correlation del middleware. Toda ruta de producto exige HTTPS. Solo health funciona en el puerto HTTP. No se habilita autenticación simulada en el producto.

SQL Server operacional separado del simulador, DDL versionado y SQL parametrizado con Microsoft.Data.SqlClient 7.1.1. Se elige SQL explícito para revisar locks/transacciones sin añadir un ORM a este slice. Sesión, conversación, mensajes, inbox, idempotencia y audit se escriben transaccionalmente; inbox Pending no implica procesamiento ni resultado empresarial. SERIALIZABLE + índices únicos deduplican claves. Persistencia registra únicamente texto sanitizado y hashes, nunca cuerpos arbitrarios de auditoría. Un fallo SQL devuelve 503 sin 201/202. No hay retry automático de una escritura cuyo resultado se desconoce; el cliente consulta/repite con la misma clave.

**Alternativas.** Identidad del texto/headers del cliente: suplantación. Memoria como store de producto: pérdida tras reinicio. Downgrade a HTTP/SQL sin cifrado para sortear el entorno: viola baseline. EF Core: válido, pero añade paquetes y generación de esquema sin ventaja necesaria en estas operaciones pequeñas.

**Límites.** Revocación local/binding inmediata; cambio de roles o revocación global del IdP todavía requiere backchannel y prueba específica, o vencimiento absoluto. No afirmar cierre B03 completo. Inbox sin worker/leases todavía no cierra B04. La validación SQL mediante Python es tooling offline, no prueba del driver del backend. Integración HTTPS/OIDC del producto permanece gate abierto.

Referencias: FR01/03/04/09/18; AC04–06/14/28/30; [DoR y gates](../progress/b03-b04-ingress.md); [secuencia](../diagrams/sequence-session-ingress.mmd). [Cookie middleware](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0), [antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0).
