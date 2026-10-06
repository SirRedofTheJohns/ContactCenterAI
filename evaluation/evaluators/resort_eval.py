"""Offline declared examples, using the real compiled C# parser; not a holdout or LLM eval."""
import argparse
import json
from pathlib import Path
import subprocess

def main():
    cli=argparse.ArgumentParser()
    cli.add_argument('--dotnet',required=True)
    cli.add_argument('--assembly',required=True)
    opts=cli.parse_args()
    repo=Path(__file__).resolve().parents[2]
    data=repo/'evaluation/datasets/resort-intents-v0.12.jsonl'
    cases=[json.loads(s) for s in data.read_text(encoding='utf-8').splitlines() if s.strip()]
    result=subprocess.run([opts.dotnet,opts.assembly,str(repo),'--parse',str(data)],check=True,capture_output=True,text=True)
    actual={c['id']:c for c in json.loads(result.stdout)}
    errors=[]
    for c in cases:
        for key,expected in c['expected'].items():
            value=actual[c['id']]
            for part in key.split('.'):
                value=value.get(part) if isinstance(value,dict) else None
            if value!=expected: errors.append({'id':c['id'],'field':key,'expected':expected,'actual':value})
    report={'result':'FAIL' if errors else 'PASS','cases':len(cases),'failures':errors,
        'scope':'Declared ES/EN parser and scope examples; real C# parser invoked by Python offline. No live LLM, no independent holdout, no provider traffic.'}
    target=repo/'evaluation/reports/resort-v0.12-parser.json'
    target.parent.mkdir(parents=True,exist_ok=True)
    target.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(report,ensure_ascii=True))
    return bool(errors)

if __name__=='__main__':raise SystemExit(main())
