"""DragLab cross-plane V8 + single turbo: idle, unloaded rev, loaded pull.

Original procedural synthesis, no recordings or sample libraries.
Physics-inspired, not a CFD / measured engine acoustics model.

Requirements: pip install numpy scipy soundfile
Run: python draglab_v8_turbo_generator.py --out <folder>

Components:
* 720-degree 4-stroke firing events with GM 1-8-7-2-6-5-4-3 order
* individual pressure blowdown + rarefaction waves from eight cylinders
* different propagation delays for exhaust runners, unequal bank spacing
* collectors, reflection taps, exhaust resonances and block vibration
* separately modeled turbo shaft inertia, boost pressure, blade tone, filtered
  compressor airflow and an actual pressure-dependent throttle-lift bypass sound
* distinct neutral (low exhaust energy/low boost) and loaded acceleration

All tracks share the same fixed output calibration. No per-file normalization;
the boost/load difference is audible at matched player volume.
"""
import argparse
from pathlib import Path
import numpy as np
import soundfile as sf
from scipy import signal
from scipy.interpolate import PchipInterpolator

FS = 48000
SEED = 171818
FIRING_ORDER = (1, 8, 7, 2, 6, 5, 4, 3)
LEFT = {1, 3, 5, 7}
RUNNERS_M = {1: .50, 2: .61, 3: .43, 4: .49,
             5: .57, 6: .46, 7: .38, 8: .55}
CYL_GAIN = {1: 1., 2: .97, 3: 1.03, 4: .98,
            5: 1.02, 6: 1., 7: .96, 8: 1.04}


def filter_signal(x, cutoff, kind='lowpass', order=2):
    return signal.sosfilt(signal.butter(order, cutoff, btype=kind, fs=FS, output='sos'), x, axis=0)


def lowpass(x, freq, order=2):
    return filter_signal(x, freq, 'lowpass', order)


def highpass(x, freq, order=2):
    return filter_signal(x, freq, 'highpass', order)


def bandpass(x, lo, hi, order=2):
    return filter_signal(x, [lo, hi], 'bandpass', order)


def peak(x, freq, q):
    b, a = signal.iirpeak(freq, q, fs=FS)
    return signal.lfilter(b, a, x)


def tap(x, ms):
    n = round(FS * ms / 1000)
    return np.pad(x, (n, 0))[:len(x)]


def interpolate(t, points_t, points_val):
    return PchipInterpolator(points_t, points_val)(np.clip(t, points_t[0], points_t[-1]))


def profile(mode):
    if mode == 'idle':
        dur = 5.5
        t = np.arange(round(dur * FS)) / FS
        return t, np.full_like(t, 840.), np.full_like(t, .12), np.full_like(t, .04)
    if mode == 'neutral':
        sec = [0, .7, 1.45, 1.72, 2.04, 2.37, 2.75, 3.28, 3.88, 4.33,
               4.50, 4.72, 5.15, 5.65, 6.25, 6.95, 7.8, 8.75, 9.25]
        rpm_pts = [840, 835, 840, 860, 1110, 1950, 3010, 4480, 5650, 6250,
                   6060, 5320, 3650, 2050, 1170, 880, 840, 840, 840]
        thr_pts = [.12, .12, .13, .21, .78, 1., 1., .95, .94, .86,
                   .45, .035, .025, .02, .045, .11, .12, .12, .12]
        load_pts = [.04, .04, .04, .04, .055, .075, .095, .105, .11, .11,
                    .07, .025, .025, .025, .03, .04, .04, .04, .04]
    elif mode == 'loaded':
        sec = [0, .7, 1.45, 1.78, 2.10, 2.70, 3.35, 4.15, 5.10, 6.15,
               7.25, 8.32, 9.20, 9.65, 9.85, 10.18, 10.78, 11.45, 12.4, 13.0]
        rpm_pts = [840, 835, 840, 880, 1110, 1660, 2340, 3100, 3950, 4750,
                   5520, 6090, 6460, 6470, 6310, 5730, 4340, 2780, 1180, 840]
        thr_pts = [.12, .12, .13, .31, .86, 1., 1., 1., 1., 1.,
                   1., .98, .96, .94, .25, .045, .025, .025, .05, .12]
        load_pts = [.04, .04, .05, .19, .47, .75, .86, .92, .96, .97,
                    .97, .96, .95, .93, .45, .12, .06, .045, .045, .04]
    else:
        raise ValueError(mode)
    t = np.arange(round(sec[-1] * FS)) / FS
    rpm = interpolate(t, sec, rpm_pts)
    throttle = np.clip(interpolate(t, sec, thr_pts), 0, 1)
    load = np.clip(interpolate(t, sec, load_pts), 0, 1)
    # Mild idle rotation-speed variation; disappear rapidly as inertia dominates.
    idle_weight = np.clip((rpm - 1550) / -700, 0, 1)
    rpm += idle_weight * (7.5 * np.sin(2*np.pi*2.4*t) + 3.8*np.sin(2*np.pi*5.1*t))
    return t, rpm, throttle, load


