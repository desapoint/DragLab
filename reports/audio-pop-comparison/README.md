# Exhaust pop comparison

The before files were copied from the last verified flywheel build before replacing the pop generator. The after files use the same physics, synth/controller, scenarios and exhaust settings with the new pressure-burst pop layer. No manufacturer samples are used.

| Matched eight-second scenario | Before | After |
| --- | --- | --- |
| V8, 3500 RPM hard ignition two-step | [Pitched pair](before-v8-two-step-3500.wav) | [Pressure bursts](after-v8-two-step-3500.wav) |
| Generic four-cylinder hard ignition two-step | [Pitched pair](before-four-two-step.wav) | [Pressure bursts](after-four-two-step.wav) |

`after-two-step-*.wav` additionally provides full twelve-second bench two-step renders for all four stock engines, with matching configuration JSON. These are separate scenarios, not direct duration-matched before/after files.

The old effect combined decaying sine tones at 340/520 Hz and 2.62 times that frequency during two-step, or 145/379.9 Hz for other pops. The new generator uses a short pressure pulse and a rapidly decaying, filtered turbulent burst. It has no fixed pitched pair or continuous noise bed. Engine size, exhaust chain and the Mix tab's `exhaustPopGain`, `exhaustPopSharpness`, `exhaustPopDecay` controls shape the result. Setting pop gain to zero isolates the reported layer without changing engine physics or cut events.

`../exhaust-pop-verification.txt` compares normalized concentration at the former two-step partials: four-cylinder 0.12704 → 0.00024; V8 0.12728 → 0.00170. It also checks bounded/reproducible events, finite tails, mute/sharpness/decay controls, 48 kHz operation and all four bench exports. These signal checks establish a changed effect; human listening determines whether any remaining artifact is present.
