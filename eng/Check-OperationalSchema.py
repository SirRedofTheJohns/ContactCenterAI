"""Verify actual SQL constraints in transactions that always roll back.

No runtime repository emulation: these are physical schema/privilege checks.
"""
from pathlib import Path
import argparse
import datetime
import json
import logging
import sys
import uuid

parser = argparse.ArgumentParser()
parser.add_argument('--tooling-path', type=Path)
args = parser.parse_args()
if args.tooling_path: sys.path.insert(0, str(args.tooling_path.resolve()))
import pytds
logging.disable(logging.CRITICAL)
repo = Path(__file__).resolve().parents[1]
environment = dict(line.split('=',1) for line in (repo/'deploy/local/.env').read_text(encoding='utf-8-sig').splitlines()
    if line.strip() and not line.lstrip().startswith('#') and '=' in line)
checks = []
def connect():
    return pytds.connect(server='127.0.0.1', port=14333, database='ContactCenterAI_Operations',
        user='sa', password=environment['CCAI_SQL_SA_PASSWORD'],
        cafile=str(repo/'.local/sql-server-public.pem'), validate_host=False, enc_login_only=False,
        login_timeout=8,timeout=8,autocommit=True,disable_connect_retry=True)
def expect_rejected(label, prepare, violating):
    with connect() as connection:
        with connection.cursor() as cursor:
            cursor.execute('SET XACT_ABORT OFF; BEGIN TRANSACTION')
            try:
                prepare(cursor)
                try:
                    violating(cursor)
                except pytds.IntegrityError:
                    checks.append(label); print('PASS: '+label)
                else:
                    raise RuntimeError('Constraint did not reject: '+label)
            finally:
                cursor.execute('IF @@TRANCOUNT>0 ROLLBACK')
def conversation(cursor, id, tenant='tenant-demo', principal='10000000-0000-0000-0000-000000000001'):
    cursor.execute("INSERT dbo.Conversation VALUES(%s,%s,%s,NULL,'es',1,1,'AI',SYSUTCDATETIME())",(id,tenant,principal))
def message(cursor, conversation_id, client_id):
    message_id, turn_id = str(uuid.uuid4()), str(uuid.uuid4())
    cursor.execute("INSERT dbo.Message VALUES(%s,%s,%s,N'synthetic sanitized fixture',%s,%s,2,SYSUTCDATETIME())",(message_id,conversation_id,client_id,'A'*64,turn_id))
    return message_id,turn_id
try:
    with connect() as connection:
        with connection.cursor() as cursor:
            cursor.execute('SELECT encrypt_option FROM sys.dm_exec_connections WHERE session_id=@@SPID')
            if cursor.fetchone()[0] != 'TRUE': raise RuntimeError('Full encryption required')
    cid, client_id = str(uuid.uuid4()),str(uuid.uuid4())
    expect_rejected('SQL composite foreign key rejects cross-tenant principal', lambda cursor:None,
        lambda cursor:conversation(cursor,cid,tenant='another-tenant'))
    expect_rejected('SQL ownership constraint rejects a conversation without owner', lambda cursor:None,
        lambda cursor:cursor.execute("INSERT dbo.Conversation VALUES(%s,'tenant-demo',NULL,NULL,'es',1,1,'AI',SYSUTCDATETIME())",(cid,)))
    expect_rejected('SQL unique client message constraint rejects duplicate message',
        lambda cursor:(conversation(cursor,cid),message(cursor,cid,client_id)), lambda cursor:message(cursor,cid,client_id))
    scope = str(uuid.uuid4())
    def prepare_key(cursor):
        conversation(cursor,cid)
        cursor.execute('INSERT dbo.Idempotency VALUES(%s,%s,%s,%s)',(scope,'B'*64,'A'*64,cid))
    expect_rejected('SQL idempotency scope/key constraint rejects duplicate key',prepare_key,
        lambda cursor:cursor.execute('INSERT dbo.Idempotency VALUES(%s,%s,%s,%s)',(scope,'B'*64,'C'*64,cid)))
    expect_rejected('SQL inbox requires a persisted message',lambda cursor:conversation(cursor,cid),
        lambda cursor:cursor.execute("INSERT dbo.Inbox VALUES(%s,%s,%s,'Pending',SYSUTCDATETIME())",(str(uuid.uuid4()),cid,str(uuid.uuid4()))))
    with connect() as connection:
        with connection.cursor() as cursor:
            cursor.execute('SELECT COUNT(*) FROM dbo.Conversation WHERE Id=%s',(cid,))
            if cursor.fetchone()[0] != 0: raise RuntimeError('Test fixture remained after rollback')
    checks.append('Every constraint fixture rolled back; no product data removed')
    report = dict(result='PASS',observedAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),checks=checks,
        scope='Physical SQL Server schema over encrypted Python TDS; no C# repository durability/concurrency claim',
        backendRepositoryLiveVerified=False)
    (repo/'docs/progress/b04-schema-evidence.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print('PASS: schema fixtures rolled back')
except Exception as error:
    print('Schema checks incomplete: '+type(error).__name__+'; arbitrary SQL errors not exported.'); sys.exit(1)
