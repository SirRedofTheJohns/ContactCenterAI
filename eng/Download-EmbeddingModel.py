"""Explicit vendor download of the pinned BGE-M3 distribution; no inference data is sent."""
import hashlib,json,pathlib,time,urllib.request
repo=pathlib.Path(__file__).resolve().parents[1];root=repo/'.local/embedding'
base='https://registry.ollama.ai/v2/library/bge-m3'
expected='7907646426070047a77226ac3e684fbbe8410524f7b4a74d02837e43f2146bab'
with urllib.request.urlopen(base+'/manifests/latest',timeout=30) as response:raw=response.read()
if hashlib.sha256(raw).hexdigest()!=expected:raise RuntimeError('MODEL_MANIFEST_CHANGED: approve a new baseline before changing the pin')
manifest=json.loads(raw);blobs=root/'models/blobs';blobs.mkdir(parents=True,exist_ok=True)
def sha_file(path):
    with path.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
for layer in [manifest['config'],*manifest['layers']]:
    sha=layer['digest'].split(':')[1];target=blobs/('sha256-'+sha)
    if target.exists() and target.stat().st_size==layer['size'] and sha_file(target)==sha:continue
    partial=target.with_suffix('.partial');h=hashlib.sha256();count=0;last=time.monotonic()
    with urllib.request.urlopen(base+'/blobs/'+layer['digest'],timeout=60) as response,partial.open('wb') as stream:
        while chunk:=response.read(1024*1024):
            stream.write(chunk);h.update(chunk);count+=len(chunk)
            if time.monotonic()-last>15:print(f'Descarga {count//1048576} / {layer["size"]//1048576} MiB',flush=True);last=time.monotonic()
    if h.hexdigest()!=sha or count!=layer['size']:raise RuntimeError('MODEL_BLOB_INTEGRITY_FAILED')
    partial.replace(target)
path=root/'models/manifests/registry.ollama.ai/library/bge-m3/latest';path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(raw)
(root/'provenance.json').write_text(json.dumps({'model':'bge-m3:latest','digest':expected,'registry':base,'manifest':manifest},indent=2),encoding='utf-8')
print('Modelo bilingüe verificado. No se modificaron los modelos del usuario.')
