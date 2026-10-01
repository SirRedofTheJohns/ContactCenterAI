"""Offline local SQL data tooling. Never loaded by the C# runtime.

Use --tooling-path for an isolated installation of requirements-data.txt.
Never prints passwords, connection strings, transcripts or arbitrary SQL errors.
"""
from pathlib import Path
import argparse
import datetime
import json
import logging
import re
import secrets
import sys

parser = argparse.ArgumentParser()
parser.add_argument('--tooling-path', type=Path)
args = parser.parse_args()
if args.tooling_path:
    sys.path.insert(0, str(args.tooling_path.resolve()))
import pytds
import pytds.tls
from OpenSSL import SSL, crypto

logging.disable(logging.CRITICAL)
repo = Path(__file__).resolve().parents[1]
local = repo / '.local'
local.mkdir(exist_ok=True)
env_path = repo / 'deploy/local/.env'
environment = dict(line.split('=', 1) for line in env_path.read_text(encoding='utf-8-sig').splitlines()
                   if line.strip() and not line.lstrip().startswith('#') and '=' in line)
app_password = environment.get('CCAI_OPERATIONAL_DB_PASSWORD')
if not app_password:
    app_password = secrets.token_urlsafe(36)
    with env_path.open('a', encoding='utf-8') as output:
        output.write('\nCCAI_OPERATIONAL_DB_PASSWORD=' + app_password + '\n')
if not re.fullmatch(r'[A-Za-z0-9_-]{40,128}', app_password):
    raise SystemExit('Local app credential requires a generated URL-safe value; no value exported.')
public_cert = local / 'sql-server-public.pem'
original_context = pytds.tls.create_context

def bootstrap_context(_cafile):
    # Trust the self-signed leaf only on the fixed loopback SQL endpoint, matching
    # ADR-011's local TrustServerCertificate baseline. Afterwards pin this leaf.
    context = SSL.Context(SSL.TLS_CLIENT_METHOD)
    def verify(_connection, certificate, error, depth, verified):
        if depth == 0:
            public_cert.write_bytes(crypto.dump_certificate(crypto.FILETYPE_PEM, certificate))
        return bool(verified) or (depth == 0 and error == 18)
    context.set_verify(SSL.VERIFY_PEER, verify)
    return context

def connect(database, app=False):
    return pytds.connect(server='127.0.0.1', port=14333, database=database,
        user='ccai_app' if app else 'sa', password=app_password if app else environment['CCAI_SQL_SA_PASSWORD'],
        cafile=str(public_cert), validate_host=False, enc_login_only=False,
        login_timeout=8, timeout=8, autocommit=True, disable_connect_retry=True)

checks = []
def check(condition, label):
    if not condition:
        raise RuntimeError('CHECK_FAILED: ' + label)
    checks.append(label)
    print('PASS: ' + label)

