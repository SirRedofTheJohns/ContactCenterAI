# ADR-015 — Fuente de reservas separada para la presentación

Estado: Accepted. Refinamiento B05 autorizado por la continuación del proyecto y la simplificación local solicitada. El diseño empresarial de ADR-002 se conserva.

La fuente es otro proceso ASP.NET Core C#, otro archivo SQLite y un contrato HTTP privado en loopback 7453. El middleware no hace JOIN ni escribe en la base fuente. Una clave de servicio aleatoria se carga automáticamente; la UI no la recibe. El middleware obtiene MemberRef de su sesión validada; no acepta ese dato del cliente. La fuente vuelve a comprobar propiedad, versión, política CP-001 y hora del servidor.

B05 entrega lectura, regla pura de 72 h y ledger de comandos fuente. La UI solo lista reservas propias y evalúa elegibilidad; no crea ofertas consumibles ni envía cancelaciones. El comando fuente es exclusivamente interno: B06/B07 deben implementar confirmación, intención durable, dispatch y reconciliación antes de conectarlo al producto. No hay tool LLM de cancelación.

SQLite simplifica la presentación y no sustituye evidencia de SQL Server. Los archivos fuente/operaciones y procesos son distintos. HTTP/claves de servicio locales son exclusivamente sintéticos, con bind loopback/Host fijo; credenciales y DB quedan excluidas del repo/ZIP. No aporta TLS servicio-servicio ni integración empresarial real.

El ledger conserva commandId y hash de payload durante toda la vida del simulador; resultado y transición fuente se confirman en una misma transacción. Replay idéntico devuelve el resultado; payload distinto devuelve 409. Versión/política/propiedad/hora se validan en fuente. Reabrir instancia conserva receipts. NotFound se garantiza para un commandId nunca aceptado en esta DB consistente; no generalizar esa garantía a sistemas externos.

Oráculos: propietario A/B; fuente caída; exactamente 72 h vs un segundo menos; estado no Confirmed; política/version stale; cien replays/un efecto; concurrencia; payload conflict; reabrir DB/consultar receipt. AC15/16 de recuperación del middleware siguen pendientes.
