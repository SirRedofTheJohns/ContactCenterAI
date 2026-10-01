"""HTTP API smoke checks against the running synthetic demo. No browser/login-form automation.
Never exports challenge URLs, CSRF tokens, cookies, or credentials.
"""
from pathlib import Path
import datetime
import http.cookiejar
import json
import urllib.error
import urllib.parse
import urllib.request

ORIGIN = 'http://127.0.0.1:7452'
ISSUER = 'http://localhost:8080/realms/contactcenterai-local'
repo = Path(__file__).resolve().parents[1]

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None

jar = http.cookiejar.CookieJar()
client = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar), NoRedirect())
checks = []

def check(passed, label):
    if not passed:
        raise RuntimeError(label)
    checks.append(label)
    print('PASS: ' + label)

def call(path, method='GET', headers=None):
    request = urllib.request.Request(ORIGIN + path, data=b'' if method == 'POST' else None,
                                     method=method, headers=headers or {})
    try:
        response = client.open(request, timeout=8)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        return response.code, response.headers, response.read()

try:
    code, _, body = call('/health/ready')
    check(code == 200 and json.loads(body)['status'] == 'Ready', 'Running demo and persistent SQLite readiness')
    code, _, body = call('/')
    check(code == 200 and '<title>ContactCenterAI · Demo</title>' in body.decode(), 'Demo entry page served by production host')
    for asset in ['/demo.css', '/demo.js']:
        code, _, body = call(asset)
        check(code == 200 and len(body) > 100, 'Static asset served: ' + asset)
    code, _, _ = call('/v1/session')
    check(code == 401, 'No authenticated session fabricated on page load')
    code, _, _ = call('/v1/conversations/00000000-0000-0000-0000-000000000001')
    check(code == 401, 'Private conversation denied without session')
    code, _, _ = call('/v1/conversations/00000000-0000-0000-0000-000000000001/reservations')
    check(code == 401, 'Product reservation query denied without active session')
    for path in ['/v1/conversations/00000000-0000-0000-0000-000000000001/assistant',
                 '/v1/conversations/00000000-0000-0000-0000-000000000001/actions',
                 '/v1/operations/00000000-0000-0000-0000-000000000001','/v1/agent/conversations']:
        code, _, _ = call(path)
        check(code == 401, 'Unauthenticated access denied: ' + path)
    code, _, body = call('/', headers={'Host': 'remote.invalid'})
    check(code == 403 and json.loads(body)['code'] == 'LOCAL_DEMO_ONLY', 'Untrusted Host denied')
    code, _, body = call('/v1/session/login', 'POST', {'Origin': ORIGIN})
    check(code == 403 and json.loads(body)['code'] == 'CSRF_REJECTED', 'Login POST without CSRF denied')
    code, _, body = call('/v1/session/csrf')
    check(code == 200 and bool(json.loads(body).get('token')), 'CSRF bootstrap issues token')
    csrf = json.loads(body)['token']
    code, _, body = call('/v1/session/login', 'POST', {'Origin': 'http://remote.invalid', 'X-CSRF-TOKEN': csrf})
    check(code == 403 and json.loads(body)['code'] == 'ORIGIN_REJECTED', 'Cross-origin login denied')
    code, headers, _ = call('/v1/session/login', 'POST', {'Origin': ORIGIN, 'X-CSRF-TOKEN': csrf})
    target = urllib.parse.urlsplit(headers.get('Location', ''))
    query = urllib.parse.parse_qs(target.query)
    check(code == 302 and target.netloc == 'localhost:8080' and
          target.path == '/realms/contactcenterai-local/protocol/openid-connect/auth', 'Valid login challenges actual local Keycloak')
    pushed = bool(query.get('request_uri'))
    if pushed:
        check(query.get('client_id') == ['contactcenterai-bff'] and
              query['request_uri'][0].startswith('urn:ietf:params:oauth:request_uri:'),
              'Actual middleware challenges with Keycloak pushed authorization request')
    else:
        check(query.get('response_type') == ['code'] and query.get('code_challenge_method') == ['S256'] and
              len(query.get('code_challenge', [''])[0]) >= 43, 'Actual middleware emits Code and PKCE S256')
        check(query.get('redirect_uri') == [ORIGIN + '/signin-oidc'] and query.get('response_mode') == ['query'],
              'Exact demo callback and GET response mode')
    check('client_secret' not in query and 'access_token' not in query and 'id_token' not in query,
          'Challenge excludes client secrets and tokens')
    report = dict(result='PASS', observedAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  checksPassed=len(checks), checks=checks,
                  scope='Actual running HTTP demo API, static assets, CSRF/Origin/Host and real Keycloak challenge; no login form submitted',
                  challengeTransport='PAR' if pushed else 'Direct',
                  pushedParametersNotInspected=pushed, browserLoginVerified=False, sqlServerNativeVerified=False)
    (repo / 'docs/progress/demo-api-evidence.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(f'Demo API checks: {len(checks)} passed. Browser callback not claimed.')
except Exception as error:
    print('Demo API verification failed: ' + type(error).__name__ + '; no credentials or protocol payloads exported.')
    raise SystemExit(1)