def turbo_state(t, rpm, throttle, load, idle=False):
    """Turbo shaft speed and manifold pressure from exhaust-energy-driven dynamics.

    Integrate at 1 kHz and interpolate states onto the audio grid; no 48kHz
    Python control loops. Neutral revs have low exhaust enthalpy and little boost.
    """
    if idle:
        # Exact harmonic periodicity makes the 2s idle loop continuous.
        return np.full_like(t, .028), np.full_like(t, .009)
    ctrl_dt = .001
    ct = np.arange(0, t[-1] + ctrl_dt, ctrl_dt)
    rc = np.interp(ct, t, rpm)
    tc = np.interp(ct, t, throttle)
    lc = np.interp(ct, t, load)
    shaft = np.empty_like(ct)
    shaft[0] = .028
    press = np.empty_like(ct)
    press[0] = .009
    s, b = .028, .009
    for k in range(len(ct)):
        ex_energy = (np.clip((rc[k] - 700)/5600, 0, 1) ** .72)
        # Even at 6500 RPM a free rev has far less exhaust energy under load.
        target = .028 + tc[k] * ex_energy * (.09 + .95*lc[k]**.72)
        target = np.clip(target, .025, 1.)
        response = .70 if target >= s else .46
        s += (target-s) * ctrl_dt / response
        # Compressor is mostly bypassed below its effective shaft threshold.
        max_bar = 1.10
        p_target = max_bar * np.clip((s-.24)/.73, 0, 1)**1.58
        # At low load the compressor cannot sustain a large manifold pressure.
        p_target *= np.clip((lc[k]-.015)/.83, 0, 1)
        # On lift the bypass vents the charged air, so pressure collapses fast.
        if tc[k] < .16:
            p_target = 0.
        pressure_tau = .13 if p_target > b else .075
        b += (p_target-b) * ctrl_dt / pressure_tau
        shaft[k] = s
        press[k] = b
    return np.interp(t, ct, shaft), np.interp(t, ct, press)


