# Car detail action rows — 2026-10-16

Owned-car details now use two rows: Stats + Fitted parts, then Warnings + History when history exists. Shared buttons keep ten logical pixels between buttons and between rows. The former extra eight-pixel fitted-parts bottom margin is removed.

The user then requested matching placement in the dealership: disclosed Fitted parts now sits beside Stats in the summary, rather than in the specifications list. Both pages reuse the same action-row styling. Uninspected used cars retain their existing paid-disclosure gate. Button behavior, saved assets and catalogue data are unchanged.

Final Windows development build succeeds and is installed in `Game/Builds/Windows`. Packaged native checks pass **49 checks / 0 runtime errors**, including aligned owned rows and equal gaps at 1024 and 1280 pixels, dealership placement beside Stats, grouped real fitted identities, incomplete-car behavior, unchanged saves and filtered Shop navigation. Viewed the owned 1280-pixel row capture and final dealership capture. Native captures are refreshed under `reports/interface-screenshots`; raw images, logs, fixture saves and builds stay local/ignored. Build fingerprints are recorded here.

Reproduce: `tools/Test-NativeMenu.ps1 -OwnedFittedOnly -FixtureName <fresh-name>`.

Review both car detail pages and expand Stats/Fitted parts. This completes the explicitly requested spacing/placement checkpoint; no later UI phase is advanced and user visual acceptance remains separate. Broader physics/economy gates retain their historical baselines.
