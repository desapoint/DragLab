# Procedural engine audio

**Settings → Master volume** is one application-wide preference, shared by racing, the engine bench and concert mode. It starts at **75%** and now scales the final generated PCM once, after saturation. The earlier listener-only implementation passed mute/save checks but failed an intermediate-level measurement: 25% and 75% produced nearly the same PCM. The listener stays at unity; an atomically published master feeds the DSP with an 8 ms gain ramp and exact settled mute. Access it from garage **Settings**, race pause **Audio settings**, or bench **Settings**. Changes persist in `audio-settings.json` independently of career saves. Slider release is captured before IMGUI consumes MouseUp, so drag edits save immediately. Scene changes, cars, presets, bench imports and concert restoration cannot overwrite the preference. Future nonprocedural sources must consume this same preference or route through a common master bus.

Settings also shows **Generated output** in dBFS RMS (or silent), measured from the actual post-master sample buffer. This verifies game-side level changes independently of speakers or remote audio forwarding.

Vehicle engine/exhaust/accessory gains remain acoustic authoring controls. Legacy `DriverSettings.soundVolume` is retained for old profile compatibility; bench JSON `volume` is a captured offline audition reference level. Current WAV exports and A/B/dyno snapshots capture the shared master level; historical captures retain their saved reference level for deterministic comparisons.

`engineGain` now also scales the two-step rotation pulse, fuel-hesitation and carbureted overrun color layers. They retain their authored character at the stock 0.8 setting and mute at zero; independently adjustable turbo/blower/valve/pop/mechanical layers remain separate. To change the complete mix, use Master.

Ignition/overrun pops now use a short pressure transient with a rapidly decaying, filtered turbulent burst. The former fixed inharmonic tone pair (second tone at 2.62 times the first) was a likely source of the reported cowbell sound during limiter/two-step bounce and has been removed. There is no continuous noise bed. The bench **Mix** tab exposes `exhaustPopGain` (zero mutes only pops), `exhaustPopSharpness` and `exhaustPopDecay`. Actual cut/pop timing, engine RPM and exhaust-chain filtering are retained. Matching previews and measured checks are in `reports/audio-pop-comparison/` and `reports/exhaust-pop-verification.txt`.

The engine voice is generated continuously from four-stroke combustion pulses rather than pitch-shifting one common loop. Pulse frequency is `RPM × cylinders / 120`. Exhaust harmonics, a lower bank/rumble layer and filters form the voice. Displacement strengthens the low rumble and lowers exhaust brightness; layout differentiates inline, V and crossplane voices. Load and throttle change level/brightness. RPM sets pulse and mechanical frequencies. Limiter and shift interruption attenuate combustion while induction/release layers continue independently. Continuous white noise has been removed from combustion and turbo layers. Valve air is low-pass filtered; tire feedback uses a quieter tone with a small filtered air component.

The fictional stock roster is **Juniper: 1.6 L inline four**, **Kestrel: 2.0 L turbo inline four**, **Bison: 5.0 L crossplane V8**, and **Timber: 4.2 L inline six**. These are authored game profiles, not manufacturer recordings.

Exhaust voices now suppress excessive upper harmonics across all engines. Lower bank/crank pulses are stronger, with a darker filter and more raw low pulses on carbureted and V8 engines throughout loaded high-RPM pulls. The firing frequency remains physically tied to RPM/cylinder count; the engine is not globally pitch-shifted. Turbo whistle and blower whine are quieter accessory layers. Inspector Sound **Exhaust bass gain / Exhaust brightness / Raw pulse gain** controls this balance; zero in older data selects nominal 1.

There is one continuous synthesized voice per car across idle, neutral revving and driving. Throttle excites exhaust intensity even without drivetrain load, so a V8 can roar during staging. Load/brightness blend over approximately 120 ms; level/throttle blend over approximately 45 ms. Idle level is closer to neutral/driving levels, and the raw lower pulse component remains present. This reduces the abrupt launch sound change while retaining distinct cut events. Generated V8 neutral blips and spectral/transition checks are in `reports/audio-previews/` and `reports/audio-verification.txt`.

Injection/carburetor, cam, mixture, idle-control, cold/warmup and accelerator-pump settings live in **Spec → Engine Behavior**. Audio reads real idle irregularity, transient hesitation/intake gulp and pop events to produce choppy idle, hollow induction, bogging, overrun burble and quiet mechanical clatter. A carburetor does not automatically imply an aggressive cam. Hard ignition cuts/two-step/anti-lag have discrete pops; fuel cuts and soft limiting have different event behavior. See `POWERTRAIN.md` for the mechanical controls and factory defaults.

Warm idle includes stronger low-mid combustion harmonics so the pulses remain audible on speakers with limited bass response. The mechanical ticking layer is roughly one-sixth of its previous gain. Idle-only coloration fades as RPM/throttle rise; it is disabled during two-step. Neutral revving adds throttle-driven roar, while ignition two-step adds a sharper open-throttle pulse layer and rapid exhaust events during cuts. The authored launch ignition-cut cadence defaults to 36 Hz; this is distinct from the slower RPM bounce. Soft and fuel-cut settings retain their separate behavior.

