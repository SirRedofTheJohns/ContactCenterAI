# Evaluación de inferencia local real v0.6

Resultado: **PASS_DEV_SAMPLE**. Modelo local **qwen3-vl:latest**, digest fijado. No es el simulador.

| Idioma | Intent correcto | Propuesta exacta | Wilson 95% (exacta) |
|---|---:|---:|---|
| es | 20/20 | 19/20 | [0.7639, 0.9911] |
| en | 20/20 | 19/20 | [0.7639, 0.9911] |

40 consultas secuenciales: p50 3071 ms, p95 3639 ms, máximo 3729 ms; timeouts 0. Deadline por petición 8 s. No es p95 de todo el chat ni benchmark bajo carga.
Uso registrado: 11705 tokens de entrada, 1177 de salida en 40 respuestas medidas. Cero llamadas cloud. Energía y costo de equipo no medidos.

## Errores registrados sin ocultarlos

- unknown-es: {"actual": {"intent": "faq", "language": "es", "reservationId": null, "topic": "payments"}, "errorCode": null}
- unknown-en: {"actual": {"intent": "faq", "language": "en", "reservationId": null, "topic": "payments"}, "errorCode": null}

El prompt quedó fijado antes de la corrida; estos resultados no se convirtieron en un holdout. Los escenarios de control son públicos y pocos; no prueban generalización. La recuperación sigue siendo por temas y la respuesta factual es extractiva desde corpus gobernado. El modelo no puede confirmar/cancelar por chat.

El gate empresarial G3 sigue OPEN. Los dos errores son preguntas sobre precios de vuelos clasificadas como payments, en lugar de unknown. La plantilla de pagos no inventa precios, pero el routing fuera de alcance requiere mejora y revisión de abstención antes de una afirmación de calidad general.

- Public development set, not an unseen holdout. One developer authored/assessed the cases.
- This evaluates intent/proposal classification, not open generation, semantic citation support, dense retrieval, or enterprise workload latency.
- Power/resource costs were not measured. Startup was measured separately; 40 sequential requests are not a load benchmark.
- Deterministic authorization/workflow safety is tested separately. No production quality, security certification or tenant Genesys validation.
