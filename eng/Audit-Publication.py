"""Check the prospective Git payload without printing matched secret values.

This is a focused publication check, not a general secret-scanner certification.
It includes staged files and nonignored untracked files. Runtime data stay local.
"""
from pathlib import Path
import json
import re
import subprocess
import sys
from urllib.parse import unquote

repo = Path(__file__).resolve().parents[1]
raw = subprocess.check_output(
    ['git', '-c', 'safe.directory='+str(repo), 'ls-files', '--cached', '--others', '--exclude-standard', '-z'], cwd=repo
)
files = sorted(set(p.decode('utf-8') for p in raw.split(b'\0') if p))
errors = []
patterns = {
    'private-key': re.compile(r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'),
    'github-token': re.compile(r'\b(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,})\b'),
    'telegram-token': re.compile(r'\b\d{7,14}:[A-Za-z0-9_-]{32,}\b'),
    'meta-access-token': re.compile(r'\bEAA[A-Za-z0-9]{60,}\b'),
    'cloud-access-key': re.compile(r'\b(?:AKIA|ASIA)[A-Z0-9]{16}\b'),
}
binary = {'.png', '.jpg', '.jpeg', '.gif', '.ico', '.pdf', '.pptx', '.woff', '.woff2'}
known_local_secrets = []
for env_file in [repo/'deploy/local/.env', repo/'deploy/channels/.env']:
    if not env_file.exists():
        continue
    for entry in env_file.read_text(encoding='utf-8-sig').splitlines():
        if '=' not in entry or entry.lstrip().startswith('#'):
            continue
        key, value = entry.split('=', 1)
        if key != 'CCAI_SYNTHETIC_USER_PASSWORD' and re.search('PASSWORD|SECRET|TOKEN|SERVICE_KEY', key):
            if len(value) >= 12 and not value.startswith(('GENERATE_', 'REVIEW_')):
                known_local_secrets.append(value)
for name in files:
    path = repo / name
    parts = Path(name).parts
    if any(p in {'.local', 'secrets', 'node_modules', 'bin', 'obj', 'provider-recordings'} for p in parts):
        errors.append((name, 'private-directory'))
    if path.name.startswith('.env') and path.name != '.env.example':
        errors.append((name, 'environment-file'))
    if path.suffix.lower() in {'.db', '.bak', '.key', '.pfx', '.pem', '.log', '.zip'} or path.name.endswith(('-wal', '-shm')):
        errors.append((name, 'private-runtime-file'))
    if path.is_symlink():
        errors.append((name, 'symlink'))
        continue
    if not path.is_file():
        errors.append((name, 'missing-file'))
        continue
    if path.stat().st_size > 10 * 1024 * 1024:
        errors.append((name, 'unexpected-large-file'))
    if path.suffix.lower() in binary:
        continue
    try:
        source = path.read_text(encoding='utf-8-sig')
    except UnicodeDecodeError:
        errors.append((name, 'unreviewed-binary'))
        continue
    for category, pattern in patterns.items():
        # Pattern definitions themselves contain no matching credentials.
        for match in pattern.finditer(source):
            line = source.count('\n', 0, match.start()) + 1
            errors.append((name, f'{category}:line-{line}'))
    if any(secret in source for secret in known_local_secrets):
        errors.append((name, 'exact-local-service-secret'))
    if path.suffix == '.json':
        try:
            json.loads(source)
        except json.JSONDecodeError:
            errors.append((name, 'invalid-json'))
    if path.suffix == '.md':
        for link in re.findall(r'!?\[[^\]]*\]\(([^\n)]+)\)', source):
            target = link.strip().split(' "', 1)[0].strip('<>')
            if re.match(r'^(?:[a-zA-Z][a-zA-Z0-9+.-]*:|#|//)', target):
                continue
            relative = unquote(target.split('#', 1)[0])
            if not relative:
                continue
            resolved = (path.parent / relative).resolve()
            if not resolved.is_relative_to(repo) or not resolved.exists():
                errors.append((name, f'broken-link:{relative}'))
for name, category in errors:
    print(f'BLOCKED {name}: {category}')
print(f'Publication payload: {len(files)} files, {len(errors)} findings. Matched values are never printed.')
sys.exit(1 if errors else 0)
