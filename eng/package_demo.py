"""Build the local delivery package: git archive of HEAD + start note + SHA256SUMS, written to .local/release/.

Only committed files are packaged, so ignored data (.local, .env, databases, caches) cannot leak. The file list
is checked again against forbidden patterns before writing. Nothing is uploaded or published.
"""
import datetime
import fnmatch
import hashlib
import subprocess
import sys
from pathlib import Path

VERSION = 'v0.13'
FORBIDDEN = ['*.env', '.env*', '*.db', '*.db-*', '*.sqlite*', '*.pfx', '*.pem', '*.key', '*.p12', '.local/*', '*/.local/*',
             '*/bin/*', '*/obj/*', 'provider-recordings/*', '*.log']


def git(*args, repo):
    return subprocess.run(['git', *args], cwd=repo, check=True, capture_output=True, text=True).stdout.strip()


def main():
    repo = Path(__file__).resolve().parents[1]
    commit = git('rev-parse', 'HEAD', repo=repo)
    if git('status', '--porcelain', '--untracked-files=no', repo=repo):
        print('Note: uncommitted changes are NOT included; the package contains HEAD ' + commit[:12] + '.')
    files = git('ls-tree', '-r', '--name-only', 'HEAD', repo=repo).splitlines()
    leaked = [f for f in files if any(fnmatch.fnmatch(f, p) or fnmatch.fnmatch(Path(f).name, p) for p in FORBIDDEN)
              and not f.endswith('.env.example')]
    if leaked:
        print('Refusing to package; forbidden files are tracked:\n  ' + '\n  '.join(leaked))
        return 1
    out = repo / '.local' / 'release'
    out.mkdir(parents=True, exist_ok=True)
    name = f'ContactCenterAI-demo-{VERSION}-{commit[:8]}'
    note = out / 'START-HERE.txt'
    note.write_text(
        f'ContactCenterAI resort demo {VERSION}\nCommit {commit}\nPackaged {datetime.datetime.now(datetime.timezone.utc):%Y-%m-%d %H:%M} UTC\n\n'
        'Requirements: Docker Desktop (or Docker Engine with Compose v2) and Internet access the first time.\n'
        'Windows: start-demo.cmd    macOS/Linux: ./demo.sh start\n'
        'Then open http://127.0.0.1:7452 - users customer-a / customer-b / operations-admin, password 123456Aa!\n\n'
        'Guide: docs/demo-guide.en.md (English) / docs/demo-guide.es.md (Spanish). Release notes: docs/release-notes-v0.13.md\n'
        'Fictional data only. No licence has been chosen yet: default copyright applies.\n', encoding='utf-8', newline='\r\n')
    archive = out / (name + '.zip')
    subprocess.run(['git', 'archive', '--format=zip', f'--prefix={name}/', f'--add-file={note}', '-o', str(archive), 'HEAD'], cwd=repo, check=True)
    digest = hashlib.sha256(archive.read_bytes()).hexdigest()
    (out / 'SHA256SUMS').write_text(f'{digest}  {archive.name}\n', encoding='utf-8', newline='\n')
    print(f'Package: {archive}\nSHA-256: {digest}\nFiles: {len(files) + 1}')
    return 0


if __name__ == '__main__':
    sys.exit(main())
