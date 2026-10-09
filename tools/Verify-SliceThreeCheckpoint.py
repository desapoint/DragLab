"""Verify immutable 3H source/player archives against the current workspace."""
import argparse
import hashlib
import re
import subprocess
from pathlib import Path
from zipfile import ZipFile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--revision', default='')
args = parser.parse_args()
assert not args.revision or re.fullmatch(r'[a-z0-9][a-z0-9-]{0,31}', args.revision), 'Invalid revision'
suffix = '-' + args.revision if args.revision else ''
source_archive = 'source-checkpoint' + suffix + '.zip'
windows_archive = 'Windows-checkpoint' + suffix + '.zip'
root = Path(__file__).resolve().parents[1]
phase = root / 'reports/phases/3h'
report = phase / 'checkpoint-integrity.txt'

def digest_file(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def digest_entry(archive, name):
    with archive.open(name) as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

inventory = subprocess.check_output(['git', '-c', 'core.quotepath=false', 'ls-files',
                                      '--cached', '--others', '--exclude-standard', '-z'], cwd=root)
source_names = {name.decode('utf-8') for name in inventory.split(b'\0') if name}
# The integrity receipt is generated after packaging and cannot hash itself.
source_names.discard(report.relative_to(root).as_posix())
source_names = {name for name in source_names if (root / name).is_file()}
required = {'AGENTS.md', 'Game/Packages/manifest.json', 'Game/ProjectSettings/ProjectVersion.txt',
            'docs/objective.md', 'docs/SLICE_3_ACCEPTANCE.md', 'reports/SLICE_3_AUDIT.md',
            'reports/phases/2a/before/legacy-earned-profile-v1.json',
            'Game/Assets/DragLab/Editor/RaceThermalVerification.cs',
            'Game/Assets/DragLab/Editor/RaceThermalVerification.cs.meta'}
assert required <= source_names, 'Required source/fixture files are missing from Git inventory'
lines = ['3H checkpoint integrity / complete source and runnable Windows folder']
for filename, expected, base in [
    (source_archive, source_names, root),
    (windows_archive, {'Windows/' + p.relative_to(root / 'Game/Builds/Windows').as_posix()
                               for p in (root / 'Game/Builds/Windows').rglob('*') if p.is_file()},
     root / 'Game/Builds')]:
    path = phase / filename
    with ZipFile(path) as archive:
        names = [info.filename for info in archive.infolist() if not info.is_dir()]
        assert len(names) == len(set(names)), 'Duplicate archive paths: ' + filename
        assert set(names) == expected, 'Incomplete or extra archive files: ' + filename
        assert archive.testzip() is None, 'Archive CRC failure: ' + filename
        for name in names:
            assert digest_entry(archive, name) == digest_file(base / name), 'Payload mismatch: ' + name
        if filename == windows_archive:
            dll = 'Windows/DragLab_Data/Managed/Assembly-CSharp.dll'
            assert dll in names, 'Missing compiled gameplay assembly'
            lines.append('PASS: archived compiled gameplay assembly matches the current validated player')
        lines.append('PASS: ' + filename + ' / ' + str(len(names)) + ' files / complete paths, CRC and every payload match')
    lines.append(filename + ' SHA256 ' + digest_file(path))
report.write_text('\n'.join(lines) + '\n', encoding='utf-8')
print('\n'.join(lines))
