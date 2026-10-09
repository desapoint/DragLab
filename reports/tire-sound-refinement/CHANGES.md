# Textured rubber screech — 2026-10-09

Tire slip now has a prominent rubber screech over a small low-frequency contact texture. The original four wavering pure-tone voices are replaced by noise-excited rubber resonances around 1.15–1.74 kHz, with a rough upper resonance. Contact speed and intensity change their character/strength. These are authored sound-design values, not measured tire-acoustic calibration.

User audition clarified that the target is recognizable rubber-on-asphalt screech with less white-noise-like grit. The final mix removes the direct broadband scrape/hiss bed and reduces the low contact-texture gain from 0.45 to 0.12, while retaining the accepted screech. A separate smooth screech envelope prevents an abrupt waveform cut when grip returns; brief shift chirps fade naturally. The initial scrub-only candidate and the fuller gritty screech candidate are preserved as local audition variants, not the delivered sound.

Physical contact/intensity gating, wheel momentum, clutch behavior, smoke and vehicle handling are unchanged. Shared master gain still applies once. Silence/reset clears filter/envelope state; current scenarios repeat sample-exactly. DSP state is preallocated and filter coefficients are calculated per callback. No engine/induction audio, progression, thermal or asynchronous-save behavior changes.

## Audition measurements

Four-second tire-only scenarios use identical authored two-wheel contact inputs. The matrix includes the original, scrub-only, gritty-screech and delivered mixes. Current output puts roughly 93–95% of energy in the authored 1–3.5 kHz screech band, with 0.15–0.32% above 6 kHz. The original had 13–23% above 6 kHz. This preserves the requested pitched rubber signature while limiting high-frequency hiss; spectral metrics cannot determine subjective sound quality.

The playable current previews are `clean-screech-stationary-burnout.wav` and `clean-screech-acceleration-slip.wav`; scenario outputs are `after-*.wav`. Before and intermediate raw media remain local and ignored. No raw audio is a required fixture for the checks: the gate generates all current auditions, and optional historical WAVs only add comparison rows.

## Checks actually run

- **32 DSP checks**: 44.1/48 kHz, mono/stereo, finite bounded output, audible burnout/skid distinction, intensity response, chirp attenuation, grip-recovery waveform continuity, sample-exact callback-size independence and reset.
- **39 retained contact/DSP/master/pool/export checks**, rerun into this report directory without overwriting earlier tire-feedback acceptance. Current mixed engine/tire burnout WAV repeats exactly and has output headroom.
- **12 spectral checks**: a prominent 1–3.5 kHz screech band, bounded >6 kHz hiss, and no single 27 Hz-wide band monopolizing the output, across stationary burnout, acceleration slip, locked braking and shift chirp.
- Final Windows build passed. Native FWD/RWD/AWD contact audio/smoke/pause/reset passed **22 checks** (`tire-screech-final-physical`); native acoustic preview/master/mute/pause/reset passed **6 checks** (`tire-screech-final-acoustic`). Both report zero runtime/GUI/DSP errors.

**111 focused checks passed.** All final native runs use the mix with the broadband hiss bed removed and the low contact-texture gain reduced.

Full Slice 2/3 matrices and asynchronous-save acceptance remain historical baselines, not rerun for this focused audio change. Existing phase ZIPs and manually copied player folders are unchanged. Launch the current `Game/Builds/Windows/DragLab.exe` to use this revision.

An initial chirp check required near-zero amplitude before the existing release envelope had decayed; the check was corrected to require at least 40 dB attenuation within one second of regained grip. The later screech envelope also has a separate waveform-continuity check. Only passing final gates/native runs are acceptance evidence.

Reproduce with `Invoke-Unity.ps1 -Action TireSoundVerify`, then `Analyze-TireAudio.py reports/tire-sound-refinement --check` using NumPy (available in bundled workspace Python). `TireSoundBaseline` optionally captures a pre-edit baseline and refuses to overwrite existing before WAVs. Rebuild with `BuildPlayerOnly`; use fresh fixture names for `Test-WindowsSlice.ps1 -TireFeedbackOnly -ShowPlayer` and `-EngineBenchOnly -TireFeedbackOnly -ShowPlayer`. Raw auditions, screenshots and logs stay local; concise results and the matrix are versioned here.