def exhaust_synthesis(t, rpm, throttle, load, boost, mode, rng):
    n = len(t)
    firing_phase = np.cumsum(rpm * 4 / (60*FS))
    event_counts = np.floor(firing_phase).astype(np.int64)
    event_idx = np.flatnonzero(np.diff(event_counts)) + 1
    bank_l = np.zeros(n)
    bank_r = np.zeros(n)
    block = np.zeros(n)
    kernel_t = np.arange(round(.028*FS)) / FS
    # The per-cylinder pressure waves (not a global harmonic oscillator).
    for idx in event_idx:
        cylinder = FIRING_ORDER[(int(event_counts[idx])-1) % 8]
        denom = firing_phase[idx] - firing_phase[idx-1]
        fraction = (event_counts[idx] - firing_phase[idx-1]) / denom
        start = (idx-1) + fraction
        r, a, ld, b = rpm[idx], throttle[idx], load[idx], boost[idx]
        sound_speed = 365 + 75*a + 35*b
        start += RUNNERS_M[cylinder] / sound_speed * FS
        onset = int(start)
        if onset >= n: continue
        frac = start-onset
        scale = np.clip(1.17 - .075*np.log2(max(r,750)/850) - .10*a - .06*ld, .74, 1.2)
        x = kernel_t / scale
        pressure = ((1-np.exp(-x/.00020))*np.exp(-x/.0030)
                    - .28*(1-np.exp(-x/.00145))*np.exp(-x/.0092))
        pressure += (.07 + .08*ld)*a*(1-np.exp(-x/.00012))*np.exp(-x/.00085)
        # Turbo load expands the blowdown and adds strong lower-mid pressure.
        amplitude = (.74 + .56*a + .46*ld + .38*b) * (max(r,800)/850)**(-.31)
        pressure *= amplitude * CYL_GAIN[cylinder]
        if mode != 'idle':
            pressure *= 1 + rng.normal(0, .009 if r > 1500 else .023)
        end = min(n, onset + len(pressure))
        length = end - onset
        if length <= 1: continue
        pulse = (1-frac)*pressure[:length] + frac*np.concatenate(([0.],pressure))[:length]
        bank = bank_l if cylinder in LEFT else bank_r
        bank[onset:end] += pulse
        short_n = min(length, round(.018*FS))
        st = kernel_t[:short_n]
        mechanical = ((1-np.exp(-st/.00033))*np.exp(-st/.006) *
                      (np.sin(2*np.pi*118*st) + .23*np.sin(2*np.pi*270*st)))
        block[onset:onset+short_n] += .21*mechanical*CYL_GAIN[cylinder]

    head_l = bank_l + .36*tap(bank_l, 3.1) - .22*tap(bank_l, 6.5) + .12*tap(bank_l, 11.2)
    head_r = bank_r + .33*tap(bank_r, 3.9) - .20*tap(bank_r, 8.1) + .11*tap(bank_r, 13.0)
    head_l += .09*peak(head_l, 167, 2.8) + .08*peak(head_l, 345, 2.0)
    head_r += .10*peak(head_r, 154, 2.6) + .06*peak(head_r, 319, 2.2)
    collector = head_l + head_r
    collector = (collector + .23*tap(collector, 8.8) - .19*tap(collector, 17.5)
                 + .10*tap(collector, 31.0) - .045*tap(collector, 46.0))

    # Turbo hot-side mass damps sharp transients and adds lower-mid resonances.
    raw_exhaust = lowpass(highpass(collector, 25), 2650, 3)
    boosted_muffler = lowpass(raw_exhaust, 1450, 2)
    compressor_backpressure = np.clip(boost/1.1, 0, 1)**.7
    exhaust = raw_exhaust * (1-.18*compressor_backpressure) + .18*boosted_muffler*compressor_backpressure
    body = (.60*lowpass(exhaust, 180) + .80*bandpass(exhaust, 56, 1450)
            + .26*peak(exhaust, 96, 1.8) + .15*peak(exhaust, 208, 2.2)
            + .10*peak(exhaust, 415, 2.0))
    body = np.tanh(1.15*body) / 1.15
    block = lowpass(highpass(block, 47), 720)
    intake = bandpass(collector, 145, 1200)
    intake *= .05 + .13*lowpass(throttle, 18)
    mechanical = .22*block + intake
    left_bank = lowpass(highpass(head_l, 38), 1700)
    right_bank = lowpass(highpass(head_r, 38), 1700)
    left = body + .13*left_bank + .052*tap(right_bank, 1.8) + mechanical
    right = body + .13*right_bank + .052*tap(left_bank, 2.1) + mechanical
    return np.column_stack((left, right)), collector


def turbo_synthesis(t, rpm, throttle, load, shaft, boost, mode, rng, collector):
    n = len(t)
    # Whine follows turbo shaft inertia rather than being pitch-shifted from RPM.
    rotor_hz = (610 + 1490*shaft) if mode != 'idle' else np.full_like(t, 630.)
    rotor_phase = 2*np.pi * np.cumsum(rotor_hz/FS)
    blade = np.sin(rotor_phase) + .24*np.sin(2*rotor_phase+.35)
    # Gain and bandwidth increase with compressor load. No continuous white hiss.
    level = np.clip(boost/1.1, 0, 1)
    compressor_gain = .0035 + (.032*shaft**1.4 + .033*level)*(.30+.70*throttle)
    if mode == 'idle':
        compressor_gain = np.full_like(t, .0035)
    flutter = 1 + .09*np.sin(2*np.pi*13.2*t)*np.clip(shaft/.8, 0, 1)
    tone = blade * compressor_gain * flutter

    if mode == 'idle':
        filtered_flow = np.zeros(n)
    else:
        # Turbulence belongs to actual airflow only: high/low band limiting and
        # substantially quieter than both combustion and pressure release.
        air = rng.standard_normal(n)
        filtered_flow = bandpass(air, 700, 3900, 2)
        filtered_flow += .24*bandpass(air, 140, 760, 2)
        filtered_flow *= (.006*shaft + .017*level) * (.22+.78*throttle)
    # Downpipe's periodic response to exhaust energy; no fake siren harmonics.
    exhaust_response = lowpass(highpass(collector, 230), 680)
    turbine_body = exhaust_response * (.014 + .036*np.clip(boost/1.1,0,1))
    turbo = tone + filtered_flow + turbine_body

    # The bypass valve only blows after throttle lift with actual positive boost.
    # Make the filtered transient spatially wider than the exhaust.
    release = np.zeros(n)
    lift_mask = (throttle[1:] < .25) & (throttle[:-1] >= .25)
    for idx in np.flatnonzero(lift_mask)+1:
        pressure = float(boost[max(0,idx-1500)])
        if pressure < .025: continue
        sec = np.arange(min(int(.65*FS),n-idx))/FS
        # Fast pressure release and a short valve tail (gentle hiss, not static).
        noise = rng.standard_normal(len(sec))
        air = bandpass(noise, 440, 3700, 2)
        air += .28*bandpass(noise, 180, 1100, 2)
        envelope = (1-np.exp(-sec/.007))*np.exp(-sec/(.095+.12*np.clip(pressure,0,1)))
        burst = air * envelope * (.11*min(1,pressure/1.1))
        # Weak flutter at 25-35Hz as pressure drops.
        flutter = .025*(pressure/1.1)*envelope*np.sin(2*np.pi*(33*sec - 9*sec**2))
        release[idx:idx+len(sec)] += burst + flutter

    # Tiny L/R shift adds depth without requiring a stereo sample library.
    left = turbo + .92*release
    right = .985*tap(turbo, .24) + 1.08*tap(release, 1.35)
    return np.column_stack((left, right))


