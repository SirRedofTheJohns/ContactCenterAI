"""Assess actual C# exports from local inference. Never call a model or read thinking."""
from pathlib import Path
import datetime, hashlib, json, math

root=Path(__file__).resolve().parents[2]
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def wilson(success,n):
    z=1.95996398454;p=success/n;d=1+z*z/n
    c=(p+z*z/(2*n))/d;h=z*math.sqrt(p*(1-p)/n+z*z/(4*n*n))/d
    return [round(c-h,4),round(c+h,4)]
source=root/'docs/progress/local-ai-intent-outputs.json'
outputs=json.loads(source.read_text(encoding='utf-8'))
dataset=root/'evaluation/datasets/demo-intents.jsonl'
gold={v['id']:v for v in map(json.loads,dataset.read_text(encoding='utf-8').splitlines())}
assert len(outputs)==len(gold)==40 and set(v['id'] for v in outputs)==set(gold)
metrics={};failures=[];intent_correct=0;tool_correct=0
for language in ('es','en'):
    subset=[v for v in outputs if v['language']==language];passed=0;intents=0
    for row in subset:
        expected=gold[row['id']];actual=row['actual'] or {}
        intent_ok=actual.get('intent')==expected['intent'] and actual.get('language')==language
        ok=not row['errorCode'] and all(actual.get(k)==expected.get(k) for k in ('intent','language','topic','reservationId'))
        passed+=ok;intents+=intent_ok
        if not ok:failures.append(dict(id=row['id'],expected={k:expected.get(k) for k in ('intent','language','topic','reservationId')},actual=row['actual'],errorCode=row['errorCode']))
    metrics[language]=dict(exact=passed,intent=intents,total=len(subset),exactRate=passed/len(subset),intentRate=intents/len(subset),wilson95=wilson(passed,len(subset)))
times=sorted(v['elapsedMilliseconds'] for v in outputs)
tokens=[v for v in outputs if v['inputTokens'] is not None and v['outputTokens'] is not None]
report=dict(result='PASS_DEV_SAMPLE' if all(v['intentRate']>=.90 and v['exactRate']>=.95 for v in metrics.values()) else 'QUALITY_GATE_NOT_MET',metrics=metrics,failures=failures,
    latency=dict(samples=len(times),p50Milliseconds=times[math.ceil(.5*len(times))-1],p95Milliseconds=times[math.ceil(.95*len(times))-1],maximumMilliseconds=max(times),timeouts=sum(v['errorCode']=='ASSISTANT_DEADLINE' for v in outputs)),
    usage=dict(measuredSamples=len(tokens),inputTokens=sum(v['inputTokens'] for v in tokens),outputTokens=sum(v['outputTokens'] for v in tokens),cloudCalls=0,cloudTokenCharges=None),
    limitations=['Public development set, not an unseen holdout. One developer authored/assessed the cases.','This evaluates intent/proposal classification, not open generation, semantic citation support, dense retrieval, or enterprise workload latency.','Power/resource costs were not measured. Startup was measured separately; 40 sequential requests are not a load benchmark.','Deterministic authorization/workflow safety is tested separately. No production quality, security certification or tenant Genesys validation.'])
report['enterpriseQualityGate']='OPEN: unseen holdout, abstention/support review and workload validation remain required'
report['unknownTopicRouting']=dict(correct=sum((v['actual'] or {}).get('topic')=='unknown' for v in outputs if gold[v['id']]['topic']=='unknown'),total=sum(v['topic']=='unknown' for v in gold.values()))
manifest=json.loads((root/'docs/progress/local-ai-run.json').read_text(encoding='utf-8'))
manifest.update(datasetSha256=digest(dataset),outputSha256=digest(source),providerSourceSha256=digest(root/'src/ContactCenterAI.Infrastructure/OllamaIntentProvider.cs'),evaluatorSha256=digest(Path(__file__)),evaluatedAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),purpose='Public development sample, not unseen holdout',rules='CP-001:v1',retrieval='lexical-topics-v1')
out=root/'evaluation/reports/demo-v0.6';out.mkdir(parents=True,exist_ok=True)
for name,value in [('metrics.json',report),('manifest.json',manifest)]: (out/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
lines=['# Evaluación de inferencia local real v0.6','',f"Resultado: **{report['result']}**. Modelo local **{manifest['model']}**, digest fijado. No es el simulador.",'','| Idioma | Intent correcto | Propuesta exacta | Wilson 95% (exacta) |','|---|---:|---:|---|']
for language,m in metrics.items():lines.append(f"| {language} | {m['intent']}/{m['total']} | {m['exact']}/{m['total']} | {m['wilson95']} |")
latency=report['latency'];usage=report['usage']
lines+=['',f"40 consultas secuenciales: p50 {latency['p50Milliseconds']} ms, p95 {latency['p95Milliseconds']} ms, máximo {latency['maximumMilliseconds']} ms; timeouts {latency['timeouts']}. Deadline por petición 8 s. No es p95 de todo el chat ni benchmark bajo carga.",f"Uso registrado: {usage['inputTokens']} tokens de entrada, {usage['outputTokens']} de salida en {usage['measuredSamples']} respuestas medidas. Cero llamadas cloud. Energía y costo de equipo no medidos.",'','## Errores registrados sin ocultarlos','']
if failures:
    for f in failures:lines.append('- '+f['id']+': '+json.dumps(dict(actual=f['actual'],errorCode=f['errorCode']),ensure_ascii=False))
else:lines.append('Ningún error de propuesta en estos 40 casos públicos.')
lines+=['','El prompt quedó fijado antes de la corrida; estos resultados no se convirtieron en un holdout. Los escenarios de control son públicos y pocos; no prueban generalización. La recuperación sigue siendo por temas y la respuesta factual es extractiva desde corpus gobernado. El modelo no puede confirmar/cancelar por chat.','',"El gate empresarial G3 sigue OPEN. Los dos errores son preguntas sobre precios de vuelos clasificadas como payments, en lugar de unknown. La plantilla de pagos no inventa precios, pero el routing fuera de alcance requiere mejora y revisión de abstención antes de una afirmación de calidad general.",'',*('- '+s for s in report['limitations'])]
(out/'report.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print(json.dumps(dict(result=report['result'],metrics=metrics,latency=latency,failures=[v['id'] for v in failures]),ensure_ascii=False))
