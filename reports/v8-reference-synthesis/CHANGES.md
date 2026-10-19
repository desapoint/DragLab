# Old V8 reference-led synthesis preview

Checkpoint: 2026-10-19. Offline audition only; listening acceptance is pending.

## Reference and scope

At the user's request, yt-dlp 2026.08.19 downloaded only 0-30 seconds of
[HonJazzz's V8 idle recording](https://www.youtube.com/watch?v=2CJ1jBMwR38).
The uploader describes a 1966 Chevy Nova with a carbureted Chevrolet 350 small-block.
That identification is uploader attribution, not independently verified hardware.
The excerpt cannot establish exact RPM, cam, exhaust geometry or microphone response.

Extraction used `--download-sections '*0-30' --extract-audio --audio-format wav`
with ffmpeg. The analysis input is exactly 30 seconds, mono PCM16 at 24 kHz;
its SHA-256 is `0fc18b74d26e43e18efb8f0eded4500d299cb466ab717246c14c585aa6c91872`.
Raw audio and yt-dlp metadata remain local and ignored. The generator uses no
reference waveform, sampled impulses, phase or amplitude envelope.

## Findings and prototype

The excerpt changes level and spectrum, especially around the opening and later
seconds. Measurements target the steadier 5-20 second interval; the page retains
the complete excerpt. The combined comparison plays reference 5-17 seconds,
then 12 seconds of generated idle, separated by 0.7 seconds of silence.

Under the audition's hypothetical 5.7 L / 750 RPM control trace, the unmodified
production synth concentrates about 84% of its 10-6000 Hz energy below 40 Hz.
The reference's steadier interval concentrates about 71% in 80-160 Hz. Merely
lowering the voice would not reproduce that textured exhaust body.

The alternative generates individual combustion pressure events into two banks,
with cylinder strength variation, small timing variation, unequal bank pickup,
generated turbulence and damped pipe resonances. Firing frequency follows RPM;
load changes pulse strength without changing event times. Private seeded audio
randomness does not touch the game or simulation random streams.

The model uses firing order 1-8-4-3-6-5-7-2, documented for Chevrolet's
[SP350/385 small-block](https://www.chevrolet.com/content/dam/chevrolet/na/us/english/index/performance/powertrain/performance-parts/installation-guides/01-images/19417631-SP350-385-Base-i-Sheet.pdf).
750 RPM and resonator parameters are modelling choices, not measurements of this car.
The gentle neutral-rev preview is an extrapolation, not a reference-matched rev recording.

The coarse normalized frequency-band L1 error drops from 1.7480 to 0.2108;
the 20 ms envelope coefficient of variation is 0.2495 for the steadier reference
and 0.2570 for the prototype. These support an audition, not a claim of perceptual
equivalence. Listening approval remains necessary.

## Verification and limits

- Final render: `reports/v8-youtube-reference-20261018-f/index.html`.
- Exact 30-second input; finite exports, matched 0.12 RMS playback, no clipping.
- Same private seed reproduces identical samples; another seed changes the output.
- 1503 idle firing events; eight-cylinder sequence and balanced bank counts checked.
- Increased load strengthens the signal while preserving firing times and order.
- Coarse spectral-error improvement gate passed against the steadier reference.
- Production source and vehicle-asset SHA-256 values remained unchanged.
- Python syntax check passed; standalone unmodified C# DSP baseline compiled and ran.
- No game audio changed and no Unity build was needed for this tooling-only checkpoint.

This is an offline FFT-convolution prototype. Real-time callback cost and Android
performance have not been validated. If the character is accepted, a separate
increment should implement causal resonators/event excitation with simulation-owned
RPM, cylinder contributions and cut events, then profile the callback. It must not
invent limiter or misfire events merely to create a rougher sound.

## Reproduction

Requires Python with NumPy/Pillow, .NET 10, and a local PCM16 30-second reference.
The tool intentionally refuses to overwrite an earlier audition folder.

```powershell
python tools/audio-audition-matrix/reference-pressure-preview.py `
  --reference reports/v8-youtube-reference-20261018-a/reference-analysis.wav `
  --output reports/v8-youtube-reference-20261018-next
```

The JSON profile holds the acoustic parameters. Tools, configuration and concise
evidence are versioned; generated C# projects, WAVs, HTML, plots and detailed event
logs are retained locally under the ignored audition directories.
