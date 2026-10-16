# Owned-car fitted parts — 2026-10-16

Added the shared compact Fitted parts action beside Stats in owned-car details. It opens the existing grouped modal with the viewed car's actual installed slot identities, names, condition and individual faults. A detached engine and loose inventory do not appear as fitted to the chassis. Dealership inspection disclosure remains unchanged.

Both entry points now call the same dialog renderer; no separate theme, duplicate grouping or preview simulation is added. Existing Shop shortcuts retain their current-car compatibility context and default Compatible + In stock + component-type query. Viewing another owned car does not silently select it.

Windows development build succeeds with Unity 6000.6.4f1 and is installed in `Game/Builds/Windows`. Focused packaged native checks pass **45 checks / 0 runtime errors**, covering the existing dealership action, duplicate owned cars, a detached-engine car, exact installed identities, grouping, actual tire condition, shared modal bounds and Shop filters. Profile/selected-car/SQLite bytes are unchanged by browsing. The representative 1280×720 details capture was visually inspected. Normal captures are refreshed in `reports/interface-screenshots`; raw fixtures/images/logs/build outputs remain local and ignored.

Reproduce: `tools/Test-NativeMenu.ps1 -OwnedFittedOnly -FixtureName <fresh-name>`.

Review: Garage → owned car → Fitted parts; expand the system groups and inspect the real part values. This is the requested button checkpoint only; later interfaces and user visual acceptance remain separate. Other gameplay/physics suites retain their prior baselines and were not rerun.
