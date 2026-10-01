# Demo v0.6 — IA real local para interpretar intenciones

Estado: DONE en este slice local. Diseño antes del código: [freeze v0.6](../architecture-freeze-v0.6-local-ai.md), [ADR-019](../adr/019-local-live-intent-provider.md). Enterprise G3, SQL/Qdrant/Genesys y delivery remoto conservan sus gates. La entrega anterior [v0.5](demo-v0.5.md) documenta transacciones/handoff; no se resetearon las DB ni se repitió otra cancelación real de la fuente ficticia.

## Qué cambió

El backend C# llama al modelo qwen3-vl:latest ya instalado mediante Ollama 0.32.15. Digest local fijado 901cae73216286ea8c5aba8b46d307ff7188f737285ec500c795a12f05225d28. No se descargaron modelos ni se enviaron mensajes a cloud. Adapter HTTP con endpoint fijo loopback, sin proxy/redirect, body <=64 KiB, schema/gateway cerrados y ocho segundos por consulta. Thinking no se lee ni se usa como propuesta. Cliente se serializa/escapa antes del wrapper fijo. Modelo devuelve intent/topic/argumento; auth, ACL, eligibility, offers/confirmations, epoch y source receipts siguen en C#.

FAQ sigue siendo extractiva desde conocimiento gobernado, y reservas/acciones provienen de la fuente. No se añadió generación libre ni Qdrant/embeddings. UI diferencia IA local real y simulador, incluso en respuestas históricas. Selección local se conserva al abrir otra vez; [Activar-IA-Local.cmd](../../Activar-IA-Local.cmd) y [Usar-Simulador.cmd](../../Usar-Simulador.cmd) permiten cambiarla. Misma clave ficticia y login real. Cambiar modo no reinicia bases ni hace una transacción.

Compatibilidad medida: carga inicial 32.11 s. Chat/GPU del modelo instalado devolvió timeout/contenido vacío; nunca se parseó thinking para rescatar respuesta. Generate/raw con wrapper fijo/CPU produjo JSON válido en 2.938 s. Estas son pruebas de diagnóstico separadas, fuera del benchmark. La plantilla pública del modelo era {{ .Prompt }}. No se modificó el modelo ni su template almacenado. El cold start puede tardar y ocurre antes de activar el API.

## Evidencia

- Build Release: cero warnings/errors. Suite completa ejecutada una vez después de integrar el modo: **182 checks C# PASS**, incluyendo [28 del adaptador](b10-local-ai-contracts.json), los 36 de assistant y los 25 de workflow. El runner [Verify-Demo](../../eng/Verify-Demo.ps1) usa simulated + HTTP fixtures para reproducir seguridad sin modelos; la inferencia real se evalúa aparte.
- [Corrida C# real](local-ai-run.json) sobre 40 casos públicos, outputs en [registro](local-ai-intent-outputs.json). [Python/reporte](../../evaluation/reports/demo-v0.6/report.md): intent 20/20 por idioma, propuesta exacta 19/20 por idioma, p50 3071 ms/p95 3639 ms, máximo 3729 ms y cero timeouts. Solo consultas secuenciales de clasificación, no latencia total del chat ni load test.
- Dos errores se conservaron: precios de vuelos ES/EN dirigidos a payments en vez de unknown. La respuesta de pagos rechaza procesamiento y ofrece humano, no inventa precios; aun así, no afirmar routing correcto ni una evaluación general de abstención. **G3 OPEN** para holdout empresarial y revisión semántica. El prompt no se retocó para ocultar esos errores.
- Navegador: login real Cliente A, crear conversación nueva, «Explícame en qué casos puedo cancelar sin penalidad» → política CP-001 con cita ES y etiqueta **Asistente · IA local real**. Vista final conservada. [Captura](screenshots/local-ai-v0.6.jpg). La respuesta citada proviene del corpus, el modelo interpreta la petición.

Una compilación inicial chocó con las DLLs de la demo abierta. Se usó un directorio de checks independiente y el lanzador detuvo solo API/source propios antes de actualizar. Build y suite final pasaron; no se mataron procesos ajenos. No se cambió confianza de certificados, contraseñas ni configuración permanente del usuario.

La validación de /api/tags mejora reproducibilidad, no constituye una attestation criptográfica de los pesos de cada respuesta; un servidor local alterado sigue siendo no confiable. La seguridad del negocio no depende de que el modelo se comporte bien. Una máquina distinta necesita instalar Ollama y disponer del modelo con el digest aprobado; el ZIP no incluye los pesos ni el SDK.

## Pendientes vigentes

SQL Server/TLS del producto y revocación IdP global; recuperación vectorial multilingüe; holdout de 200 casos y revisión de soporte/abstención; Angular; exporter/dashboard OTel, CI remoto y restore/deployment; tenant Genesys real opcional. Son validaciones adicionales, no cierres por equivalencia con la demo. [Guía](../../DEMO.md), [backlog](../08-backlog.md).
