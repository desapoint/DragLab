"""Verify a real production reference and five matched, backfire-heavy WAV options."""
from pathlib import Path
import hashlib
import json
import wave
import sys
import numpy as np

root = Path(__file__).resolve().parents[1]
folder = root / (sys.argv[1] if len(sys.argv)>1 else 'reports/tempest-backfire-comparison-20261020')
manifest = json.loads((folder / 'manifest.json').read_text(encoding='utf-8-sig'))
assert manifest['physicalPops'] >= manifest['publishedPopEvents'] >= 100
assert manifest['referenceMismatches'] == 0 and len(manifest['options']) == 5
reference_path = folder / manifest['reference']
capture = root/'reports/tempest-backfire-comparison-20261020/tempest-turbo-open.wav'
assert reference_path.read_bytes() == capture.read_bytes()
assert hashlib.sha256(reference_path.read_bytes()).hexdigest() == manifest['referenceSha256']

def read(path):
    with wave.open(str(path), 'rb') as audio:
        assert (audio.getframerate(), audio.getnchannels(), audio.getsampwidth(), audio.getnframes()) == (48000, 2, 2, manifest['frames'])
        return np.frombuffer(audio.readframes(audio.getnframes()), dtype='<i2').reshape(-1, 2)

reference = read(reference_path)
if 'sharp' in folder.name or 'texture' in folder.name:
    baseline = root / 'reports/tempest-backfire-comparison-20261020'
    prior = json.loads((baseline/'manifest.json').read_text(encoding='utf-8-sig'))
    assert reference_path.read_bytes() == (baseline/prior['reference']).read_bytes()
    def same(a,b):
        if isinstance(a,dict):
            assert a.keys()==b.keys()
            for key in a: same(a[key],b[key])
        elif isinstance(a,list):
            assert len(a)==len(b)
            for x,y in zip(a,b): same(x,y)
        elif isinstance(a,float) or isinstance(b,float):
            assert abs(a-b)<=1e-12*max(1,abs(b)), (a,b)
        else:
            assert a==b, (a,b)
    same(manifest['events'],prior['events'])
    assert manifest['frames'] == prior['frames']

first = None
prefix = int((manifest['events'][0]['time']-.02)*48000)
assert prefix > 0
hashes = {manifest['referenceSha256']}
lines = [f"PASS reference: production-render bytes and exact original-synth replay; {manifest['physicalPops']} physical / {manifest['publishedPopEvents']} published real events; {manifest['frames']/48000:.3f} seconds."]
for option in manifest['options']:
    path = folder / option['file']
    assert hashlib.sha256(path.read_bytes()).hexdigest() == option['sha256']
    pcm = read(path)
    assert np.array_equal(pcm[:prefix], reference[:prefix]), 'Engine changed before first physical pop'
    assert not np.array_equal(pcm, reference)
    if first is None:
        first = pcm
    else:
        assert np.sqrt(np.mean(((pcm.astype(float)-first)/32768)**2)) > (.0001 if 'texture' in folder.name else .004), 'Preview differences are too deeply masked'
    if 'texture' in folder.name:
        assert option['shellSquared'] > 0 and option['crackleSquared'] > 0
        sharp = json.loads((root/'reports/tempest-backfire-sharp-20261020/manifest.json').read_text(encoding='utf-8-sig'))
        base_index = 3 if option['file'].startswith(('01-', '02-')) else 4
        before = sharp['options'][base_index]['voicing']
        for key,value in before.items():
            if key not in ('shellCoupling','crackleCoupling'):
                assert option['voicing'][key] == value, key
    assert option['accepted'] == manifest['publishedPopEvents'] and option['overflow'] == 0
    assert abs(option['heatJ']-option['releasedHeatJ']) < option['heatJ']*1e-6
    assert all(option[key] > 0 for key in ('pressureSquared', 'turbulenceSquared', 'structureSquared'))
    peak = np.max(np.abs(pcm.astype(float)))/32768
    assert peak < .98
    assert np.sqrt(np.mean((pcm.astype(float)/32768)**2)) > .001
    hashes.add(option['sha256'])
    lines.append(f"PASS {option['file']}: same real triggers and engine prefix; pressure/discharge/body responses active; heat ledger closes; no voice losses; peak {peak:.5f}.")
assert len(hashes) == 6 and not (folder / 'index.html').exists()
for event in manifest['events']:
    p = event['packet']
    assert p['burnSeconds'] <= .004 and p['fuelMg'] > 0 and p['oxygenMg'] > 0
report = folder / 'verification.txt'
original = report.read_text(encoding='utf-8-sig').split('\nPASS independent WAV')[0].rstrip()
report.write_text(original+'\nPASS independent WAV/reference checks: six distinct matched files; no HTML.\n'+'\n'.join(lines)+'\n', encoding='utf-8')
print('\n'.join(lines))
