# Cel-shaded shifter — 2026-10-23

The manual and sequential knobs now use a dark ink outline and distinct flat light, midtone and shadow regions instead of a continuous radial gradient. A hard upper-left highlight, divided metal shaft tones and boot folds reinforce the same light direction. All colors derive from the existing gauge face/rim/tick palette; the H-pattern red accent remains.

Only painting in NativeRaceShifter.cs changes. Gate/pivot coordinates, selector width, text/mobile hint layout, transmission-specific behavior, real shift handover, hit areas, clutch requirements and independently held gas remain unchanged. No audio, vehicle physics or save-system implementation changes belong to this visual increment.

## Checks and review

Packaged Windows HUD **270/0** and keyboard/mobile flick **102/0** pass: **372 checks, zero runtime errors**. These retain transmission-specific presentation, real gate/timing animation, absent plus/minus buttons, held gas across shifts and independently owned touches. HUD coverage also retains race completion, durable save failure/retry, post-finish audio and SQLite reload checks. No test assertions were changed for this visual pass.

Windows and Android ARM64 IL2CPP Development builds pass. APK signature, 16 KB alignment, ZIP integrity and native library checks pass; [exact hashes](build-manifest.txt). Physical phone input/visual acceptance is still pending. Existing Unity build warnings are retained in local logs; no new compiler or runtime error occurred.

The H-pattern, sequential and mobile captures were visually reviewed. Local comparison-h-pattern.png and comparison-sequential.png juxtapose matching shifter regions. Retained before captures are the exact preceding shifter-renderer checkpoint, not a fresh deterministic full-game comparison; no whole-scene image-parity claim is made. Fresh full screenshots are under reports/interface-screenshots, with alternate resolutions under resolution-tests.

Review the sharper knob shadows/highlight in both H-pattern rows and the sequential selector; on mobile review the number and flick hint during movement. Passing input/layout checks is not human visual or physical-phone acceptance.

The separately committed [save/state/memory plan](../../docs/SAVE_STATE_REFACTOR_PLAN.md) remains review-only; none of its steps or agent assignments are executed by this work.
