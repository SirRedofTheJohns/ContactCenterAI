# Portafolio y preparación de entrevista

**Estado actual:** [demo local v0.10 y evidencia](progress/demo-v0.10.md). La implementación local está lista para presentar; los gates empresariales se distinguen de su alcance.

## Recorrido de cinco minutos

Seguir [DEMO.md](../DEMO.md): login real de customer-a, pregunta de piscina con cita abierta, política CP-001, reservas propias y propuesta de cancelación. Revisar sin confirmar si quieres conservar RES-001. Explicar que «sí» en chat no autoriza la mutación y que éxito exige receipt de la fuente. Después mostrar handoff mock/asignación o el panel operations-admin. Se conservan pruebas de replays, concurrencia, respuesta perdida y restart; no necesitas repetir fallos destructivos en cada presentación.

## Pitch basado en evidencia

“Construí un contact center bilingüe con backend C#/.NET y login real. El modelo interpreta y propone; el servidor decide identidad, permisos, elegibilidad, confirmación y resultado. La cancelación se guarda como comando durable y solo se confirma con un receipt de una fuente HTTP separada. El RAG usa embeddings reales, versiones aprobadas, permisos y citas revalidadas. Probé 249 invariantes, medí 300 preguntas y conservé los fallos; una corrección de alcance tiene replay explícito. La demo usa SQLite y un adapter neutral mock; SQL empresarial, Genesys real y CI remota tienen gates pendientes.”

## Pitch in English

“ContactCenterAI is a working architecture-first bilingual .NET portfolio demo. The model proposes bounded tools; deterministic code controls identity, resource permissions, cancellation rules, explicit confirmation and source-confirmed outcomes. A durable command ledger reconciles uncertain results. Real local embeddings retrieve approved, authorized passages with revalidated citations. I ran 249 deterministic checks and measured 300 retrieval cases, publishing failures and a clearly labeled scope-only replay. SQLite and a provider-neutral contact-center mock support the presentation; enterprise SQL, a live Genesys tenant and remote CI remain separately gated.”

## Un fallo que puedes explicar

Reducir el umbral mejoró recall, pero hizo que preguntas sobre antibióticos y reembolsos seleccionaran documentos cercanos. Se bloqueó el candidato, se agregó alcance determinista después de autorización, se probaron falsas alarmas y se conservaron las corridas. La variante española `devolver` requirió corregir conjugaciones. El cierre compuesto llega a 97%/98% y 60/60 abstenciones OOD; siguen ocho errores, incluyendo late checkout confundido con visitantes. No llamar a los conjuntos actuales holdout ni a una cita válida garantía de pertinencia.

## Preguntas difíciles y respuestas defendibles

| Pregunta | Respuesta defendible / evidencia |
|---|---|
| ¿Por qué C# y no todo Python? | Core empresarial .NET alineado a vacante; Python analiza/evalúa offline. No dos runtimes tomando decisiones de negocio. |
| ¿Por qué monolito? | Scope pequeño, transacciones SQL locales y módulos testeables. Worker separado para durable jobs; microservicios necesitan una razón de escala/equipo medida. |
| ¿Quién decide cancelar? | Policy Engine + fuente. El modelo no tiene cancel write tool; confirmación UI desencadena comando auditado. |
| ¿RBAC basta? | No: cliente propio y agente asignado, ACL y tenant en cada acceso. Mostrar AC05/06/21. |
| ¿Qué pasa si el POST hizo commit y timeout? | Unknown, commandId estable, query receipt y reconciliación. Nunca retry ciego ni éxito prematuro. |
| ¿Exactly-once? | Transporte at-least-once; efecto único por claves/constraints fuente. No prometer entrega global exactly-once. |
| ¿Cómo evita prompt injection? | No asegura erradicación. Reduce impacto con permisos/allowlist/validaciones externas y no exponer tools de alto riesgo. |
| ¿Qué significa una cita válida? | Existe y es accesible/vigente; además debe respaldar claim, medido con anotación. IDs válidos no garantizan verdad semántica. |
| ¿Por qué Qdrant/SQL en el diseño? | En el diseño SQL es autoridad y Qdrant proyección reconstruible. La demo usa SQLite y coseno exacto con recheck; no decir que ejecuta Qdrant ni SQL de producto. |
| ¿Cómo integra Genesys? | Distinguir Digital Connector, Data Actions, Open Messaging y Architect; contrato real depende de tenant y workflow de routing. |
| ¿El bot continúa después del handoff? | No tras acknowledgment y HumanOwned; epoch invalida envíos viejos. Comando in-flight sigue reconciliándose para contexto humano. |
| ¿Cómo evalúa un cambio de modelo? | Manifest, corpus ES/EN pareado, holdout y gates safety; no seleccionar mejor corrida ni usar judge como oráculo de auth. |
| ¿Cumple PCI/privacidad? | No afirmado. Demo sintética sin pagos; datos reales requieren revisión contractual/seguridad/negocio y gates nuevos. |
| ¿Qué falta para producción? | Tenant/source reales, políticas aprobadas, privacy controls efectivos, HA/restore medido, evals y operación/soporte. |

## Evidencia profesional honesta

El proyecto demuestra decisiones, ejecución y aprendizaje; no acredita años de experiencia, inglés profesional ni operación real de Genesys que no se tenga. Mostrar docs/diagramas/ADRs, un resultado medido, un fallo y el control aplicado. Explicar qué es mock, qué se midió localmente y qué requiere un tenant. CI declarada con su comando aprobado localmente no equivale a un run GitHub. La conexión SQL cifrada tiene un diagnóstico reproducible; no se rebajó el cifrado para presentarla como terminada.
