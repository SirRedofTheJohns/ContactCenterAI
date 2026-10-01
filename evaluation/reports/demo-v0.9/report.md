# Recuperación v0.9: regresión y conjunto adicional

IA real, texto final extractivo y citas. No se cambiaron preguntas/oráculos de v0.8. El conjunto adicional se congeló antes del código, pero lo escribió el mismo autor: no es evaluación humana independiente.

| Conjunto | Casos | Exacto | Abstención fuera de alcance | Precisión de selección | p95 ms | Gate local |
|---|---:|---:|---:|---:|---:|---|
| regression | 200 | 95.5% | 95.0% | 98.7% | 4755 | OPEN |
| additional | 100 | 95.0% | 90.0% | 96.2% | 4774 | OPEN |

Se mide recuperación y selección secuencial en CPU. Excluye clasificación de intención, login, concurrencia y carga. Gate humano/empresarial abierto. Informes anteriores y fallos preservados.

## Fallos conservados

### regression

- rag-001-es: esperado `KB-CANCELLATION-ES`, observado `None`; INFERENCE_FAILED.
- rag-004-es: esperado `KB-SERVICES-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-004-en: esperado `KB-SERVICES-EN`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-013-es: esperado `KB-PARKING-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-023-en: esperado `KB-AIRPORT-EN`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-073-es: esperado `KB-PRIVACY-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-073-en: esperado `KB-PRIVACY-EN`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-084-es: esperado `None`, observado `KB-FOOD-ALLERGY-ES`; GROUNDED_EXTRACT.
- rag-095-en: esperado `None`, observado `KB-CANCELLATION-EN`; GROUNDED_EXTRACT.

### additional

- fresh-005-es: esperado `KB-AGENT-ES`, observado `None`; INFERENCE_FAILED.
- fresh-014-en: esperado `KB-LATE-CHECKOUT-EN`, observado `KB-VISITORS-EN`; GROUNDED_EXTRACT.
- fresh-032-es: esperado `KB-KEYS-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- fresh-049-es: esperado `None`, observado `KB-POOL-ES`; GROUNDED_EXTRACT.
- fresh-049-en: esperado `None`, observado `KB-POOL-EN`; GROUNDED_EXTRACT.

