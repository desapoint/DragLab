"""DragLab: physically inspired cross-plane V8 exhaust sound synthesis.

Creates a 48 kHz stereo idle -> full-throttle rev -> idle recording, plus a
seamless-ish looping idle layer for Unity. No audio samples or external assets.

Requirements: pip install numpy scipy soundfile
Usage: python draglab_v8_exhaust_generator.py

Core mechanisms: 720-degree 4-stroke cycle; GM cross-plane 1-8-7-2-6-5-4-3
firing order; independent left/right bank headers; 8 unequal runner lengths;
blowdown + expansion/rarefaction pressure pulses; collector and tailpipe acoustic
reflections; intake / block resonances; throttle, RPM and crank inertia.
"""
from pathlib import Path
import argparse
import numpy as np
from scipy import signal
from scipy.interpolate import PchipInterpolator
import soundfile as sf

FS = 48000
SEED = 171818
FIRING_ORDER = (1, 8, 7, 2, 6, 5, 4, 3)
LEFT = {1, 3, 5, 7}
# Each branch length is slightly different: causes bank-specific manifold tone.
RUNNERS_M = {1: 0.50, 2: 0.61, 3: 0.43, 4: 0.49,
             5: 0.57, 6: 0.46, 7: 0.38, 8: 0.55}
CYL_GAIN = {1: 1.0, 2: .97, 3: 1.03, 4: .98,
            5: 1.02, 6: 1.00, 7: .96, 8: 1.04}


def lowpass(x, cutoff, order=2):
    return signal.sosfilt(signal.butter(order, cutoff, fs=FS, output='sos'), x, axis=0)


def highpass(x, cutoff, order=2):
    return signal.sosfilt(signal.butter(order, cutoff, btype='highpass', fs=FS, output='sos'), x, axis=0)


def bandpass(x, low, high, order=2):
    return signal.sosfilt(signal.butter(order, [low, high], btype='bandpass', fs=FS, output='sos'), x, axis=0)


def peak(x, hz, q):
    b, a = signal.iirpeak(hz, q, fs=FS)
    return signal.lfilter(b, a, x)


def tap(x, delay_ms):
    n = round(FS * delay_ms / 1000)
    return np.pad(x, (n, 0))[:len(x)]


def profile(duration, idle_only=False):
    t = np.arange(round(duration * FS)) / FS
    if idle_only:
        rpm = np.full_like(t, 840.0)
        throttle = np.full_like(t, 0.12)
    else:
        # Monotonic cubic interpolation prevents sudden pitch discontinuities.
        key_t = [0, .8, 1.75, 2.2, 2.45, 2.9, 3.55, 4.35, 5.05,
                 5.60, 5.80, 6.20, 6.80, 7.50, 8.25, 9.0, 10.8, 11.2]
        key_r = [840, 825, 850, 850, 1080, 1890, 3250, 4600, 5620,
                 6200, 6070, 4900, 3230, 1810, 1050, 835, 835, 835]
        key_a = [.12,.12, .13, .17, .62, .94, 1.0, 1.0, .97,
                 .92, .25, .10, .09, .08, .12, .12, .12, .12]
        rpm = PchipInterpolator(key_t, key_r)(np.clip(t, key_t[0], key_t[-1]))
        throttle = PchipInterpolator(key_t, key_a)(np.clip(t, key_t[0], key_t[-1]))
        # Mild uneven-idle combustion speed; decreases as rotational inertia grows.
        idle_factor = np.clip((1500 - rpm)/650, 0, 1)
        rpm += idle_factor * (9 * np.sin(2*np.pi*2.7*t) + 5*np.sin(2*np.pi*5.1*t))
    return t, rpm, np.clip(throttle, 0, 1)


