"""Offline provisioning: preserve the diagnostic client and add product callback.
Uses synthetic local admin credentials only; does not implement password login in BFF.
"""
from pathlib import Path
import datetime
import json
import sys
import urllib.parse
import urllib.request

repo = Path(__file__).resolve().parents[1]
env = dict(line.split('=',1) for line in (repo/'deploy/local/.env').read_text(encoding='utf-8-sig').splitlines()
    if line.strip() and not line.lstrip().startswith('#') and '=' in line)
def call(method, path, payload=None, token=None, form=False):
    data = urllib.parse.urlencode(payload).encode() if form else json.dumps(payload).encode() if payload is not None else None
    headers = {'Content-Type':'application/x-www-form-urlencoded' if form else 'application/json'}
    if token: headers['Authorization']='Bearer '+token
    request = urllib.request.Request('http://localhost:8080'+path,data=data,headers=headers,method=method)
    with urllib.request.urlopen(request,timeout=10) as response:
        body=response.read(); return json.loads(body) if body else None
try:
    token=call('POST','/realms/master/protocol/openid-connect/token',
        {'grant_type':'password','client_id':'admin-cli','username':'ccai-local-admin','password':env['CCAI_KEYCLOAK_ADMIN_PASSWORD']},form=True)['access_token']
    client=call('GET','/admin/realms/contactcenterai-local/clients?clientId=contactcenterai-bff',token=token)[0]
    existing=call('GET','/admin/realms/contactcenterai-local/clients/'+client['id'],token=token)
    if existing.get('publicClient') or existing.get('directAccessGrantsEnabled') or not existing.get('standardFlowEnabled'):
        raise RuntimeError('Unexpected BFF client authentication configuration')
    redirects=existing.setdefault('redirectUris',[])
    for uri in ['https://localhost:7443/signin-oidc','https://localhost:7451/signin-oidc']:
        if uri not in redirects: redirects.append(uri)
    existing.setdefault('attributes',{})['pkce.code.challenge.method']='S256'
    call('PUT','/admin/realms/contactcenterai-local/clients/'+client['id'],existing,token)
    verified=call('GET','/admin/realms/contactcenterai-local/clients/'+client['id'],token=token)
    assert 'https://localhost:7451/signin-oidc' in verified['redirectUris']
    assert not any('*' in uri for uri in verified['redirectUris'])
    report=dict(result='PASS',observedAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        exactProductCallback='https://localhost:7451/signin-oidc',diagnosticCallbackPreserved=True,
        confidentialClient=True,directAccessGrant=False,pkce='S256',wildcardRedirect=False,
        scope='Local IdP client provisioning only; not product login roundtrip')
    (repo/'docs/progress/b03-identity-provisioning.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print('PASS: exact product callback registered, diagnostic callback preserved, Code+PKCE only; no credentials exported.')
except Exception as error:
    print('Identity provisioning incomplete: '+type(error).__name__+'; response bodies and credentials not exported.');sys.exit(1)
