# Fitted tire/rim corners - 2026-10-18

Requested fix: fitted parts showed the original tire specification after individual replacements.

The shared dialog named the tire/rim assembly from its container definition, which stays original after member swaps. It now resolves the four actual fitted definitions using TireFitments / WheelFitments. A uniform replacement set uses its current common name; different specifications show Mixed tires / Mixed wheels. Four corner rows show the installed name, retained member identity (tooltip), remaining condition and applicable failure/reason. Tire percentages use remaining physical life including consumedLife, not the neutral container condition. Existing component groups, shared Shop action and inspection disclosure gating are retained.

## Verification and review

Final packaged Windows suites pass **105 owned fitted / 63 dealership inspection**, zero runtime errors. See [the concise assertions](verification-summary.txt). The final expanded owned capture was visually reviewed. Fixtures cover mixed rear replacements, all four individually replaced with one different specification, varied wear, a typed tire failure and three owned-car contexts including a nonselected car and detached engine. The read-only dialog must preserve inventory, money, selected car and saved bytes. Dealer checks retain actual listing contents and inspection privacy. Corner names in the final regression fixture deliberately differ from the original model name; merely choosing another content ID can select an OEM entry with an identical label.

Windows and ARM64 Android builds succeed. APK signature, ZIP/ELF 16 KiB alignment, CRC and SQLite plugin checks pass; the tested Windows runtime remains unchanged after Android packaging. See [build hashes](build-manifest.txt) and [packaging evidence](packaging-verification.txt). Updated local artifacts are `Game/Builds/Windows/DragLab.exe` and `Game/Builds/Android/DragLab.apk`. Verified build-only Unity serialization was archived locally and restored.

Review Wheels & brakes in the fitted-parts dialog: each corner should match the installed tire/rim and its condition. Phone visual acceptance remains pending without a connected device. No mechanical, save-schema or later UI phase changes are included.

Raw captures/logs/isolated saves remain local and ignored. Pre-existing reports/maintenance-batch/verification.txt, reports/shop-inventory/cart-verification.txt, .codex-remote-attachments/, Game/Assets/Resources.meta, Tests.ps1 and reports/race-exit-regression/ remain excluded and preserved.
