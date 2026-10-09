"""Collect passing Slice-3 runner evidence without changing historical reports."""
import argparse
import csv
from datetime import datetime
import re
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--domain-run', required=True)
parser.add_argument('--windows-run', required=True)
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
phase = root / 'reports/phases/3h'
expected_domain = {'thermal', 'collection', 'assembly', 'valuation', 'paid', 'career', 'business',
                   'balance', 'economy-save', 'foundation', 'interrupted-physics', 'integrated-tools', 'scene'}
expected_windows = {'thermal', 'stock-durability', 'economy-interruption', 'collection', 'setups', 'valuation', 'paid', 'paid-resume',
                    'career', 'business', 'business-resume', 'balance', 'gameplay', 'keyboard', 'reset',
                    'physical-bench', 'bench-config', 'audio-levels'}
all_rows = []
for run, expected, stage in [(args.domain_run, expected_domain, 'Domain'), (args.windows_run, expected_windows, 'Windows')]:
    assert re.fullmatch(r'[A-Za-z0-9_-]+', run), 'Invalid run name'
    folder = phase / 'full-validation' / run
    with (folder / 'manifest.csv').open(encoding='utf-8-sig', newline='') as stream:
        rows = list(csv.DictReader(stream))
    assert len(rows) == len(expected), (stage, 'Incomplete manifest', len(rows))
    assert {r['Gate'] for r in rows} == expected, (stage, 'Missing/extra gates')
    assert all(r['Stage'] == stage and r['Result'] == 'PASS' for r in rows), (stage, 'Failed gate')
    if stage == 'Domain':
        audit = (folder / 'foundation-audit-verification.txt').read_text(encoding='utf-8-sig')
        assert audit.startswith('Model: draglab-integrated-tools-v16\n'), 'Stale physical audit model'
        assert re.search(r'failures: 0;', audit), 'Physical audit has failures'
        with (folder / 'foundation-audit-runs.csv').open(encoding='utf-8-sig', newline='') as stream:
            scenarios = list(csv.DictReader(stream))
        assert len(scenarios) == int(re.search(r'simulation runs: (\d+);', audit).group(1)), 'Incomplete audit CSV'
        assert len({r['car'] for r in scenarios}) == 8, 'Missing audit vehicles'
        audit_utc = datetime.fromisoformat(re.search(r'^UTC: (.+)$', audit, re.MULTILINE).group(1).replace('Z', '+00:00'))
        times = {r['Gate']: datetime.fromisoformat(r['UTC'].replace('Z', '+00:00')) for r in rows}
        assert times['economy-save'] <= audit_utc <= times['foundation'], 'Stale physical audit timestamp'
    for p in sorted(folder.iterdir()):
        if p.suffix not in {'.csv', '.txt'} or p.name == 'manifest.csv':
            continue
        text = p.read_text(encoding='utf-8-sig')
        assert not re.search(r'^FAIL[: ]', text, re.MULTILINE), p
        assert not re.search(r'Runtime errors:\s*[1-9]', text), p
        # Review evidence retains every value/label, with Git-compatible whitespace.
        name = p.stem + '-matrix.csv' if p.suffix == '.csv' and not p.name.endswith('matrix.csv') else p.name
        destination = phase / (stage.lower() + '-' + name)
        destination.write_text('\n'.join(line.rstrip() for line in text.splitlines()) + '\n', encoding='utf-8')
    all_rows.extend(rows)
with (phase / 'full-validation-matrix.csv').open('w', encoding='utf-8', newline='') as stream:
    writer = csv.DictWriter(stream, fieldnames=['Stage', 'Gate', 'Result', 'Evidence', 'UTC'])
    writer.writeheader()
    writer.writerows(all_rows)
summary = []
for p in sorted(phase.glob('domain-*.txt')):
    text = p.read_text(encoding='utf-8')
    lines = [line for line in text.splitlines() if re.search(r'Checks|TOTAL|foundation checks', line)]
    if lines:
        summary.append(p.name + ': ' + ' / '.join(lines[-2:]))
(phase / 'full-validation-summary.txt').write_text(
    'PASS: 13 domain gates and 18 Windows workflows. Overlapping checks are not summed as unique cases.\n'
    + '\n'.join(summary) + '\n', encoding='utf-8')
print('\n'.join(summary))
print('PASS: all required manifests and review evidence collected.')
