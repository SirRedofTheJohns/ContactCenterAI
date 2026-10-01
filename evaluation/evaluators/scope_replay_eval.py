"""Score explicitly composed C# scope checks and unchanged measured outputs."""
import hashlib
import json
from pathlib import Path

repo = Path(__file__).resolve().parents[2]
target = repo / 'evaluation/reports/demo-v0.10/scope-replay'
target.mkdir(parents=True, exist_ok=True)
reports = []
for label, dataset_name, source_name in (
    ('regression', 'rag-200-v1.jsonl', 'rag-v0.10-run.json'),
    ('additional', 'rag-additional-v1.jsonl', 'rag-v0.10-additional-run.json'),
):
    dataset = repo / 'evaluation/datasets' / dataset_name
    original = repo / 'docs/progress' / source_name
    replay_path = repo / f'docs/progress/rag-v0.10-{label}-scope-replay.json'
    replay = json.loads(replay_path.read_text(encoding='utf-8'))
    cases = [json.loads(line) for line in dataset.read_text(encoding='utf-8').splitlines()]
    rows = {row['id']: row for row in replay['queries']}
    original_rows = {row['id']: row for row in json.loads(original.read_text(encoding='utf-8'))['queries']}
    assert len(rows) == len(cases) == (200 if label == 'regression' else 100)
    assert set(rows) == set(original_rows) == {row['id'] for row in cases}
    assert replay['sourceSha256'] == hashlib.sha256(original.read_bytes()).hexdigest()
    assert replay['datasetSha256'] == hashlib.sha256(dataset.read_bytes()).hexdigest()
    assert replay['newModelCalls'] == 0
    for case in cases:
        row = rows[case['id']]
        if row['measurementOrigin'] == 'CURRENT_CSHARP_SCOPE_ZERO_INFERENCE':
            assert case['goldDocument'] is None and row['selected'] is None
            assert row['reasonCode'] == 'OUT_OF_SCOPE' and row['candidates'] == []
        else:
            assert row['measurementOrigin'] == 'UNCHANGED_PATH_REUSED_REAL_MODEL_OUTPUT'
            assert {k: v for k, v in row.items() if k != 'measurementOrigin'} == original_rows[case['id']]
    negatives = [c for c in cases if c['goldDocument'] is None]
    answered = [c for c in cases if rows[c['id']]['selected'] is not None]
    correct = lambda c: rows[c['id']]['selected'] == c['goldDocument']
    exact = sum(map(correct, cases)) / len(cases)
    abstention = sum(map(correct, negatives)) / len(negatives)
    failed_negative = sum(rows[c['id']]['reasonCode'] == 'INFERENCE_FAILED' for c in negatives)
    result = {
        'label': label, 'scope': replay['scope'], 'n': len(cases),
        'exact': exact, 'outOfScopeAbstention': abstention,
        'selectionPrecision': sum(map(correct, answered)) / len(answered),
        'guardedQueries': replay['guardedQueries'], 'reusedOutputs': len(cases) - replay['guardedQueries'],
        'correctedSelections': replay['correctedSelections'], 'newModelCalls': 0,
        'outOfScopeInferenceFailures': failed_negative,
        'latencyMeasuredForCurrentComposedRun': False,
        'independentHumanReview': False, 'knownReplay': True,
        'sourceSha256': replay['sourceSha256'], 'datasetSha256': replay['datasetSha256'],
        'replaySha256': hashlib.sha256(replay_path.read_bytes()).hexdigest(),
        'localNumericGate': 'PASS' if exact >= .95 and abstention == 1 and failed_negative == 0 else 'OPEN',
        'failures': [{'id': c['id'], 'expected': c['goldDocument'], 'actual': rows[c['id']]['selected'],
                      'reason': rows[c['id']]['reasonCode']} for c in cases if not correct(c)],
    }
    (target / (label + '.json')).write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    reports.append(result)

lines = ['# Cierre local v0.10: replay compuesto', '',
         'La corrida real inicial permanece intacta en el directorio superior. Solo cambió el reconocimiento determinista de conjugaciones de devolver dinero. Cada consulta que el filtro reconoce pasó de nuevo por el repositorio C# autorizado, con cero llamadas de inferencia. Para los caminos sin cambio se reutilizan exactamente sus outputs medidos. No son 300 nuevas inferencias ni un nuevo benchmark de latencia. Casos conocidos del mismo autor; revisión humana independiente abierta.', '',
         '| Conjunto | Casos | Exacto compuesto | Abstención fuera de scope | Checks actuales del filtro | Outputs reutilizados | Gate local |',
         '|---|---:|---:|---:|---:|---:|---|']
for r in reports:
    lines.append(f"| {r['label']} | {r['n']} | {r['exact']:.1%} | {r['outOfScopeAbstention']:.1%} | {r['guardedQueries']} | {r['reusedOutputs']} | {r['localNumericGate']} |")
lines += ['', '## Errores conservados', '']
for r in reports:
    lines.extend([f"### {r['label']}", ''])
    lines.extend([f"- {f['id']}: esperado `{f['expected']}`, observado `{f['actual']}`; {f['reason']}." for f in r['failures']] or ['Ninguno.'])
    lines.append('')
(target / 'report.md').write_text('\n'.join(lines) + '\n', encoding='utf-8')
print(json.dumps({r['label']: {k: r[k] for k in ('n', 'exact', 'outOfScopeAbstention', 'guardedQueries', 'reusedOutputs', 'correctedSelections', 'localNumericGate')} for r in reports}, indent=2))
raise SystemExit(any(r['localNumericGate'] != 'PASS' for r in reports))
