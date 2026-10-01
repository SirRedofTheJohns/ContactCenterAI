# Evaluación local v0.5

Resultado: **PASS**. Proveedor **simulado**, corpus sintético, dataset público de desarrollo.

| Idioma | Intent y argumentos correctos | Wilson 95% |
|---|---:|---|
| es | 20/20 | [0.8389, 1.0] |
| en | 20/20 | [0.8389, 1.0] |

Paridad semántica: 20/20 pares. No hay inferencia de un LLM real.

Los checks deterministas proceden de ejecución C# sobre persistencia, API y fuente; sus ámbitos están descritos en cada evidencia.

- `demo-storage-evidence.json`: 17 checks PASS.
- `b03-b04-ingress-evidence.json`: 33 checks PASS.
- `b05-source-evidence.json`: 22 checks PASS.
- `b06-b07-evidence.json`: 25 checks PASS.
- `b08-b12-evidence.json`: 36 checks PASS.

Manifest fija hashes de dataset/corpus/output, regla y proveedor. No se midieron tokens, precios ni rendimiento cloud. El holdout empresarial de 200 casos, dos revisores y evaluación LLM live siguen pendientes.

La muestra pública pequeña puede sobreestimar capacidad. Las pruebas adversariales/fallos son gates separados y no se promedian con intent.
