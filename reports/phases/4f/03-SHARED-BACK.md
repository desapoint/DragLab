# Shared Back button correction (2026-10-12)

The user selected the existing Garage/Dealership Back button as the visual target. Removed the part-page size override so all three use the same `car-back-button`, filled Back icon and secondary racing surface: 40×34 dimensions, 20-point icon, 16-point title spacing, grey shading, angled hover shine and pressed feedback. The existing Garage/Dealership visual is unchanged.

This supersedes the 36-point shop variant recorded in the preceding detail checkpoint. The layout probe now requires the exact shared dimensions rather than accepting any width below 40.

Windows `BuildPlayerOnly` passed. Focused `Test-NativeMenu.ps1 -FixtureName native-shared-back-01 -PartsOnly` passed **188 checks, zero runtime errors**, including exact Back dimensions/title spacing at the current window, compact, 1080p and 4K sizes, navigation, retained drafts and atomic checkout. The current-window detail screenshot was reviewed. [Focused evidence](native-shared-back-verification.txt).

The first sandboxed build stalled on the Unity licensing connection; retrying with local licensing access completed successfully. Earlier full native **1,133/0** and cart-domain **102** gates remain historical baselines, not rerun for this size/style correction. UTF-8, report links and whitespace checks passed; generated font serialization was verified as whitespace-only and restored.

No mechanics, cart/confirmation behavior, other controls or later phases change. Review: compare Back on owned-car, dealership and part details; confirm the same face/icon/size and separated title. Stop for user validation after the local commit.
