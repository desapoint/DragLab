from pathlib import Path
import importlib.util
import wave
import numpy as np

root = Path(__file__).resolve().parents[1]
folder = root / 'reports/backfire-bang-variants-20261020'
old = root / 'reports/backfire-pop-variants-20261020'
spec = importlib.util.spec_from_file_location('extract', root / 'tools/Extract-RaceAudioSegment.py')
extract = importlib.util.module_from_spec(spec)
spec.loader.exec_module(extract)

def read(path):
    with wave.open(str(path), 'rb') as source:
        assert source.getframerate() == 48000 and source.getnchannels() == 2
        assert source.getsampwidth() == 2
        return np.frombuffer(source.readframes(source.getnframes()), dtype='<i2').reshape(-1, 2)

def rms(samples):
    return np.sqrt(np.mean((samples.astype(float) / 32768) ** 2))

lines = []
for identity in ('29-exhaust-1', '30-exhaust-2'):
    bed = read(folder / f'{identity}-no-pop-control.wav')
    original = read(folder / f'{identity}-original.wav')
    assert np.array_equal(original, read(old / f'{identity}-original.wav'))
    assert np.array_equal(bed, read(old / f'{identity}-no-pop-control.wav'))
    baseline = read(old / f'{identity}-B-deep-pop.wav').astype(float) - bed
    tails = []
    for name in ('D-sharp-bang', 'E-deep-bang', 'F-long-bark'):
        path = folder / f'{identity}-{name}.wav'
        samples = read(path)
        assert samples.shape == (105600, 2)
        assert np.array_equal(samples[:43200], bed[:43200])
        peak = np.max(np.abs(samples.astype(float))) / 32768
        assert peak < .9
        delta = samples.astype(float) - bed
        gain = 20 * np.log10(rms(delta) / rms(baseline))
        assert gain > 10
        # 50-250 ms after the first actual trigger, about 0.917 s into the crop.
        tail = rms(delta[round(.97 * 48000):round(1.17 * 48000)])
        tails.append(tail)
        lines.append(f'PASS {identity} {name}: unchanged pre-event bed; peak {peak:.5f}; pop-layer RMS {gain:.2f} dB above prior B; 50-250 ms tail RMS {tail:.6f}.')
        output = folder / f'{identity}-{name}-repeat.wav'
        extract.extract(path, output, 0, 2.2, repeats=4)
        assert read(output).shape == (480000, 2)
    assert tails[0] < tails[1] < tails[2]
    lines.append(f'PASS {identity}: original and no-pop controls identical to prior render; successively stronger long tails D/E/F; all repeats are 10 seconds, stereo 48 kHz PCM16.')

report = folder / 'verification.txt'
unity_lines = report.read_text(encoding='utf-8-sig').splitlines()[:3]
report.write_text('\n'.join(unity_lines) + '\n\n' + '\n'.join(lines) + '\n', encoding='utf-8')
print('\n'.join(lines))
