# Recuperación v0.10: regresión y conjunto adicional

IA real, texto final extractivo y citas. No se cambiaron preguntas/oráculos de v0.8. El conjunto adicional se congeló antes del código v0.9, pero lo escribió el mismo autor: no es evaluación humana independiente. En v0.10 se vuelve a medir tras el guard diseñado por fallos de la regresión; conservar también su corrida v0.9.

| Conjunto | Casos | Exacto | Abstención fuera de alcance | Precisión de selección | p95 ms | Gate local |
|---|---:|---:|---:|---:|---:|---|
| regression | 200 | 96.5% | 97.5% | 99.4% | 4782 | OPEN |
| additional | 100 | 98.0% | 100.0% | 98.7% | 4760 | PASS |

Se mide recuperación y selección secuencial en CPU. Excluye clasificación de intención, login, concurrencia y carga. Gate humano/empresarial abierto. Informes anteriores y fallos preservados.

## Fallos conservados

### regression

- rag-004-es: esperado `KB-SERVICES-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-004-en: esperado `KB-SERVICES-EN`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-013-es: esperado `KB-PARKING-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-023-en: esperado `KB-AIRPORT-EN`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-073-es: esperado `KB-PRIVACY-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-073-en: esperado `KB-PRIVACY-EN`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-095-es: esperado `None`, observado `KB-CANCELLATION-ES`; GROUNDED_EXTRACT.

### additional

- fresh-014-en: esperado `KB-LATE-CHECKOUT-EN`, observado `KB-VISITORS-EN`; GROUNDED_EXTRACT.
- fresh-032-es: esperado `KB-KEYS-ES`, observado `None`; INSUFFICIENT_EVIDENCE.

