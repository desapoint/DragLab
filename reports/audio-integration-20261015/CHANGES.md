# Approved engine sound integration — 2026-10-15

The user approved the matrix's After voices and explicitly requested integration. The shared `EngineSoundSynth` now uses that approved sound in races, opponent playback and existing bench/concert rendering: stronger low-bank crossplane V8 growl, quieter softened healthy-petrol clatter, retained diesel clatter, subtle private seeded pulse-strength variation, mild load saturation and restrained turbo/supercharger texture. No separate race/bench generator or new mixer/preferences is introduced.

Controller inputs, physical RPM/boost, installed hardware permissions, cylinder contributions, cut/pop/release events, volume controls, opponent attenuation, physics/replay identity, player resources and saves remain authoritative. Exhaust presets and blow-off voicing are retained. New oil/wear/fault-dependent mechanical sounds are outside this approved integration. The debug bench interface is unchanged.

Block-constant smoothing coefficients are calculated once per callback. Inactive audio clears the new texture filters to prevent stale tails; seed zero maps to a valid private seed. No gameplay/global RNG, Unity API or I/O is used by the new sample-generation code. The normal default seed retains the exact approved audition output.

## Completed checks

| Gate | Result |
| --- | --- |
| Production vs approved source and original published After buffers | **1,326 sample-exact pairs**, finite bounded output; repeat/different-seed checks pass |
| Unity engine audio | **120** checks |
| Shared volume/preferences | **7** checks |
| Exhaust pressure pops / former bell regression | **25** checks |
| Induction | **112** checks |
| Existing bench/exhaust renderer | **170** checks; all authored cars/scenarios render deterministically |
| Existing three-song engine concert | **42** checks |
| Windows `BuildPlayerOnly` | **Succeeded** |
| Packaged race/HUD/pause/finished exit | **141 checks, 0 runtime errors** |

The new Unity checks cover private seed determinism without gameplay RNG/input mutation, callback partition invariance, audible missing-cylinder contribution, petrol/diesel mechanical contrast, finite stereo twincharge at 32/44.1/48/96 kHz and allocation-free warm callbacks. Observed V8 cost was **1.387 ms per 2,048 mono samples** (42.667 ms of signal) with **zero managed bytes allocated**. This is an Editor timing observation, not a mobile performance guarantee or a universal latency bound. Shared engine/induction, existing bench and concert checks total **476**.

An initial new test-entry compile failed because `Debug` was ambiguous between the diagnostics and Unity namespaces. An explicit alias corrected it before the successful complete suite and build. Historical checks were actually rerun; broad economy/physics matrices were not rerun for this audio-only integration. A legacy bench report's fixed phrase “four cars” predates the current Resources-driven loop; the executed loop includes all 13 definitions.

Logs are local: `unity-audio-final.log`, `unity-build.log`; isolated player evidence is under `reports/player-fixtures/approved-engine-audio-20261015/`. Current audio/induction/pop/bench/concert summary reports are versioned alongside this checkpoint. The original listening matrix remains available; tooling now pins its Before source to the reviewed pre-integration Git revision so rerunning it cannot apply the sound change twice. The integration gate also pins the approved candidate hash and works without ignored PCM fixtures on a fresh checkout, comparing published buffers when present.

Updated playable build: `Game/Builds/Windows/DragLab.exe`. Unity's whitespace-only font reserialization was verified equivalent and restored. Pre-existing user report edits, `Tests.ps1` and race-exit diagnostics are preserved. No push or later interface phase is included. The user separately requested adding event/effect auditions to the HTML library next.
