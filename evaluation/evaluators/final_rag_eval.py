"""Score final candidates without a model judge or changing frozen oracles."""
import argparse,hashlib,json,pathlib,statistics
parser=argparse.ArgumentParser();parser.add_argument('--version',choices=['0.9','0.10'],default='0.10');args=parser.parse_args();version=args.version
repo=pathlib.Path(__file__).resolve().parents[2]
reports=repo/f'evaluation/reports/demo-v{version}'
reports.mkdir(parents=True,exist_ok=True)
def score(dataset,runfile,label,expected_count):
    cases=[json.loads(line) for line in dataset.read_text(encoding='utf-8').splitlines()]
    run=json.loads(runfile.read_text(encoding='utf-8'))
    outputs={x['id']:x for x in run['queries']}
    assert len(cases)==len(outputs)==expected_count
    assert set(outputs)=={x['id'] for x in cases}
    def metrics(rows):
        positive=[x for x in rows if x['goldDocument'] is not None]
        negative=[x for x in rows if x['goldDocument'] is None]
        answered=[x for x in rows if outputs[x['id']]['selected'] is not None]
        correct=lambda x:outputs[x['id']]['selected']==x['goldDocument']
        times=sorted(outputs[x['id']]['elapsedMs'] for x in rows)
        return {'n':len(rows),'positive':len(positive),'outOfScope':len(negative),
                'exact':sum(map(correct,rows))/len(rows),'answeredCorrectly':sum(map(correct,positive))/len(positive),
                'outOfScopeAbstention':sum(map(correct,negative))/len(negative) if negative else None,
                'selectionPrecision':sum(map(correct,answered))/len(answered) if answered else None,
                'recallAt5':sum(any(y['document']==x['goldDocument'] for y in outputs[x['id']]['candidates']) for x in positive)/len(positive),
                'inferenceFailures':sum(outputs[x['id']]['reasonCode']=='INFERENCE_FAILED' for x in rows),
                'outOfScopeInferenceFailures':sum(outputs[x['id']]['reasonCode']=='INFERENCE_FAILED' for x in negative),
                'p50Ms':statistics.median(times),'p95Ms':times[min(len(times)-1,int(len(times)*.95))],'maxMs':max(times)}
    result={'label':label,'scope':'Real BGE-M3 + Qwen closed selection, isolated authorized fixture DB; not end-to-end OIDC/workflow, load, or independent human review.',
            'model':run['model'],'reranker':run['reranker'],'variants':run['variants'],'minimumReviewedCosine':.35,
            'overall':metrics(cases),'es':metrics([x for x in cases if x['language']=='es']),'en':metrics([x for x in cases if x['language']=='en']),
            'independentHumanReview':False,'humanReviewers':0,'datasetSha256':hashlib.sha256(dataset.read_bytes()).hexdigest(),
            'runSha256':hashlib.sha256(runfile.read_bytes()).hexdigest(),
            'failures':[{'id':x['id'],'expected':x['goldDocument'],'actual':outputs[x['id']]['selected'],'reason':outputs[x['id']]['reasonCode']} for x in cases if not correct_row(x,outputs)]}
    result['localNumericGate']='PASS' if result['overall']['exact']>=.95 and result['overall']['outOfScopeAbstention']==1 and result['overall']['outOfScopeInferenceFailures']==0 else 'OPEN'
    if label=='regression':
        result['knownQuestions']=True;result['historicalSplitIsUnseen']=False
        result['dev']=metrics([x for x in cases if x['split']=='dev'])
        result['historicalHoldout']=metrics([x for x in cases if x['split']=='holdout'])
    else:
        manifest=json.loads((repo/'evaluation/datasets/rag-additional-v1.manifest.json').read_text(encoding='utf-8'))
        assert manifest['datasetSha256']==result['datasetSha256']
        result['frozenBeforeInitialCandidateImplementation']=manifest['authoredBeforeV09Implementation'];result['sameAuthor']=True
        result['knownReplay']=version=='0.10';result['v10NamedVenueGuardInformedByV09AdditionalRun']=version=='0.10'
    (reports/(label+'.json')).write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    return result
def correct_row(row,outputs):return outputs[row['id']]['selected']==row['goldDocument']
regression=score(repo/'evaluation/datasets/rag-200-v1.jsonl',repo/f'docs/progress/rag-v{version}-run.json','regression',200)
additional=score(repo/'evaluation/datasets/rag-additional-v1.jsonl',repo/f'docs/progress/rag-v{version}-additional-run.json','additional',100)
lines=[f'# Recuperación v{version}: regresión y conjunto adicional','','IA real, texto final extractivo y citas. No se cambiaron preguntas/oráculos de v0.8. El conjunto adicional se congeló antes del código v0.9, pero lo escribió el mismo autor: no es evaluación humana independiente. En v0.10 se vuelve a medir tras el guard diseñado por fallos de la regresión; conservar también su corrida v0.9.','',
       '| Conjunto | Casos | Exacto | Abstención fuera de alcance | Precisión de selección | p95 ms | Gate local |',
       '|---|---:|---:|---:|---:|---:|---|']
for r in (regression,additional):
    m=r['overall'];lines.append(f"| {r['label']} | {m['n']} | {m['exact']:.1%} | {m['outOfScopeAbstention']:.1%} | {m['selectionPrecision']:.1%} | {m['p95Ms']} | {r['localNumericGate']} |")
lines+=['','Se mide recuperación y selección secuencial en CPU. Excluye clasificación de intención, login, concurrencia y carga. Gate humano/empresarial abierto. Informes anteriores y fallos preservados.','', '## Fallos conservados','']
for r in (regression,additional):
    lines.append(f"### {r['label']}");lines.append('')
    lines.extend([f"- {f['id']}: esperado `{f['expected']}`, observado `{f['actual']}`; {f['reason']}." for f in r['failures']] or ['Ninguno.'])
    lines.append('')
(reports/'report.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print(json.dumps({r['label']:{'overall':r['overall'],'localNumericGate':r['localNumericGate']} for r in (regression,additional)},indent=2))
