# Generated sound auditions

Original procedural PCM, 44.1 kHz mono, eight seconds each. Each sample follows idle → pull → shift → lift, advancing the same physical induction model used in races. No manufacturer recordings or third-party samples are used. The same controller and sample generator run in the Windows game. Regenerate with Verify Foundation; use the Vehicle Workbench for other specs/upgrades.

| Stock engine | Audition |
| --- | --- |
| 1.6 L inline four | [Juniper](starter-hatch.wav) |
| 2.0 L turbo inline four | [Kestrel](sport-coupe.wav) |
| 4.2 L inline six | [Timber](utility-pickup.wav) |
| 5.0 L crossplane V8 | [Bison](v8-muscle.wav) |

Turbo examples use the same 1.6 L inline-four engine stage and change only the valve: [Recirculated](turbo-recirculated.wav), [Hiss](turbo-hiss.wav), [Chirp](turbo-chirp.wav), [Flutter](turbo-flutter.wav). The releases occur at the shift around four seconds and lift at six seconds.

[Supercharger whine](supercharger.wav) uses the same engine-stage build with a supercharger kit. Its whine follows crank RPM; there is no turbo-valve release.

Compare the five setups on the same engine-stage build: [single turbo](kit-turbo.wav), [twin turbo](kit-twinturbo.wav), [single supercharger](kit-supercharger.wav), [twin supercharger](kit-twinsupercharger.wav) and [turbo plus supercharger](kit-twincharged.wav). Continuous combustion/turbo white noise has been removed; valve air is filtered.

See [engine sound authoring](../../docs/ENGINE_AUDIO.md), [physical induction](../../docs/INDUCTION.md) and [measured checks](../audio-verification.txt). Listening quality still needs human review; automated tests check differences, event behavior, signal bounds, steady-waveform roughness and silence.

## Idle, neutral throttle and two-step

[V8 neutral blips](v8-neutral-blips.wav) starts with **three seconds of warm idle**, followed by throttle blips at three and five seconds. The opening should have audible deep combustion pulses with subdued mechanical ticking. [V8 3,500 RPM ignition two-step](v8-two-step-3500.wav) starts with **one second of warm idle**, holds throttle for six seconds, then releases. Both advance the actual car simulation and shared audio generator.

Compare generic launch settings: [hard fuel cut](two-step-hard-fuel.wav), [hard ignition cut](two-step-hard-ignition.wav), [soft fuel limiting](two-step-soft-fuel.wav), [soft ignition limiting](two-step-soft-ignition.wav). Ignition cuts create rapid pop events; fuel cuts and soft limiting have different behavior.

Fueling examples: [warm carb idle](carb-warm.wav), [cold carb idle](carb-cold.wav), [injected idle](injection-idle.wav). Deep high-RPM examples: [V8 pull](deep-pull-v8-muscle.wav), [carb pickup pull](deep-pull-utility-pickup.wav). See [powertrain mechanics](../../docs/POWERTRAIN.md) for factory fueling, launch-control and gearbox choices.
