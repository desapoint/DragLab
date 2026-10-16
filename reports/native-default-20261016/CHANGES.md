# New interface by default — 2026-10-16

The native main-menu routes now start selected in the presentation-only session state: Garage, Settings, Build, Tuning, Race selection, Business and Data. This fixes an existing owned-car save opening the legacy Garage on a new launch. The existing F8 whole-interface toggle and its input/modal guards are retained. Existing native race/results startup behavior and development benches are unchanged. No save format, settings file, simulation or resource changes are required.

Updated Windows development build succeeds with Unity 6000.6.4f1 and is installed in `Game/Builds/Windows`; fingerprints are in `build-manifest.txt`. Packaged navigation checks pass **111/0** from a fresh game, then **87/0** after relaunching that same persisted owned-car save. Both runs verify new Garage startup before F8, native menu defaults, F8 old/new switching, unchanged profile/database bytes, main navigation/filter defaults and fitted-part Shop shortcuts. The second run explicitly skips first purchase and exercises the existing-save path. Reports are retained separately; captures are refreshed under `reports/interface-screenshots`.

Reproduce by running `tools/Test-NativeMenu.ps1 -NavigationOnly -FixtureName <fresh-name>` twice with the same fixture name. Test saves, raw captures and logs remain local/ignored. Broader simulation and debug-bench suites were not rerun for this presentation default change.

Review a normal launch with an owned car, then press F8 twice. This is the requested default-selection checkpoint only; no later UI phase or implicit approval of unfinished pages is recorded.
