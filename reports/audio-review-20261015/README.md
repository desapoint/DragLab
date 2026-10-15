# Listening pack — proposals, not installed sounds

**First draft rejected by the user:** the new engine voices sounded brassy/trumpet-like and shifted too much energy upward. These files remain historical auditions, not an accepted direction. Use the [revised V8 comparisons](revision-2/README.md) for the current listening checkpoint. All other proposed voices remain on hold.

Game code/assets/build are unchanged. Listen at a comfortable fixed volume. These are original procedural drafts using scripted RPM/load, not real-car recordings or finalized in-game mixes. Findings and the approval boundary are in [the audit](REPORT.md).

For each **A/B** clip: **0–8 s current algorithm → 0.7 s silence → 8.7–16.7 s proposed concept**. A/B pairs are loudness-matched using constant gain, targeting −20 LUFS with a −3 dBTP ceiling. The first two compare V8 character; turbo/supercharger clips include engine plus induction.

| Clip | What to compare |
| --- | --- |
| [01 — V8 idle and neutral blips](01-v8-idle-rev-AB.wav) | Idle texture, pulse character and throttle blips |
| [02 — V8 loaded acceleration](02-v8-acceleration-AB.wav) | Loaded growl, low-mid body and lift-off |
| [03 — Turbo spool and release](03-turbo-spool-release-AB.wav) | Whistle/air balance, spool progression and lift release |
| [04 — Supercharged pull](04-supercharger-AB.wav) | Engine body with mechanically related whine |
| [05 — Five exhaust concepts](05-exhaust-five-voices.wav) | Same V8/input, different exhaust character; timing below |
| [06 — Subtle variation](06-subtle-variation.wav) | Same idle/blip script repeated without variation, then seeds A and B |
| [07 — Other engine families](07-engine-families.wav) | Inline-three, inline-four, inline-six, diesel concepts |
| [08 — Isolated turbo](08-turbo-isolated.wav) | Proposed induction texture alone |
| [09 — Isolated supercharger](09-supercharger-isolated.wav) | Proposed whine alone |

Clip 05: **0–6 s quiet OEM; 6.7–12.7 s chambered muscle; 13.4–19.4 s resonated sport; 20.1–26.1 s straight-through; 26.8–32.8 s open headers.** Each segment is independently loudness-matched to expose tone rather than price/loudness hierarchy.

Clip 06: **0–8 s no subtle event variation; 8.7–16.7 s subtle seed A; 17.4–25.4 s subtle seed B.** No intentional misfires or changing physics. The off example still contains the same basic air texture.

Clip 07: **0–5 s inline-three; 5.7–10.7 s inline-four; 11.4–16.4 s inline-six; 17.1–22.1 s diesel.** These extra voices are early sketches, not final calibration.

Clips 08/09 are independently normalized for audition and will sound more prominent than their intended level within a full engine mix. Measurements, source hashes and uncompressed raw drafts are local artifacts. [Download the WAV pack](listening-pack.zip).

Feedback needed: whether the proposed V8 is closer to the desired growl, preferred exhaust character, whether induction is too tonal/noisy/prominent, and whether the variation is subtle enough. **Nothing is installed until approved.**