try:
    if not public_cert.exists():
        pytds.tls.create_context = bootstrap_context
        with connect('master') as connection:
            with connection.cursor() as cursor:
                cursor.execute('SELECT encrypt_option FROM sys.dm_exec_connections WHERE session_id=@@SPID')
                check(cursor.fetchone()[0] == 'TRUE', 'SQL data tooling connection fully encrypted')
        pytds.tls.create_context = original_context
    with connect('master') as connection:
        with connection.cursor() as cursor:
            cursor.execute("IF DB_ID('ContactCenterAI_Operations') IS NULL CREATE DATABASE ContactCenterAI_Operations")
            # Password is generated locally with a validated alphabet. It is never an external input.
            cursor.execute("IF NOT EXISTS(SELECT 1 FROM sys.server_principals WHERE name='ccai_app') "
                           "CREATE LOGIN ccai_app WITH PASSWORD='" + app_password + "', CHECK_POLICY=ON")
    with connect('ContactCenterAI_Operations') as connection:
        with connection.cursor() as cursor:
            cursor.execute('SELECT encrypt_option FROM sys.dm_exec_connections WHERE session_id=@@SPID')
            check(cursor.fetchone()[0] == 'TRUE', 'Pinned local SQL tooling connection fully encrypted')
            cursor.execute((repo / 'deploy/local/operational-v001.sql').read_text(encoding='utf-8'))
            while cursor.nextset():
                pass
            cursor.execute("IF USER_ID('ccai_app') IS NULL CREATE USER ccai_app FOR LOGIN ccai_app; "
                           "REVOKE SELECT,INSERT,UPDATE ON SCHEMA::dbo FROM ccai_app")
            for table in ['SchemaVersion','Principal','UserSession','Conversation','Assignment','Idempotency','Message','Inbox','Audit']:
                cursor.execute('GRANT SELECT ON dbo.' + table + ' TO ccai_app')
            for table in ['UserSession','Conversation','Idempotency','Message','Inbox','Audit']:
                cursor.execute('GRANT INSERT ON dbo.' + table + ' TO ccai_app')
            for table in ['UserSession','Conversation']:
                cursor.execute('GRANT UPDATE ON dbo.' + table + ' TO ccai_app')
            seed = json.loads((repo / 'deploy/local/member-bindings.json').read_text(encoding='utf-8'))
            for number, roles in [(1,1),(2,1),(3,2),(4,2),(5,4),(6,8),(7,16),(8,32)]:
                subject = f'10000000-0000-0000-0000-{number:012}'
                member = next((row['memberRef'] for row in seed['bindings'] if row['subject'] == subject), None)
                cursor.execute('IF NOT EXISTS(SELECT 1 FROM dbo.Principal WHERE Issuer=%s AND Subject=%s) '
                               'INSERT dbo.Principal VALUES(%s,%s,%s,%s,%s,%s,1)',
                    (seed['issuer'], subject, subject, 'tenant-demo', seed['issuer'], subject, member, roles))
            cursor.execute('SELECT COUNT(*) FROM dbo.Principal WHERE Active=1')
            check(cursor.fetchone()[0] == 8, 'Eight synthetic server bindings seeded without passwords or tokens')
            cursor.execute('SELECT Version FROM dbo.SchemaVersion')
            check(cursor.fetchone()[0] == 1, 'Operational schema v001 applied')
            cursor.execute("SELECT COUNT(*) FROM sys.tables WHERE name IN ('Reservation','Member','Payment')")
            check(cursor.fetchone()[0] == 0, 'Operational database contains no authoritative source reservation copy')
    with connect('ContactCenterAI_Operations', app=True) as connection:
        with connection.cursor() as cursor:
            # A non-sysadmin sees only its own connection row on SQL 2022.
            cursor.execute("SELECT HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CREATE TABLE'), IS_SRVROLEMEMBER('sysadmin')")
            row = cursor.fetchone()
            check(row[0] == 0 and row[1] == 0, 'Application login has no DDL or sysadmin permission')
            cursor.execute("SELECT HAS_PERMS_BY_NAME('dbo.Message','OBJECT','INSERT'), HAS_PERMS_BY_NAME('dbo.Message','OBJECT','DELETE')")
            row = cursor.fetchone()
            check(row[0] == 1 and row[1] == 0, 'Application can append messages and cannot delete them')
            cursor.execute("SELECT HAS_PERMS_BY_NAME('dbo.Principal','OBJECT','UPDATE'), HAS_PERMS_BY_NAME('dbo.Assignment','OBJECT','INSERT'), HAS_PERMS_BY_NAME('dbo.Audit','OBJECT','UPDATE')")
            check(cursor.fetchone() == (0,0,0), 'Application cannot rewrite bindings, assignments or audit')
            cursor.execute('SELECT COUNT(*) FROM dbo.SchemaVersion WHERE Version=1')
            check(cursor.fetchone()[0] == 1, 'Restricted application login reads operational schema')
    report = dict(scope='Offline Python data provisioning over encrypted TDS; not C# driver or product OIDC proof',
                  observedAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(), result='PASS',
                  checks=checks, nativeBackendLiveVerified=False, fullSessionLiveVerified=False)
    (repo / 'docs/progress/b04-sql-provisioning.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
except Exception as error:
    print('Provisioning incomplete: ' + type(error).__name__ + '; no credentials or SQL error body exported.')
    sys.exit(1)
finally:
    pytds.tls.create_context = original_context
