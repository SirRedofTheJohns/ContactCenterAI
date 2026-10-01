"""Configure only synthetic demo users and exact loopback callback; preserve service secrets/volumes."""
from pathlib import Path
import datetime
import json
import sys
import urllib.parse
import urllib.request

repo=Path(__file__).resolve().parents[1]
env_path=repo/'deploy/local/.env'
source=env_path.read_text(encoding='utf-8-sig')
env=dict(line.split('=',1) for line in source.splitlines() if line.strip() and not line.lstrip().startswith('#') and '=' in line)
demo_password='123456Aa!' # Public synthetic fixture, explicitly requested by the user.
def call(method,path,data=None,token=None,form=False):
    body=urllib.parse.urlencode(data).encode() if form else json.dumps(data).encode() if data is not None else None
    headers={'Content-Type':'application/x-www-form-urlencoded' if form else 'application/json'}
    if token: headers['Authorization']='Bearer '+token
    with urllib.request.urlopen(urllib.request.Request('http://localhost:8080'+path,data=body,headers=headers,method=method),timeout=10) as response:
        raw=response.read();return json.loads(raw) if raw else None
try:
    token=call('POST','/realms/master/protocol/openid-connect/token',{'grant_type':'password','client_id':'admin-cli','username':'ccai-local-admin','password':env['CCAI_KEYCLOAK_ADMIN_PASSWORD']},form=True)['access_token']
    realm=json.loads((repo/'deploy/local/keycloak/contactcenterai-local-realm.json').read_text(encoding='utf-8-sig'))
    for expected in realm['users']:
        rows=call('GET','/admin/realms/contactcenterai-local/users?exact=true&username='+urllib.parse.quote(expected['username']),token=token)
        if len(rows)!=1 or rows[0]['id']!=expected['id']: raise RuntimeError('Unexpected synthetic identity binding')
        user=rows[0]
        call('PUT','/admin/realms/contactcenterai-local/users/'+user['id']+'/reset-password',{'type':'password','value':demo_password,'temporary':False},token)
        user['firstName']=user.get('firstName') or 'Demo'
        user['lastName']=user.get('lastName') or expected['username']
        user['email']=expected['username']+'@example.invalid';user['emailVerified']=True;user['requiredActions']=[]
        call('PUT','/admin/realms/contactcenterai-local/users/'+user['id'],user,token)
    rows=call('GET','/admin/realms/contactcenterai-local/clients?clientId=contactcenterai-bff',token=token)
    client=call('GET','/admin/realms/contactcenterai-local/clients/'+rows[0]['id'],token=token)
    callback='http://127.0.0.1:7452/signin-oidc'
    if callback not in client['redirectUris']: client['redirectUris'].append(callback)
    assert not client['publicClient'] and not client['directAccessGrantsEnabled']
    assert not any('*' in uri for uri in client['redirectUris'])
    call('PUT','/admin/realms/contactcenterai-local/clients/'+client['id'],client,token)
    lines=source.splitlines()
    lines=[('CCAI_SYNTHETIC_USER_PASSWORD='+demo_password) if line.startswith('CCAI_SYNTHETIC_USER_PASSWORD=') else line for line in lines]
    env_path.write_text('\n'.join(lines)+'\n',encoding='utf-8')
    fixture_client=next(item for item in realm['clients'] if item['clientId']=='contactcenterai-bff')
    if callback not in fixture_client['redirectUris']:fixture_client['redirectUris'].append(callback)
    for user in realm['users']:
        user['firstName']=user.get('firstName') or 'Demo';user['lastName']=user.get('lastName') or user['username']
        user['email']=user['username']+'@example.invalid';user['emailVerified']=True;user['requiredActions']=[]
    (repo/'deploy/local/keycloak/contactcenterai-local-realm.json').write_text(json.dumps(realm,indent=2)+'\n',encoding='utf-8')
    report=dict(result='PASS',observedAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        syntheticUsersUpdated=len(realm['users']),knownPublicDemoPassword=True,exactCallback=callback,
        serviceSecretsChanged=False,volumesDeleted=False,realLoginVerified=False,
        scope='Demo provisioning; password verification awaits a real OIDC browser login')
    (repo/'docs/progress/demo-setup-evidence.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print('PASS: demo users share the requested public fixture password; exact callback registered; service secrets and volumes preserved.')
except Exception as error:
    print('Demo preparation incomplete: '+type(error).__name__+'; no tokens or service secrets exported.');sys.exit(1)
