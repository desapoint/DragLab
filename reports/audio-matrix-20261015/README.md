# Full engine before/after listening matrix — 2026-10-15

**Subsequent approval:** the user accepted the After voices and requested their game integration. [Integration, checks and build](../audio-integration-20261015/CHANGES.md). The preview-generation record below remains historical; the library retains the reviewed Before/After reference.

**Preview checkpoint only; no game integration or listening acceptance.** Based on `main` at `8761bfc`. All game sources, assets, bench interfaces, settings, saves and the current Windows build remain unchanged. Unrelated user work remains untouched.

Open the local `index.html` in a browser. It provides engine/cylinder/layout/fuel/delivery/induction/exhaust/size/pulley/boost/valve filters, search, factory-only selection and Before / After / full A/B playback. Factory configurations appear first. The optional laboratory switch reveals counts not currently offered by cars. Download/copy `listening-matrix.zip` and extract it to keep the viewer and media together. `highlights/` contains WAV A/B files for all 13 actual factory configurations.

Every file: **before 0–10 seconds; after 10.7–20.7 seconds**. Each half contains idle 0–1.5 s, neutral revving 1.5–3.5 s, loaded acceleration 3.5–8 s and lift 8–10 s. Both halves receive the same scripted input trajectory. These are not live race recordings or full simulation/fitment verification.

## Coverage

| Group | Cases |
| --- | ---: |
| Every authored engine/fueling variant × six induction layouts × factory exhaust/four presets | 510 |
| Compact/street/large turbo and all four applicable release types, on each engine/fueling variant | 507 additional |
| Pulley 0.85 / fixed-street 0.90 / 1.00 / 1.20, with other controls held fixed | 153 additional |
| Boost calibration 0.80 / 1.00 / 1.20, with other controls held fixed | 102 additional |
| Experimental 1–16 cylinder sweep × carburetor / injection / diesel | 48 |
| Additional V and crossplane layout references | 6 |
| **Total labelled before/after cases** | **1,326** |

The 13 actual definitions cover Sprig 1.2; Juniper 1.4 Economy, 1.6 and 1.6 Club; Kestrel 2.0T and Track; Tempest AWD 2.0T; Meridian GT 3.0; Timber Utility 4.2; Bison 5.0 and R 5.0 SC; Timber 2.8D Work and Tow. Actual cylinder counts are 3, 4, 6 and 8. The six induction layouts are naturally aspirated, single turbo, twin turbo, supercharger, twin supercharger and twincharged.

Fuel kinds remain petrol and diesel. The two carbureted engines also receive both available TBI and port-EFI conversion examples. Their healthy scripted sound may be identical; labelled cases remain present and exact duplicate pairs share files. All matrix cases retain their metadata in the versioned `coverage-matrix.csv`. There are **836 unique A/B files**, totaling 363,798,321 bytes of lossless FLAC; the complete ZIP is approximately 366 MB.

All requested discrete axes are represented. Continuous tuning uses the stated reference/end values. The focused sweeps are not a Cartesian product of every size, pulley, boost and exhaust value. Laboratory profiles are not buyable engines or validated mechanical configurations. Injection subtype, octane/cetane and supercharger rotor families without a current distinct audio model do not receive invented sound differences. No unrelated fuel types are introduced.

## Proposed voice

The stronger low bank growl is limited to crossplane V8s. Other families retain their core sound. Healthy petrol mechanical ticking is reduced and softened; diesel retains its authored mechanical clatter. Small private seeded amplitude variation and restrained compressor tone/air texture are auditioned. Existing release-valve voicing remains the before/after reference, rather than reusing the rejected bright prototype. Added supercharger third harmonics have a sample-rate guard. These are new listening drafts, not approved production voices.

This checkpoint does not add oil-pressure-, wear-, knock- or individual-valve-driven mechanical sound. Those simulation-to-audio links remain separate work requiring explicit scope/approval. The profile adapter captures stock audio parameters and audition hardware overrides; the source before-DSP itself is extracted unchanged. Spool/boost trajectories use authored scales under scripted load rather than calling the complete authoritative race simulation. Comparisons concern sound character, not whether a hypothetical engine build survives a pass.

## Checks actually completed

- Standalone .NET Release renderer: **1,326 finite bounded before/after pairs**. Repeated/different private seed checks pass for every hundredth case.
- Coverage assertions: all 13 authored definitions, every valid fueling mode, all six induction layouts/five exhaust selections, 13 exact factory references, diesel release exclusions and counts 1–16 pass.
- Packaging: full raw dimensions, finite samples, clipping headroom and constant-gain −20 LUFS target / −3 dBTP ceiling pass. Maximum measured A/B level difference: **0.00 LUFS**.
- All **836 unique FLAC files** decoded and compared sample-for-sample with the packaged 16-bit PCM; hashes recorded. Identical pairs are deduplicated without removing cases.
- Source hashes for DSP, hardware definitions and all vehicle assets match; no `Game/` diff or untracked asset exists.
- Viewer JavaScript: **12 in-memory logic checks** pass, covering filtering, factory references, hidden laboratory cases, empty results, paging and segment controls. The test's number parsing was corrected to accept the workstation's locale grouping separators; viewer behavior did not require a change for that fixture issue.
- Python source compilation and scoped Git whitespace checks pass. No Unity/game build or production callback-performance claim is made.

The browser tool rejected the local `file:` protocol, so actual browser rendering/playback was not checked through that tool; no workaround was used. Media was validated directly and the viewer logic was checked without a browser or audio device. Human listening remains the acceptance gate.

Reproducible source: `tools/audio-audition-matrix/` (no dependency on earlier ignored preview projects). Generated audio, raw buffers, copied DSP, manifests and ZIP remain local ignored artifacts. The concise report, full case CSV and tooling are versioned. Current game sound remains in place until explicit approval and a separately requested integration checkpoint.
