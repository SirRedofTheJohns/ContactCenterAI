# ADR-018 — Preparar otra presentación sin perder la anterior

Estado: Accepted, operación local v0.5.1. Scope: NFR14/18, demo reproducible. Diseño cerrado antes de añadir el control al lanzador; no cambia permisos ni reglas productivas.

Las fechas fuente persistidas envejecen y las cancelaciones realizadas permanecen canceladas. No mover silenciosamente check-in en cada arranque ni borrar receipts para volver a mostrar una reserva elegible. Añadir un lanzador separado Preparar-Demo.cmd que solicita explícitamente FreshData: detiene solo procesos propios con metadata/executable/startTime verificados y archiva las dos DB y sidecars en .local/demo-backups antes de iniciar otro dataset sintético. Claves/realm/certificados no cambian. Abrir-Demo.cmd conserva el dataset actual.

Validar paths absolutos dentro de .local y rechazar reparse points antes de mover; usar un solo shell PowerShell y Move-Item -LiteralPath. No borrar archivos ni volúmenes. La app crea nuevas fechas/fixtures al iniciar las bases vacías; archive conserva histórico/receipts. Perfil HTTP/SQLite/synthetic conocido: no aplicar a producción ni a datos reales. El acceso del navegador vuelve a iniciar sesión porque sus sessions anteriores se archivan.

La preparación nueva queda disponible para el usuario. En esta entrega no se invoca sobre el dataset cuya evidencia de navegador se conserva; sus instrucciones/AST se inspeccionan sin afirmar un reset observado.
