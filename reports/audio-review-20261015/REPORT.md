# Engine and induction sound audit — 2026-10-15

**Review only. No game implementation, asset, build, profile, setting or bench change.** Reviewed `main` at `6a94977`. The proposed voices below are standalone listening drafts and require the user's approval before integration. This does not advance another Slice 4 control.

## Findings

| Area | Current implementation | Consequence / proposed direction |
| --- | --- | --- |
| Engine character | `EngineSound.cs:228–258` builds a shared, regular harmonic stack. Crossplane layout mainly changes a rumble coefficient; extra crank/bank sine layers supply bass. Cylinder contributions gate the shared voice, without separate bank acoustics. | This can retain bass while lacking a convincing loaded V8 bark/growl. Generate combustion pressure events, route them to the correct bank, then model their merge/short pipe delays. Keep different crossplane, flat-plane and inline families. |
| Acceleration | Throttle and load already affect gain, brightness and smoothing. Large/carburated voices receive a lower source cutoff; multiple exhaust filters then soften the result. | Existing smoothing and input authority are useful. Tune the loaded pulse attack, low-mid body and controlled upper texture instead of only increasing sub-bass or gain. Distinguish unloaded revving, loaded acceleration, lift and idle. Excessive filtering is a plausible cause of the duller character; source review alone is not a listening verdict. |
| Turbo | `EngineSound.cs:275–280` uses a sine around 1,000–4,200 Hz driven by normalized spool. Twins add a nearby sine. Boost controls level. | Add restrained airflow texture and richer but bounded compressor harmonics. Different sizes/housings should have different voices and response; the current oscillator has no distinct rotor/blade acoustic identity. Initially derive audio rotor behavior from existing spool/boost/flow observations; do not invent a second authoritative induction simulation. |
| Supercharger | `EngineSound.cs:283–289` uses RPM × one sound ratio, one harmonic, and small fixed twin detuning. | Introduce hardware voice families for positive-displacement versus centrifugal devices, with rotor/lobe/blade and bypass character. Keep the whine mechanically related to actual speed and load, rather than an unrelated rising tone. |
| Exhaust variety | `ExhaustAcoustics.cs:55` provides four presets. `PartSemantics.cs:198` assigns an upgraded exhaust by stage. Other same-stage hardware can consequently share the same voice. Filters already support pipes, absorptive/chambered silencers, resonators and catalysts. | Reuse this foundation, but give individual hardware an authored acoustic identity independent of rank: quiet OEM, chambered muscle, resonated sport, straight-through, open headers, and later valved systems. Geometry, muffler style and resonator tuning should matter; a higher tier should not simply mean louder. |
| Variation | The main synth starts with the same fixed noise seed. Much of combustion modulation is periodic. Random noise is primarily used for release air; `ExhaustPopSynth` already varies pressure-pulse width and decay. | Add small event-level strength, pulse-width and timing variation plus slow correlated texture. Avoid audible pitch wobble, random musical whistles, continuous static, gratuitous pops or simulated healthy-engine misfires. Preserve the existing non-pitched pressure-pop approach rather than reintroducing the earlier bell-like artifact. |
| Integration | `EngineAudio` publishes immutable profiles/frames to DSP, uses phase-continuous smoothing and applies the master gain after saturation. | Preserve this architecture, actual cylinder failures, installed hardware, fuel/ignition-cut behavior, opponent attenuation and both shared volume controls. Use private audio RNG, not gameplay/Unity global RNG. |

The existing sound tests check signal bounds, differences, firing harmonics and low-frequency energy. Those checks are useful but **do not prove that a voice sounds like a convincing V8**. Some explicitly favor large low/high frequency ratios; retain safety checks while adding approved auditory references and checks for load transitions, bank character, limited noise, aliasing, clicks and DSP cost. Do not treat more bass as automatic realism.

## Proposed subtle variation

Starting audition values are approximately 1.4–2.8% event-strength variation, 1.2% pulse-width variation and at most about 50 microseconds of event delay. These are listening parameters, not claimed universal real-engine measurements. Slow variation is below 1%; no global pitch warble is added. Separate seeds identify cars/sessions, with a repeatable seed for auditions/replays. Variation must not change physics RPM, power, wear, fuel usage, failures or race outcome, and must not conceal an actual damaged cylinder.

## Listening drafts

[Sample index and timing guide](README.md). Nine original procedural mono PCM WAVs, 48 kHz/16 bit, generated outside `Game/`. No downloaded vehicle recordings or third-party sound assets are used.

The four A/B files play **current DSP 0–8 s, silence 8–8.7 s, proposed concept 8.7–16.7 s**. They use the same scripted RPM/throttle/load trajectory and authored stock-profile parameters. “Current” means the existing sample-generation algorithm copied unchanged into an isolated .NET harness, with a small authored-profile adapter; it is not a recording of a live race. The proposal is a separate event/bank synthesis sketch, not a patched game synth.

Comparisons use constant gain targeting **−20 LUFS with a −3 dBTP ceiling**, measured by FFmpeg loudness analysis. This is not dynamic compression; idle/load dynamics within each clip are retained. All four A/B pairs matched the measured loudness target. Isolated induction examples are independently normalized to expose character and do not represent their intended level in a full mix. No audition includes tire/opponent audio, camera acoustics or a Unity output-device/mixer chain.

Five exhaust concepts share the same V8 and driving inputs, with differences in acoustic filtering and bank-path delays. The variation sample repeats the same idle/blip sequence with no subtle event variation, then two different subtle seeds. A separate montage sketches inline-three, inline-four, inline-six and diesel families. These are first listening drafts, not final production character or a substitute for later in-game listening.

Checks actually performed: standalone renderer compiled and ran; all raw buffers finite; all nine WAVs valid mono/48 kHz and below PCM clipping; same-seed repeat produced identical raw PCM; different seeds changed the sound; all captured DSP source hashes unchanged; `git diff -- Game tools` empty. Raw data, source copies, renderer, provenance hashes and measurements remain local in this audition folder. No Unity build, broad gameplay suite or DSP-device performance claim is made for this report-only checkpoint.

## Order after approval

1. Select the V8 core character and idle/loaded contrast from the first two A/B clips. Revise samples until accepted; do not install a rejected voice.
2. Approve exhaust identities on that same engine before connecting acoustic profiles to installed part definitions. Keep acoustic family separate from stage, tuning permissions and performance ratings.
3. Approve turbo and supercharger character, then balance them against the accepted engine voice. Do not add flutter to every lift: Garrett describes blow-off valves as preventing compressor surge, so release/surge behavior must remain hardware/state-dependent. [Garrett technical reference](https://www.garrettmotion.com/it/turbocharger-technology/how-a-turbo-works/basic/)
4. Approve the subtle variation amount. Preserve deterministic auditions and independent audio RNG. Then calibrate the other engine families without cloning the same V8 bass layer everywhere.
5. Integrate only approved choices in a separate requested checkpoint. Recheck race and existing bench playback through the same generator, hardware swaps, healthy/faulted cylinders, fuel versus ignition cuts, volumes, shift/lift transitions, pause/resume, aliasing and callback allocations/CPU. The bench interface itself remains deferred.

Different supercharger rotor hardware is a substantive distinction rather than a cosmetic name: Eaton documents three- and four-lobe TVS families. This supports separate authored hardware voices, without claiming that a small synthetic preview exactly reproduces a particular manufacturer's product. [Eaton technical reference](https://www.eaton.com/id/id-id/products/engine-solutions/superchargers/tvs-overview.html)

**Approval gate:** audition generation is authorized; game integration is not. Keep the current game sound until the user selects or revises the drafts.
