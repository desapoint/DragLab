"""Package six isolated preview WAVs with constant-gain, gated loudness matching."""
from pathlib import Path
import hashlib
import itertools
import json
import math
import re
import subprocess
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT/'reports/isolated-backfire-sources-20261020'
FFMPEG = Path('C:/tools/FFMPEG/ffmpeg.exe')
RATE = 48000
FRAMES = 259200
NAMES = ('01-sharp-snap', '02-ragged-burst', '03-fuller-bang')

def measure(pcm):
    stereo = np.column_stack((pcm, pcm)).astype('<f8')
    process = subprocess.run([str(FFMPEG), '-hide_banner', '-nostats', '-f', 'f64le', '-ar', str(RATE), '-ac', '2', '-i', 'pipe:0', '-af', 'loudnorm=I=-24:TP=-2:LRA=11:print_format=json', '-f', 'null', '-'], input=stereo.tobytes(), stdout=subprocess.DEVNULL, stderr=subprocess.PIPE, check=True)
    text = process.stderr.decode('utf-8', errors='replace')
    data = json.loads(re.findall(r'\{[^{}]*"input_i"[^{}]*\}', text)[-1])
    return float(data['input_i']), float(data['input_tp'])

def load(name):
    data = np.fromfile(FOLDER/name, dtype='<f8')
    assert data.size == FRAMES and np.isfinite(data).all()
    return data

signals = {}
for suffix in ('dry', 'exhaust'):
    for name in NAMES:
        signals[name+'-'+suffix] = load(name+'-'+suffix+'.f64')

# Independent linear-path verification: each wet stream must be the dry stream
# convolved with exactly the same prepared exhaust impulse, not separately voiced.
impulse = load('exhaust-impulse.f64')
fft_size = 1 << (FRAMES*2-1).bit_length()
transfer = np.fft.rfft(impulse, fft_size)
for name in NAMES:
    dry, wet = signals[name+'-dry'], signals[name+'-exhaust']
    predicted = np.fft.irfft(np.fft.rfft(dry, fft_size)*transfer, fft_size)[:FRAMES]
    assert np.max(np.abs(wet-predicted)) < 1e-9*max(1, np.max(np.abs(wet)))

similarities = []
for a,b in itertools.combinations(NAMES,2):
    x = signals[a+'-dry'][28800:86400]
    y = signals[b+'-dry'][28800:86400]
    # Account for a few samples of peak-alignment difference before testing shape.
    best = 0
    for shift in range(-240,241):
        xa,ya = (x[-shift:],y[:shift]) if shift<0 else ((x[:-shift],y[shift:]) if shift else (x,y))
        score = abs(float(np.dot(xa,ya)))/math.sqrt(float(np.dot(xa,xa)*np.dot(ya,ya)))
        best = max(best, score)
    assert best < .85, (a,b,best)
    similarities.append({'a':a,'b':b,'max_aligned_similarity':best})

measurements = {name: measure(values) for name,values in signals.items()}
# Meter every delivered stereo file at one common target, lowering it if an
# impulsive source needs more crest headroom. Never compress or limit the attack.
target = round(min(-24, min(i-tp-2.2 for i,tp in measurements.values())), 2)
rows = []
for index,(key,values) in enumerate(signals.items(),1):
    loudness,true_peak = measurements[key]
    gain = 10**((target-loudness)/20)
    scaled = values*gain
    assert np.max(np.abs(scaled)) < 1
    mono = np.rint(scaled*32767).astype('<i2')
    pcm = np.column_stack((mono,mono))
    output = FOLDER/(key+'.wav')
    with wave.open(str(output),'wb') as audio:
        audio.setnchannels(2); audio.setsampwidth(2); audio.setframerate(RATE); audio.writeframes(pcm.tobytes())
    delivered_i, delivered_tp = measure(mono.astype(float)/32768)
    assert abs(delivered_i-target) <= .10, (key,delivered_i,target)
    assert delivered_tp <= -2
    assert np.all(mono[:28800] == 0)
    assert np.max(np.abs(mono[-9600:].astype(float))) <= 1
    assert np.max(np.abs(mono.astype(float)/32767-scaled)) <= .50001/32767
    rows.append({'file':output.name,'lufs':delivered_i,'true_peak_db':delivered_tp,'constant_gain':gain,'sha256':hashlib.sha256(output.read_bytes()).hexdigest()})
assert max(r['lufs'] for r in rows)-min(r['lufs'] for r in rows) <= .10
assert len(set(r['sha256'] for r in rows)) == 6
manifest = {'sample_rate':RATE,'channels':2,'frames':FRAMES,'target_lufs':target,'event_starts':[.6,2.2,3.8],'source_comparisons':similarities,'outputs':rows}
(FOLDER/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
report = FOLDER/'verification.txt'
original = report.read_text(encoding='utf-8-sig').split('PASS loudness packaging')[0].rstrip().replace(' Loudness/WAV validation pending Python packaging.', '')
lines = [f"PASS loudness packaging: six stereo PCM16 WAVs, 5.4 s each; common {target:.2f} LUFS target, spread <=0.10 LU, true peak <=-2 dBTP; constant gain only, no limiter/compressor.", 'PASS independent common-exhaust impulse-convolution check for all three sources.', 'PASS source shapes differ after gain and +/-5 ms alignment; maximum pair similarity '+str(round(max(x['max_aligned_similarity'] for x in similarities),5))+'.']
lines += [f"PASS {r['file']}: {r['lufs']:.2f} LUFS / {r['true_peak_db']:.2f} dBTP; preserved waveform, silent lead/tail." for r in rows]
report.write_text(original+'\n'+'\n'.join(lines)+'\n',encoding='utf-8')
print('\n'.join(lines))
