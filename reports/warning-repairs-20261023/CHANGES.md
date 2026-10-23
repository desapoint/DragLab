# Warning repair shortcuts — 2026-10-23

Current/introduced/remaining car warnings link to specific fitting part types with fresh Compatible + In stock + Price ascending defaults. Resolved warnings do not invite duplicate purchases. Fluid/service issues route to the same car's maintenance; missing engines route to the engine market. Stored-engine warnings reuse their existing typed part-preparation picker rather than shopping against an unrelated selected car.

Part selection uses actual named categories and current affected parts, not warning severity as incompatibility. Existing current-car carts remain intact; switching to another warned car requires no active other-car cart or unapplied tuning, uses durable selection and leaves failed saves retryable. Browsing does not purchase or repair anything.

## Verification and builds

- Packaged warning suite: **948 checks / zero runtime errors**. Covers exact category routing, default filters, preserved cart/save bytes, resolved issues, maintenance, unapplied tuning, other-car cart guards, failed durable selection/retry, correct engine market and SQLite reload. Shared dialog screenshots at normal, compact, 1080p and 4K sizes remain in the local screenshot gallery.
- Final-player quick-leave regression: **54 checks / zero runtime errors**, with a successful player exit and gallery PASS. The outer sequential command incorrectly treated a stale PowerShell native exit code as failure after that successful script; the subsequent listening/inspection reruns were **not launched**. Their retained successful baselines are full listening 64/0, header controls 49/0, owned fitted/layout 119/0 and dealership 271/0, documented in the listening report. Do not count these as repeated warning-build tests.
- The first warning run stopped at check 947 because the test clicked an off-viewport engine action. The corrected test explicitly scrolls it into view and requires visible guard/save errors, then verifies successful retry. No production fix was needed for that failure.
- Windows and Android ARM64 IL2CPP Development builds pass. APK signature, 16 KB alignment, ZIP integrity and native library checks pass. See [exact artifacts](build-manifest.txt). Android has identical production code; the later correction is inside the Windows-only test fixture.
- Physical phone testing and user visual/listening acceptance remain pending. Existing DEVELOPMENT_BUILD deprecation warnings remain; there were no new compiler/runtime errors.

## Interface review

Review a worn part's Shop shortcut and its Compatible / In stock / Price ascending defaults; confirm your current cart remains. Review low-fluid maintenance, missing-engine selection, and an unapplied tune's protective message. Existing warning groups, modal scrolling and Close/X use the shared styling. No purchase, repair, express fee or fitment policy is changed by following a link.

The earlier shifter, quick-leave and listening controls retain their own local commits. No save-system refactor, audio-model change or later architecture/workbench phase is implemented here.
