"""Offline evaluator over outputs actually exported by the C# simulated provider.
No model API calls; no fabricated runtime measurements; no provider credentials.
"""
from pathlib import Path
import datetime
import hashlib
import json
import math
import sys

root=Path(__file__).resolve().parents[2]
def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def wilson(success,total):
    if not total:return None
    z=1.95996398454;p=success/total;d=1+z*z/total
    center=(p+z*z/(2*total))/d
    half=z*math.sqrt(p*(1-p)/total+z*z/(4*total*total))/d
    return [round(center-half,4),round(center+half,4)]

outputs=json.loads((root/'docs/progress/demo-intent-outputs.json').read_text(encoding='utf-8'))
inputs=[json.loads(line) for line in (root/'evaluation/datasets/demo-intents.jsonl').read_text(encoding='utf-8').splitlines()]
assert len(inputs)==len(outputs)==40 and len({row['id'] for row in outputs})==40
by_id={row['id']:row for row in inputs}
failures=[];metrics={}
for language in ('es','en'):
    subset=[row for row in outputs if row['language']==language]
    success=0
    for row in subset:
        expected=by_id[row['id']]
        correct=all(row['actual'].get(key)==expected.get(key) for key in ('intent','topic','reservationId')) and row['actual'].get('language')==language
        success+=correct
        if not correct:failures.append(row['id'])
    metrics[language]=dict(success=success,total=len(subset),rate=success/len(subset),wilson95=wilson(success,len(subset)))
pairs={row['pairId'] for row in outputs}
assert len(pairs)==20
parity=0
for pair in pairs:
    rows=[row for row in outputs if row['pairId']==pair]
    assert {row['language'] for row in rows}=={'es','en'}
    parity+=all(rows[0]['actual'].get(key)==rows[1]['actual'].get(key) for key in ('intent','topic','reservationId'))
safety=[]
for filename in ['demo-storage-evidence.json','b03-b04-ingress-evidence.json','b05-source-evidence.json','b06-b07-evidence.json','b08-b12-evidence.json']:
    report=json.loads((root/'docs/progress'/filename).read_text(encoding='utf-8'))
    assert report.get('result',report.get('state'))=='PASS', filename
    safety.append(dict(artifact=filename,passed=report['checksPassed'],hash=digest(root/'docs/progress'/filename)))
manifest=dict(observedAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),profile='demo-v0.5',provider='simulated-intent-v1',
              rule='CP-001:v1',retrieval='lexical-topics-v1',toolSchema='closed-intent-v1',runtime='C#/.NET10',evaluation='Python offline',
              datasetSha256=digest(root/'evaluation/datasets/demo-intents.jsonl'),corpusSha256=digest(root/'evaluation/datasets/demo-corpus.json'),
              outputSha256=digest(root/'docs/progress/demo-intent-outputs.json'),datasetPurpose='Public development/control cases; not unseen holdout',
              liveLlmTested=False,denseRetrievalTested=False,genesysTenantTested=False,remoteCiRunObserved=False)
report=dict(result='PASS' if not failures else 'FAIL',metrics=metrics,pairedSemantics=dict(success=parity,total=20),failures=failures,deterministicChecks=safety,
            limitations=['Small public dataset maintained by one developer.','Scores measure a deterministic intent simulator, not LLM linguistic quality.',
                         'No claims of cloud latency, token cost, production safety certification or enterprise 200-case holdout.'])
out=root/'evaluation/reports/demo-v0.5';out.mkdir(parents=True,exist_ok=True)
for name,data in [('manifest.json',manifest),('metrics.json',report)]:
    (out/name).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
lines=['# Evaluación local v0.5','',f"Resultado: **{report['result']}**. Proveedor **simulado**, corpus sintético, dataset público de desarrollo.",'',
       '| Idioma | Intent y argumentos correctos | Wilson 95% |','|---|---:|---|']
for language,metric in metrics.items():lines.append(f"| {language} | {metric['success']}/{metric['total']} | {metric['wilson95']} |")
lines.extend(['',f'Paridad semántica: {parity}/20 pares. No hay inferencia de un LLM real.','',
              'Los checks deterministas proceden de ejecución C# sobre persistencia, API y fuente; sus ámbitos están descritos en cada evidencia.',''])
for suite in safety:lines.append(f"- `{suite['artifact']}`: {suite['passed']} checks PASS.")
lines.extend(['','Manifest fija hashes de dataset/corpus/output, regla y proveedor. No se midieron tokens, precios ni rendimiento cloud. El holdout empresarial de 200 casos, dos revisores y evaluación LLM live siguen pendientes.','',
              'La muestra pública pequeña puede sobreestimar capacidad. Las pruebas adversariales/fallos son gates separados y no se promedian con intent.'])
(out/'report.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print(json.dumps(dict(result=report['result'],cases=40,pairs=20,failures=failures,liveLlm=False)))
sys.exit(bool(failures))