def synthesize(mode):
    t, rpm, throttle, load = profile(mode)
    rng = np.random.default_rng(SEED)
    shaft, boost = turbo_state(t, rpm, throttle, load, mode == 'idle')
    exhaust, collector = exhaust_synthesis(t, rpm, throttle, load, boost, mode, rng)
    turbo = turbo_synthesis(t, rpm, throttle, load, shaft, boost, mode, rng, collector)

    # Fixed shared calibration. Unlike independent normalization, actual changes
    # in exhaust energy and compressor pressure are preserved in A/B listening.
    stereo = highpass(exhaust*.38 + turbo*.95, 21)
    stereo = np.tanh(stereo*2.55)*.86
    if mode != 'idle':
        fade = int(.065*FS)
        stereo[:fade] *= np.linspace(0,1,fade)[:,None]
        stereo[-fade:] *= np.linspace(1,0,fade)[:,None]
    return stereo.astype(np.float32), rpm, boost, shaft


def make_idle_loop():
    # 840 rpm = 14 crank rotations/s -> 2 seconds includes 28 revolutions.
    # Loop after initial filter/transient settling.
    sound, _, _, _ = synthesize('idle')
    start = 2*FS
    return sound[start:start+2*FS].copy()


def rms(samples):
    return float(np.sqrt(np.mean(np.square(samples.astype(np.float64)))))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', type=Path, default=Path(__file__).resolve().parent)
    args = parser.parse_args()
    args.out.mkdir(exist_ok=True, parents=True)
    configs = [
        ('neutral','draglab_v8_turbo_free_rev.wav'),
        ('loaded','draglab_v8_turbo_loaded_pull.wav')
    ]
    sounds = {}
    for mode,name in configs:
        sound,rpm,boost,shaft = synthesize(mode)
        sounds[mode] = sound
        sf.write(args.out/name, sound, FS, subtype='PCM_24')
        print(f'{name}: {len(sound)/FS:.2f}s; maxRPM {max(rpm):.0f}; peak boost {max(boost):.3f}bar; '+
              f'RMS {rms(sound):.3f}, peak {np.max(np.abs(sound)):.3f}')
    idle = make_idle_loop()
    sf.write(args.out/'draglab_v8_turbo_idle_loop.wav', idle, FS, subtype='PCM_24')
    print(f'draglab_v8_turbo_idle_loop.wav: {len(idle)/FS:.2f}s, RMS {rms(idle):.3f}, seam delta {np.max(np.abs(idle[0]-idle[-1])):.5f}')
    # Comparison with 0.55s silent interval, free rev first, loaded second.
    silence=np.zeros((int(.55*FS),2),dtype=np.float32)
    ab = np.concatenate((sounds['neutral'],silence,sounds['loaded']))
    sf.write(args.out/'draglab_v8_turbo_free_vs_loaded.wav',ab,FS,subtype='PCM_24')
    print('draglab_v8_turbo_free_vs_loaded.wav: neutral rev -> 0.55s gap -> loaded pull')
    print('All outputs stereo PCM_24 at 48 kHz (same gain calibration; no normalization per clip)')

if __name__ == '__main__':
    main()
