# Cierre local v0.10: replay compuesto

La corrida real inicial permanece intacta en el directorio superior. Solo cambió el reconocimiento determinista de conjugaciones de devolver dinero. Cada consulta que el filtro reconoce pasó de nuevo por el repositorio C# autorizado, con cero llamadas de inferencia. Para los caminos sin cambio se reutilizan exactamente sus outputs medidos. No son 300 nuevas inferencias ni un nuevo benchmark de latencia. Casos conocidos del mismo autor; revisión humana independiente abierta.

| Conjunto | Casos | Exacto compuesto | Abstención fuera de scope | Checks actuales del filtro | Outputs reutilizados | Gate local |
|---|---:|---:|---:|---:|---:|---|
| regression | 200 | 97.0% | 100.0% | 5 | 195 | PASS |
| additional | 100 | 98.0% | 100.0% | 8 | 92 | PASS |

## Errores conservados

### regression

- rag-004-es: esperado `KB-SERVICES-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-004-en: esperado `KB-SERVICES-EN`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-013-es: esperado `KB-PARKING-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-023-en: esperado `KB-AIRPORT-EN`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-073-es: esperado `KB-PRIVACY-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-073-en: esperado `KB-PRIVACY-EN`, observado `None`; INSUFFICIENT_EVIDENCE.

### additional

- fresh-014-en: esperado `KB-LATE-CHECKOUT-EN`, observado `KB-VISITORS-EN`; GROUNDED_EXTRACT.
- fresh-032-es: esperado `KB-KEYS-ES`, observado `None`; INSUFFICIENT_EVIDENCE.

