# Android and commercial release preparation

The project keeps its fixed-step simulation and vehicle data portable. Android support is being installed through Unity Hub; no phone build has been claimed until the module, SDK/NDK/OpenJDK and real-device verification complete.

## Android first test

1. Finish the Android Build Support installation, including Unity's SDK/NDK and OpenJDK components.
2. Open the Unity project. In Build Profiles, create an Android profile and switch platform.
3. Keep `FirstSlice.unity` in the scene list. Use landscape orientation; the setup tool supplies a placeholder application identifier.
4. Use a Development Build on a USB-debugging-enabled test device. Start with automatic driving, because the starter IMGUI controls do not yet support reliable simultaneous multitouch.
5. Check import/rendering, landscape layout, garage/race/result flow, save/relaunch, suspend/resume, thermal behavior and performance. Record device, OS and timings.

Before release: replace IMGUI with scalable touch UI; respect safe areas; add a true pointer-per-control input layer; handle focus and interruptions; test at 30/60/120 display FPS. Limit texture sizes and remove QA/source-only content from build payloads. Current placeholders use uncompressed textures for clarity; compression/atlases are later profiling decisions.

## Desktop first test

The prototype can be built for Windows with Unity's installed standalone support and the included build command. It requires no Steam account. A generated executable must be checked separately from editor Play Mode. Keyboard is the first control target; controller mappings, resolutions, fullscreen and accessibility are follow-up work.

## Steam / other storefronts

Keep the game independent of the storefront. Add a platform adapter later for achievements, cloud saves, presence and ownership. Save schema versioning and migrations belong in the game, not inside Steam callbacks. Reserve a Steam App ID and replace temporary product identifiers only when the user supplies publishing details.

Before selling: establish a sustainable original/legally licensed vehicle roster; verify artwork/music/font/engine/plugin licenses; remove debug tools from player builds; ship settings/remapping/onboarding; test saves across updates; prepare installer/build upload automation and crash handling; complete store assets and required disclosures. No storefront publication or account changes are authorized by this preparation task.
