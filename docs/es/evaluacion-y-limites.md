# Evidencia y límites

[English](../en/evaluation-and-limits.md)

## Seguridad y funcionamiento

La base v0.10 registró **249 comprobaciones deterministas** en nueve suites y build Release sin warnings ni errores. Cubren permisos por recurso, persistencia, conflictos y duplicados fuente, recuperación de respuesta perdida, leases, ownership humano, contratos cerrados de modelos, permisos/revisión de conocimiento y panel operativo. Ver [informe](../progress/demo-v0.10-checks.json).

Usan relojes controlados y fallos inyectados. No prueban un tenant Genesys, la composición SQL Server completa, calidad bilingüe independiente ni carga de producción. Existe workflow del mismo comando; la evidencia de publicación registra si su ejecución remota realmente pasó.

La extensión v0.11 añade 75 checks aislados y la regresión completa actual pasa 324. El [estado de canales](../progress/channels-v0.11.md) distingue HTTP simulado de aceptación real. El primer runner de CI remota no pudo iniciar por una restricción de plataforma/cuenta; pasar localmente no equivale a pasar en GitHub.

## Calidad de respuestas

| Evidencia | Resultado | Interpretación |
|---|---|---|
| 200 respuestas originales locales | 96.5% exacto, 39/40 abstenciones OOD, sin errores de inferencia, p95 4,782 ms | Regresión conocida |
| 100 respuestas originales adicionales | 98% exacto, 20/20 abstenciones OOD, sin errores de inferencia, p95 4,760 ms | Mismo autor; sin aprobación independiente |
| Replay compuesto de alcance | 97% / 98%; 60/60 abstenciones OOD | 13 checks actuales y 287 outputs reutilizados; no inferencia nueva |

OOD significa fuera del tema soportado. Quedan ocho errores de calidad en el resultado compuesto, incluido un documento incorrecto para late checkout. Datos, expectativas, respuestas y errores originales se conservan en [informe original](../../evaluation/reports/demo-v0.10/report.md) y [replay](../../evaluation/reports/demo-v0.10/scope-replay/report.md).

La latencia es la de retrieval medido en el equipo original; no corresponde al replay, entrega por mensajería ni tráfico concurrente de extremo a extremo. Cambiar documentación no produce mediciones nuevas.

## Pendientes

- Conexión cifrada del producto a SQL Server y aceptación del repositorio SQL completo; bloqueadas en el runtime Windows original.
- Tenant Genesys real: configuración, routing, aceptación y transferencia.
- Revisores bilingües independientes y casos no conocidos.
- HTTPS de producción, revocación, retención efectiva, telemetría distribuida, carga y disponibilidad.
- Aceptación real de WhatsApp y Telegram cuando existan credenciales y configuración.

El proyecto demuestra decisiones y comportamiento local. No acredita años de experiencia, certificación empresarial ni cada función de la vacante original.
