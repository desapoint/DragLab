"""Verify and repeat the offline longer-afterfire and source-location studies."""
from pathlib import Path
import importlib.util
import wave
import numpy as np

root = Path(__file__).resolve().parents[1]
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

studies = (
    ('backfire-explosion-variants-20261020', ('G-full-afterfire', 'H-deep-afterfire', 'I-rolling-afterfire')),
    ('backfire-location-variants-20261020', ('J-before-silencer', 'K-inside-silencer', 'L-after-silencer')),
)
for study, variants in studies:
    folder = root / 'reports' / study
    lines = []
    for identity in ('29-exhaust-1', '30-exhaust-2'):
        bed = read(folder / f'{identity}-no-pop-control.wav')
        original = read(folder / f'{identity}-original.wav')
        previous = root / 'reports/backfire-bang-variants-20261020'
        assert np.array_equal(original[:105600], read(previous / f'{identity}-original.wav'))
        assert np.array_equal(bed[:105600], read(previous / f'{identity}-no-pop-control.wav'))
        tails, signals = [], []
        for name in variants:
            path = folder / f'{identity}-{name}.wav'
            samples = read(path)
            assert samples.shape == (144000, 2)
            assert np.array_equal(samples[:43200], bed[:43200])
            peak = np.max(np.abs(samples.astype(float))) / 32768
            assert peak < .95
            delta = samples.astype(float) - bed
            tail = rms(delta[round(1.17 * 48000):round(1.42 * 48000)])
            assert tail > .001
            tails.append(tail)
            signals.append(samples)
            lines.append(f'PASS {identity} {name}: same pre-event bed; peak {peak:.5f}; 250-500 ms tail RMS {tail:.6f}.')
            output = folder / f'{identity}-{name}-repeat.wav'
            extract.extract(path, output, 0, 3, repeats=4)
            assert read(output).shape == (633600, 2)
        assert all(not np.array_equal(signals[a], signals[b]) for a, b in ((0, 1), (1, 2), (0, 2)))
        assert tails[0] < tails[1] < tails[2]
        lines.append(f'PASS {identity}: distinct responses; original and no-pop prefixes match prior controls; four-repeat outputs are 13.2 seconds at stereo 48 kHz PCM16.')
    report = folder / 'verification.txt'
    unity_lines = report.read_text(encoding='utf-8-sig').splitlines()[:3]
    report.write_text('\n'.join(unity_lines) + '\n\n' + '\n'.join(lines) + '\n', encoding='utf-8')
    print('\n'.join(lines))
