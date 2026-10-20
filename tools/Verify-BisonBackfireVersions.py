"""Check five WAV-only Bison voicing previews against the unchanged 64-second reference."""
from pathlib import Path
import hashlib
import json
import wave
import numpy as np

root = Path(__file__).resolve().parents[1]
folder = root / 'reports/bison-backfire-versions-20261020'
previous = root / 'reports/shared-backfire-audition-20261020/30-exhaust-2.wav'
manifest = json.loads((folder / 'manifest.json').read_text(encoding='utf-8-sig'))
assert (folder / 'reference-default.wav').read_bytes() == previous.read_bytes()
cases = manifest['cases']
assert len(cases) == 5

def read(path):
    with wave.open(str(path), 'rb') as audio:
        assert (audio.getframerate(), audio.getnchannels(), audio.getsampwidth(), audio.getnframes()) == (48000, 2, 2, 3072000)
        return np.frombuffer(audio.readframes(audio.getnframes()), dtype='<i2').reshape(-1, 2)

reference = read(previous)
hashes = set()
lines = ['PASS default source stays byte-identical to the original Bison chambered 64-second WAV.']
for c in cases:
    path = folder / c['file']
    assert hashlib.sha256(path.read_bytes()).hexdigest() == c['sha256']
    pcm = read(path)
    assert np.array_equal(pcm[:8*48000], reference[:8*48000]), 'Engine baseline changed'
    assert c['events'] == cases[0]['events'], 'Event input/timing/seed changed between versions'
    assert c['overflows'] == 0 and c['chapters'][2]['accepted'] == c['chapters'][14]['accepted'] == 0
    assert len(c['chapters']) == 16 and c['duration'] == 64
    assert c['voicing']['turbulenceCoupling'] < 1 and c['voicing']['pressureCoupling'] > 1
    peak = np.max(np.abs(pcm.astype(float))) / 32768
    assert peak < .95
    assert not np.array_equal(pcm, reference)
    for chapter in c['chapters']:
        samples = pcm[int(chapter['start']*48000):int(chapter['end']*48000)].astype(float)/32768
        assert np.sqrt(np.mean(samples*samples)) > .0001
    hashes.add(c['sha256'])
    lines.append(f"PASS {c['id']}: full 64-second stereo sequence, same engine baseline and event inputs, peak {peak:.5f}, zero voice overflow, intact no-explosion controls.")
assert len(hashes) == 5
assert not (folder / 'index.html').exists()
report = folder / 'verification.txt'
baseline = report.read_text(encoding='utf-8-sig').split('\nPASS independent WAV')[0].rstrip()
report.write_text(baseline+'\nPASS independent WAV checks: five distinct variants, no HTML.\n'+'\n'.join(lines)+'\n', encoding='utf-8')
print('\n'.join(lines))
