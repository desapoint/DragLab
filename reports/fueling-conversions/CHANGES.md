# Fuel cut, ignition cut and carburetor-to-EFI conversions

## Cut behavior already present

- Fuel cut suppresses injector delivery in proportion to the shared simulation's combustion fraction. Ignition cut suppresses combustion while requested fuel can continue flowing. Both reduce torque; neither prevents a mechanically forced over-rev from a bad downshift.
- Hard/soft limiter behavior is separate from fuel/ignition selection. Rev limiter and two-step use separate calibration and cut-method settings.
- Carbureted engines use ignition cut because this model has no high-frequency injector control on a mechanical carb. Programmable limiter/two-step modules allow fuel or ignition cut on injected engines. Fixed modules retain their authored settings.
- Ignition cut triggers distinct exhaust-pop events. Launch preload/anti-lag is a separate controller/induction feature, with exhaust energy, turbo response, heat and wear. Fuel cut reduces preload exhaust energy. Ordinary ignition-cut catalyst/afterfire thermal damage remains simplified; do not interpret this as a full raw-fuel exhaust chemistry model.

## Added hardware

The muscle car and pickup each gain:

| Part | Current provisional price | Behavior |
|---|---:|---|
| Throttle-body EFI conversion kit | $1,500 | Shared throttle-body injection, sensors, high-pressure fuel delivery; 88% idle-control authority, 4.5% metering variation |
| Port-injection conversion kit | $2,500 | Port injectors/intake and fuel delivery; 96% idle-control authority, 2.5% metering variation |
| EFI fuel controller / fixed calibration | $650 | Compatible EFI operation; no free programmable limiter, two-step, shift-light or anti-lag |

An existing programmable engine controller also supports conversion kits, with authored engine compatibility up to 12 cylinders. Controller cylinder capacity describes engine/calibration compatibility, **not** the physical injector count: throttle-body injection can feed multiple cylinders with shared injectors. Factory four-cylinder controllers are not compatible with these authored V8 kits.

Install a kit in **Parts → FuelSystem** and a compatible controller in **Parts → Controls**. Installing a kit alone leaves an incomplete build with a specific controller warning. Ownership transactions may store incomplete builds, but races require a runnable build.

EFI changes fuel consumption, cold/idle mixture behavior, throttle hesitation and the fueling portion of procedural sound. Cylinders, displacement, cam overlap, mechanical clatter, exhaust voice and the base steady torque curve remain the same. Conversion mass is part of the vehicle's mass calculation. Fuel/ignition capacity still limits modified builds; the kit is not an automatic horsepower multiplier.

The original carb and controller remain inventory assets with their existing wear. Reinstalling the carb restores its original behavior. A failed EFI controller stops injector supply and stalls the engine without marking its internals destroyed. The shared race and physical bench use the same implementation. Raw sound engineering remains unrestricted.

The shop summary now shows fuel delivery, idle control, metering variation and hesitation changes. Measured wheelspin is unavailable when required hardware prevents the driving probe, rather than showing a false 0% traction improvement. Existing difficulty fuel-use modes, draft Apply rules and saved ownership remain intact. Legacy replay definitions without controller metadata retain their previous behavior; no progression reset or model-version bump is required.

Compatibility correction: mechanical carb controls are no longer offered for factory injected engines. If a saved custom build used that previously permitted combination, it now needs a compatible factory/EFI controller. Factory builds are unaffected, and old recorded scenarios retain their captured behavior.

## Verification

- Focused conversion verification: 122 checks across factory fueling, both kits/controllers, incomplete builds, controller failure, carb reuse, cut-method fuel flow, fuel-use difficulty and exact physical replay.
- Full regression suite: 3,969 checks passed, including the conversion checks and expanded catalog (493 definitions). See `../unity-verification.txt`.
- Native Windows shop workflow: 27 checks passed with zero runtime/GUI errors, covering actual mouse preview/purchase/controller completion, real race entry, disk persistence and carb reuse; final report in `../player-fixtures/fueling-conversions-shop-final/runtime-verification.txt`. The final display correction is checked in that player run, and screenshots were visually reviewed.
- Windows player rebuilt at `Game/Builds/Windows/DragLab.exe` with adjacent data files. No progression reset is needed.

## Real-world references

- [Haltech launch-control guide](https://www.haltech.com/news-events/how-launch-control-works/): fuel versus ignition cut, hard/soft strategies and ignition-retard turbo launch control.
- [Holley Atomic EFI](https://www.holley.com/products/fuel_systems/fuel_injection/atomic_efi/): throttle-body carburetor replacement.
- [Holley support FAQ](https://www.holley.com/support/faq/): fuel-system conversion and compatible ignition control are distinct capabilities.

These parts use fictional, provisional calibration and prices. They are not replicas of a particular manufacturer's products.