def render(duration=11.2, idle_only=False):
    t, rpm, throttle = profile(duration, idle_only)
    count = len(t)
    # rpm/60 revolutions/s * 4 exhaust events/revolution: cross-plane 90° spacing.
    absolute_firing_phase = np.cumsum((rpm/60)*4/FS)
    event_bins = np.floor(absolute_firing_phase).astype(np.int64)
    event_positions = np.flatnonzero(np.diff(event_bins)) + 1
    bank_l = np.zeros(count, dtype=np.float64)
    bank_r = np.zeros(count, dtype=np.float64)
    block = np.zeros(count, dtype=np.float64)
    rng = np.random.default_rng(SEED)
    # Keep realistic idle lope by bank timing, NOT overpowering random noise.
    for idx in event_positions:
        cyl = FIRING_ORDER[int(event_bins[idx] - 1) % 8]
        # Interpolate fractional crank-phase crossing to limit quantization jitter.
        denom = absolute_firing_phase[idx] - absolute_firing_phase[idx-1]
        fraction = (event_bins[idx] - absolute_firing_phase[idx-1]) / denom
        start = (idx - 1) + fraction
        r = rpm[idx]
        a = throttle[idx]
        # Pulse gas velocity changes with engine load; runner reflection length.
        propagation = RUNNERS_M[cyl] / (365 + 85*a) * FS
        start += propagation
        onset = int(start)
        sub_sample = start - onset
        if onset >= count: continue

        # A pressure blowdown wave followed by underpressure (rarefaction).
        # Durations contract at higher RPM and exhaust flow. Never add white noise.
        scale = np.clip(1.17 - .075*np.log2(max(r, 750)/850) - .11*a, .78, 1.20)
        kernel_t = np.arange(round(.028*FS)) / FS
        x = kernel_t/scale
        pressure = ((1-np.exp(-x/.00020))*np.exp(-x/.0030)
                    - .28*(1-np.exp(-x/.00145))*np.exp(-x/.0092))
        # Short sharper leading blowdown at open throttle, without broadband hiss.
        pressure += .10*a*(1-np.exp(-x/.00012))*np.exp(-x/.0008)
        pressure *= (0.78 + .94*a) * (max(r, 800)/850)**(-.35)
        pressure *= CYL_GAIN[cyl] * (1 if idle_only else (1 + rng.normal(0, .008 if r > 1500 else .027)))
        end = min(count, onset + len(kernel_t))
        n = end - onset
        if n > 1:
            # fractional sample correction is applied through linear interpolation
            pulse = (1-sub_sample)*pressure[:n] + sub_sample*np.concatenate(([0], pressure))[:n]
            if cyl in LEFT:
                bank_l[onset:end] += pulse
            else:
                bank_r[onset:end] += pulse
            # Engine block/piston impulse reaches listener before the tailpipe.
            short_t = np.arange(min(n, round(.018*FS))) / FS
            mechanical = (1-np.exp(-short_t/.00033))*np.exp(-short_t/.006)
            mechanical *= np.sin(2*np.pi*118*short_t) + .23*np.sin(2*np.pi*270*short_t)
            block[onset:onset+len(short_t)] += .23*mechanical*CYL_GAIN[cyl]

    # Each manifold's pressure wave bounces through a different-length bank.
    # Echo polarity reversal models area changes in exhaust tubing.
    head_l = bank_l + .36*tap(bank_l, 3.1) - .22*tap(bank_l, 6.5) + .12*tap(bank_l, 11.2)
    head_r = bank_r + .33*tap(bank_r, 3.9) - .20*tap(bank_r, 8.1) + .11*tap(bank_r, 13.0)
    head_l += .09*peak(head_l, 167, 2.8) + .08*peak(head_l, 345, 2.0)
    head_r += .10*peak(head_r, 154, 2.6) + .06*peak(head_r, 319, 2.2)

    # Merging bank exhausts: collector expansion, muffler chamber and tailpipe.
    collector = head_l + head_r
    collector = (collector + .23*tap(collector, 8.8) - .19*tap(collector, 17.5)
                 + .10*tap(collector, 31.0) - .045*tap(collector, 46.0))
    exhaust = lowpass(highpass(collector, 25, 2), 2450, 3)
    # Deep rumble of the exhaust and 2nd/3rd manifold resonances.
    body = (.60*lowpass(exhaust, 180, 2) + .80*bandpass(exhaust, 56, 1450, 2)
            + .26*peak(exhaust, 96, 1.8) + .15*peak(exhaust, 208, 2.2)
            + .10*peak(exhaust, 415, 2.0))
    # A gentle nonlinear muffler overdrive generates low even/odd harmonics.
    body = np.tanh(1.23*body) / 1.23

    # Lower-frequency block piston vibration and muted intake roar.
    block = lowpass(highpass(block, 47), 720)
    intake = bandpass(collector, 145, 1200, 2)
    throttle_smoothed = lowpass(throttle, 18)
    intake *= (.06 + .12*throttle_smoothed)
    mechanical = .24*block + intake
    # Exterior stereo perspective: two uneven exhaust banks, slightly separated.
    left_bank = lowpass(highpass(head_l, 38), 1700, 2)
    right_bank = lowpass(highpass(head_r, 38), 1700, 2)
    lch = body + .14*left_bank + .052*tap(right_bank, 1.8) + mechanical
    rch = body + .14*right_bank + .052*tap(left_bank, 2.1) + mechanical
    stereo = np.column_stack((lch, rch))
    stereo = highpass(stereo, 21, 2)
    # Subtle parallel compression (no artificial broadband static layer).
    peak_amplitude = max(float(np.max(np.abs(stereo))), 1e-10)
    stereo = stereo/peak_amplitude
    stereo = .88*np.tanh(1.35*stereo)/np.tanh(1.35)
    # Fade recording edges; for idle loops, seam the periodic waveform instead.
    if not idle_only:
        fade = min(int(.075*FS), count//5)
        stereo[:fade] *= np.linspace(0, 1, fade)[:, None]
        stereo[-fade:] *= np.linspace(1, 0, fade)[:, None]
    return stereo.astype(np.float32), rpm


def create_idle_loop():
    # Starting after 1s allows pipe resonances and filter states to settle.
    x, _ = render(5.5, idle_only=True)
    # Make loop length correspond to exact whole four-stroke firing sequences:
    # 28 revolutions at exactly 840 RPM, 2 seconds.
    # 840 RPM = 14 rev/s, thus 28 revs = exactly 2.000 s = 96,000 samples.
    start = 2 * FS
    length = 2 * FS
    loop = x[start:start+length].copy()
    # RPM and four-stroke cycle divide exactly into 2 s; after filter settle
    # the first and last samples are phase-continuous without a crossfade.
    return loop


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', type=Path, default=Path(__file__).resolve().parent)
    args = parser.parse_args()
    args.out.mkdir(parents=True, exist_ok=True)
    demo, rpm = render()
    loop = create_idle_loop()
    # Match the separately normalized idle loop to the demonstration's idle level.
    target_idle = demo[int(.7*FS):int(1.7*FS)]
    loop *= np.sqrt(np.mean(target_idle**2) / max(np.mean(loop**2), 1e-12))
    demo_path = args.out/'draglab_crossplane_v8_idle_rev.wav'
    loop_path = args.out/'draglab_crossplane_v8_idle_loop.wav'
    sf.write(demo_path, demo, FS, subtype='PCM_24')
    sf.write(loop_path, loop, FS, subtype='PCM_24')
    print(f'Created {demo_path} ({len(demo)/FS:.2f}s, 48 kHz, stereo 24-bit)')
    print(f'Created {loop_path} ({len(loop)/FS:.2f}s, 48 kHz, stereo 24-bit)')
    print(f'RPM: idle ~{rpm[FS]:.0f}, peak {np.max(rpm):.0f}; V8 firing order {FIRING_ORDER}')
    for a, b, label in ((.5,1.75,'idle'),(3.6,5.6,'rev'),(8.7,10.5,'return')):
        cut = demo[int(a*FS):int(b*FS)]
        print(f'{label}: RMS {np.sqrt(np.mean(cut**2)):.3f}; peak {np.max(np.abs(cut)):.3f}')

if __name__ == '__main__':
    main()
