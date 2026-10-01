"""SQLite online backup and restore rehearsal; never replaces active databases."""
import datetime,hashlib,json,pathlib,shutil,sqlite3,uuid
repo=pathlib.Path(__file__).resolve().parents[1]
local=(repo/'.local').resolve()
target=local/'demo-backups'/('snapshot-'+datetime.datetime.now(datetime.UTC).strftime('%Y%m%d-%H%M%S')+'-'+uuid.uuid4().hex[:8])
target.mkdir(parents=True)
sources={'operations':local/'demo/operations.db','reservations':local/'source/reservations.db'}
def connect(path):
    resolved=path.resolve()
    if not resolved.is_relative_to(local) or path.is_symlink():raise RuntimeError('BACKUP_PATH_OUTSIDE_LOCAL')
    return sqlite3.connect(resolved.as_uri()+'?mode=ro',uri=True,timeout=5)
def inspect(path):
    with connect(path) as db:
        assert db.execute('PRAGMA integrity_check').fetchone()[0]=='ok'
        assert not db.execute('PRAGMA foreign_key_check').fetchall()
        tables=[r[0] for r in db.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name")]
        counts={name:db.execute('SELECT COUNT(*) FROM "'+name.replace('"','""')+'"').fetchone()[0] for name in tables}
        return {'counts':counts,'schemaVersion':db.execute('PRAGMA user_version').fetchone()[0]}
manifest={'scope':'two SQLite online snapshots; not a distributed atomic backup; source receipts reconcile uncertain commands','createdAt':datetime.datetime.now(datetime.UTC).isoformat(),'files':{}}
for name,path in sources.items():
    with connect(path) as source,sqlite3.connect(target/(name+'.db')) as destination:
        source.backup(destination)
    backup=target/(name+'.db')
    manifest['files'][name]={'sha256':hashlib.sha256(backup.read_bytes()).hexdigest(),**inspect(backup)}
restore=local/'restore-rehearsals'/target.name;restore.mkdir(parents=True)
for name in sources:
    restored=restore/(name+'.db');shutil.copyfile(target/(name+'.db'),restored)
    assert inspect(restored)=={k:v for k,v in manifest['files'][name].items() if k!='sha256'}
with connect(restore/'operations.db') as op,connect(restore/'reservations.db') as src:
    commands=[row[0] for row in op.execute("SELECT Id FROM BusinessCommand WHERE Status='Completed'")]
    receipts={row[0] for row in src.execute('SELECT CommandId FROM Receipt')}
    assert set(commands).issubset(receipts),'SOURCE_RECEIPT_MISSING_FROM_RESTORE'
manifest['restore']={'integrity':'PASS','foreignKeys':'PASS','rowCounts':'PASS','completedCommandsHaveSourceReceipts':'PASS','activeDatabasesReplaced':False}
(target/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
proof={'scope':manifest['scope'],'createdAt':manifest['createdAt'],'backupFiles':2,'restore':manifest['restore'],'counts':{name:manifest['files'][name]['counts'] for name in sources},'artifactPolicy':'DB/backup/session data remain in .local and are excluded from portfolio ZIP'}
(repo/'docs/progress/demo-v0.7-restore.json').write_text(json.dumps(proof,indent=2),encoding='utf-8')
print('Backup and separate restore verified. Current conversations and reservations were preserved.')
