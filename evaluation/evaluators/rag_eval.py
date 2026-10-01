"""Offline scorer for the frozen bilingual corpus. No judge or hidden reasoning."""
import argparse,hashlib,json,pathlib,statistics
parser=argparse.ArgumentParser();parser.add_argument('--version',choices=['0.7','0.8'],default='0.7');args=parser.parse_args();version=args.version
repo=pathlib.Path(__file__).resolve().parents[2]
dataset=repo/'evaluation/datasets/rag-200-v1.jsonl'
runfile=repo/f'docs/progress/rag-v{version}-run.json'
cases=[json.loads(line) for line in dataset.read_text(encoding='utf-8').splitlines()]
run=json.loads(runfile.read_text(encoding='utf-8'))
outputs={row['id']:row for row in run['queries']}
assert len(cases)==len(outputs)==200 and len({x['id'] for x in cases})==200
assert sum(c['split']=='dev' for c in cases)==60
assert sum(c['split']=='holdout' for c in cases)==140
def metrics(rows):
    positive=[c for c in rows if c['goldDocument'] is not None];negative=[c for c in rows if c['goldDocument'] is None]
    correct=lambda c:outputs[c['id']]['selected']==c['goldDocument']
    times=sorted(outputs[c['id']]['elapsedMs'] for c in rows)
    return {'n':len(rows),'positive':len(positive),'outOfScope':len(negative),'exact':sum(map(correct,rows))/len(rows),
            'recallAt5':sum(any(x['document']==c['goldDocument'] for x in outputs[c['id']]['candidates']) for c in positive)/len(positive),
            'answeredCorrectly':sum(map(correct,positive))/len(positive),'outOfScopeAbstention':sum(map(correct,negative))/len(negative),
            'inferenceFailures':sum(outputs[c['id']]['reasonCode']=='INFERENCE_FAILED' for c in rows),
            'p50Ms':statistics.median(times),'p95Ms':times[min(len(times)-1,int(len(times)*.95))],'maxMs':max(times)}
result={'scope':'real BGE-M3 retrieval, not end-to-end intent/workflow or independent human evaluation',
        'model':run['model'],'variants':run['variants'],'indexingMs':run['indexingMs'],
        'threshold':.55,'margin':.015 if version=='0.7' else None,'thresholdTunedAfterHoldout':False,'rankingChangedAfterInspectingPreviousHoldout':version=='0.8','unseenHoldout':False,
        'overall':metrics(cases),'dev':metrics([c for c in cases if c['split']=='dev']),
        'holdout':metrics([c for c in cases if c['split']=='holdout']),
        'es':metrics([c for c in cases if c['language']=='es']),'en':metrics([c for c in cases if c['language']=='en'])}
result['localQualityGate']='PASS' if result['holdout']['exact']>=.95 and result['holdout']['outOfScopeAbstention']==1 else 'OPEN'
result['failures']=[{'id':c['id'],'split':c['split'],'expected':c['goldDocument'],'actual':outputs[c['id']]['selected'],'reasonCode':outputs[c['id']]['reasonCode']} for c in cases if not outputs[c['id']]['selected']==c['goldDocument']]
target=repo/f'evaluation/reports/demo-v{version}';target.mkdir(parents=True,exist_ok=True)
(target/'metrics.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
manifest={'datasetSha256':hashlib.sha256(dataset.read_bytes()).hexdigest(),'runSha256':hashlib.sha256(runfile.read_bytes()).hexdigest(),
          'corpusSha256':hashlib.sha256((repo/'evaluation/datasets/rag-corpus-v1.json').read_bytes()).hexdigest(),'split':'60 dev /140 holdout; paired ES/EN; authored before implementation','independent':False,'humanReviewers':0}
(target/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
lines=[f'# Evaluación de recuperación local v{version}','','200 casos, 100 pares ES/EN. Corpus sintético de 40 documentos lógicos y 80 variantes. Inferencia real BGE-M3 y búsqueda coseno exacta C#. '+('Umbral/margen fijados antes de ejecutar; sin ajuste posterior al holdout. Los autores conocen los casos: split de calibración separado, no evaluación ciega.' if version=='0.7' else 'Reranker Qwen añadido después de inspeccionar los errores v0.7. Esta corrida es regresión sobre casos conocidos; los nombres dev/holdout conservan el split histórico y no demuestran generalización. No se cambiaron las preguntas.'),'',
       '| Split | N | Acierto exacto | Recall@5 positivos | Abstención fuera de alcance | p95 ms |','|---|---:|---:|---:|---:|---:|']
for name in ['overall','dev','holdout','es','en']:
    m=result[name];lines.append(f'| {name} | {m["n"]} | {m["exact"]:.1%} | {m["recallAt5"]:.1%} | {m["outOfScopeAbstention"]:.1%} | {m["p95Ms"]} |')
lines+=['',f'Gate numérico local: **{result["localQualityGate"]}** (split histórico holdout ≥95% exacto y 100% abstención fuera de alcance). No cierra el gate de calidad empresarial/independiente.',
        '','Se mide recuperación aislada: no latencia total de chat, concurrencia, disponibilidad HA, transferencia Genesys ni calidad humana. No convertir abstención en acierto para preguntas que sí tienen documento relevante. El informe conserva todos los fallos; no se cambian preguntas o resultados para aprobar. Coste local sin factura cloud, pero no se midió consumo eléctrico.','', '## Fallos','']
lines += [f'- {x["id"]}: esperado `{x["expected"]}`, observado `{x["actual"]}`; {x["reasonCode"]}.' for x in result['failures']] or ['Ninguno.']
(target/'report.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print(json.dumps({k:result[k] for k in ['overall','holdout','localQualityGate']},indent=2))