**Mix → idleBoostDb** adds adjustable combustion presence at idle, default **+6 dB**, range **−6 to +12 dB**. It blends out over the first 1,200 RPM above idle and as throttle opens; two-step bypasses it entirely. It increases the existing exhaust pulses, without raising mechanical ticking or adding white noise. Four matching warm-idle before/after WAV pairs are in `reports/idle-level-comparison/`. Their measured RMS increase is approximately 1.85–1.96 times after saturation; loaded steady revving and 3,500 RPM two-step retain their existing voice. Keyboard/concert ownership keeps Mix read-only; **Use sliders** enables sound-authoring edits, while the global Master stays editable in either mode. Bench/concert WAV exports apply their captured reference volume at the same post-synthesis stage as live master.

## In the garage

- Buy **EXHAUST** stages for sport, race and open exhaust tones. These are $250, $500 and $750 tone upgrades; they do not add horsepower.
- Buy engine stage 1 or higher to choose **Factory**, **Single turbo**, **Twin turbo**, **Single supercharger**, **Twin supercharger** or **Turbo + supercharger**. These change physical pressure, torque, HP and race results. See `INDUCTION.md` for lag, flow, heat, drive losses and tuning.
- A turbo profile exposes **Factory valve**, **Recirculated**, **Hiss**, **Chirp** and **Flutter**. Factory follows the definition. Recirculated is a short subdued whoosh, hiss is broadband discharge, chirp adds a falling whistle, and flutter uses decaying pulses.
- Supercharger profiles add a mechanically coupled whine whose pitch follows engine RPM, charger ratio and pulley setting. Blower-only setups do not trigger turbo blow-off sounds. Twin units have separate slightly offset voices; the mixed setup plays both turbo and blower layers according to their physical states.

Upgrades and valve choices save with the car and are copied into each race/last-pass record. Tune slots restore the valve setting alongside normal adjustments; purchased exhaust and chosen induction hardware remain installed.

Sound reads fixed-tick rotor speed, pressure, blower load, combustion fraction, fueling state and release/pop events from the simulation. It does not estimate boost or advance compressor physics per rendered frame. Charged turbo lifts/fully interrupted shifts trigger valve sounds even when automatic throttle remains held; ordinary unloaded neutral staging does not invent full charge. Enabled launch preload adds actual exhaust energy. Pause freezes physical pressure/heat/warmup and silences audio; garage/results/mute clear valve/pop tails. Resume rebases events and does not replay an old release or pop. Stalled/failed engines stop combustion audio.

## Development tools

The standalone **DragLab → Open Engine Sound Test Scene** bypasses the race cycle. It offers manual/fixed-RPM load tests, all engine/induction/mix settings, configurable exhaust chains and eleven repeatable scenarios with captured A/B configs and sample-exact WAV exports. It is also accessible from the Windows Garage or `tools/Open-EngineBench.ps1`. See [engine test bench](ENGINE_TEST_BENCH.md).

Open **DragLab → Vehicle Workbench**. Set engine stage, exhaust stage, induction and valve, then export the **8-second sound audition WAV**. The script runs idle, a rising-RPM pull, a shift and a throttle lift using the same controller/synth as the player. The play button opens the exported WAV in your default audio app. Workbench choices do not change a player save.

Use **Select definition in Inspector → Spec → Sound** for factory authoring:

| Field | Purpose |
| --- | --- |
| Cylinders / layout | Combustion event rate and bank/rumble signature |
| Exhaust | Factory exhaust tone |
| Spec displacement litres | Physical size and exhaust filtering; zero falls back to Sound displacement |
| Spec Induction fields | Physical stock system, boost/spool, flow, efficiency and factory valve |
| Supercharger ratio | Whine frequency relative to crank speed |
| Engine / turbo / supercharger / blow-off gain | Independent layer balances |

Validation rejects nonfinite/out-of-range parameters. Preparation initializes the existing roster once; `sound.version = 1` preserves subsequent sound authoring. Keep that version when editing profiles. Hardware is captured at race start; restart a pass after Inspector edits to hear them.

The older Sound induction/boost/spool/valve fields remain serialized for one-time migration. Once `induction.version = 2`, edit the physical **Spec → Induction** fields; those older sound fields do not control the running vehicle.

Generated examples live in `reports/audio-previews/`: stock vehicles, turbo valves, all five kit voices, hard/soft fuel/ignition two-step, injected idle, warm/cold carb idle, deeper older-car pulls, V8 neutral blips and V8 3,500 RPM two-step. Each WAV is original mono PCM, 44.1 kHz, eight seconds. The neutral-blips preview starts with three seconds of warm idle; the V8 two-step preview starts with one second of idle, then holds gas for six seconds before release. `reports/audio-verification.txt` reports bounds, RMS, steady-voice roughness, high-RPM low-band versus upper-harmonic checks, idle/two-step spectral separation and offline generation timings. `powertrain-verification.txt` covers the actual idle/thermal/fueling events. Automated checks do not replace human listening.

## Current limits

The user reported a blurping artifact in some configurations. Its cause and affected settings are not confirmed; investigation is deferred until the user's next requested change. That earlier report remains separate from the later cowbell report: the identified fixed inharmonic pop tones have now been replaced as described above. Human listening is still needed to assess the complete mix.

These are original procedural approximations. Flutter is a sound style rather than a surge/damage simulation. Cylinder order, cam overlap and exhaust geometry are approximated through layout/harmonics/controls; exact firing orders, valve-timing maps, measured intake/exhaust resonators, manufacturer recordings and spatial opponent audio remain future refinements. Human listening and Android DSP profiling are still needed.

The DSP thread boundary follows Unity's [OnAudioFilterRead API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/MonoBehaviour.OnAudioFilterRead.html); the clock clip uses [AudioClip.Create](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioClip.Create.html).
