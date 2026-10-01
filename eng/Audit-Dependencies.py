"""Locked package inventory, archive integrity and optional official NuGet advisory snapshot."""
import argparse,base64,datetime,gzip,hashlib,json,pathlib,re,urllib.request
repo=pathlib.Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser();parser.add_argument('--LiveAdvisories',action='store_true');args=parser.parse_args()
inventory={}
for folder in ['src','tests','spikes']:
    for lock in (repo/folder).glob('*/packages.lock.json'):
        for group in json.loads(lock.read_text(encoding='utf-8-sig'))['dependencies'].values():
            for name,item in group.items():
                if item['type']=='Project':continue
                key=(name.lower(),item['resolved']);entry=inventory.setdefault(key,{'name':name,'version':item['resolved'],'contentHash':item['contentHash'],'scopes':set()});entry['scopes'].add(folder)
entries=[]
canonical_path=repo/'docs/progress/demo-v0.7-package-content-hashes.json'
canonical=json.loads(canonical_path.read_text(encoding='utf-8')) if canonical_path.exists() else {'packages':[]}
canonical_matches={(x['name'].lower(),x['version']):x['matched'] for x in canonical['packages']}
for (name,version),entry in sorted(inventory.items()):
    archive=repo/'.local/packages'/name/version/(name+'.'+version+'.nupkg')
    rawhash=base64.b64encode(hashlib.sha512(archive.read_bytes()).digest()).decode() if archive.exists() else None
    metadata=archive.with_suffix('.nupkg.sha512')
    # NuGet lock contentHash excludes the signature; whole-archive SHA512 does not.
    entry['scopes']=sorted(entry['scopes']);entry['archiveSha512']=rawhash
    entry['localArchive']='MATCH' if rawhash and metadata.exists() and metadata.read_text().strip()==rawhash and canonical_matches.get((name,version)) else 'NEEDS_CANONICAL_CONTENT_CHECK'
    entries.append(entry)
advisories={};sources=[]
if args.LiveAdvisories:
    def read_json(url):
        with urllib.request.urlopen(url,timeout=30) as response:
            raw=response.read()
            if raw.startswith(b'\x1f\x8b'):raw=gzip.decompress(raw)
            return json.loads(raw)
    url='https://api.nuget.org/v3/vulnerabilities/index.json'
    index=read_json(url)
    sources=[url]
    for batch in index:
        values=read_json(batch['@id'])
        sources.append(batch['@id'])
        for name,rows in values.items():advisories.setdefault(name,[]).extend(rows)
def version(v):
    core,_,pre=v.strip().partition('-');nums=tuple(int(x) for x in core.split('.'))
    return nums+(0,)*(4-len(nums))+(1 if not pre else 0,pre)
def contains(text,v):
    matched=re.fullmatch(r'([\[(])([^,]*),([^\])]*)([\])])',text)
    if not matched:
        return v==text.strip('[]')
    left,minimum,maximum,right=matched.groups();x=version(v)
    return (not minimum or x>=version(minimum) if left=='[' else not minimum or x>version(minimum)) and (not maximum or x<=version(maximum) if right==']' else not maximum or x<version(maximum))
findings=[]
for entry in entries:
    for advisory in advisories.get(entry['name'].lower(),[]):
        if contains(advisory['versions'],entry['version']):findings.append({'name':entry['name'],'version':entry['version'],'severity':advisory['severity'],'url':advisory['url'],'range':advisory['versions']})
proof={'observedAtUtc':datetime.datetime.now(datetime.UTC).isoformat(),'scope':'locked runtime, tests and spike NuGet packages; not OS/Docker/Ollama/SDK vulnerability certification','hashMethod':'whole archive compared to .sha512; signature-excluded lock hash recomputed with SDK PackageArchiveReader; see canonical proof','officialNugetAdvisoriesQueried':args.LiveAdvisories,'sources':sources,'packages':entries,'findings':findings,'integrityPass':all(x['localArchive']=='MATCH' for x in entries)}
(repo/'docs/progress/demo-v0.7-dependencies.json').write_text(json.dumps(proof,indent=2),encoding='utf-8')
sbom={'bomFormat':'CycloneDX','specVersion':'1.6','version':1,'metadata':{'timestamp':proof['observedAtUtc'],'component':{'type':'application','name':'ContactCenterAI local presentation','version':'0.7.0'}},'components':[{'type':'library','name':x['name'],'version':x['version'],'purl':f'pkg:nuget/{x["name"]}@{x["version"]}','hashes':[{'alg':'SHA-512','content':base64.b64decode(x['archiveSha512'] or x['contentHash']).hex()}],'properties':[{'name':'ccai:scope','value':','.join(x['scopes'])},{'name':'ccai:nuget-content-hash','value':x['contentHash']}]} for x in entries]}
(repo/'docs/progress/demo-v0.7-sbom.cdx.json').write_text(json.dumps(sbom,indent=2),encoding='utf-8')
print(json.dumps({'packages':len(entries),'runtime':sum('src' in x['scopes'] for x in entries),'integrityPass':proof['integrityPass'],'matchingAdvisories':findings,'officialFeedQueried':args.LiveAdvisories},indent=2))
