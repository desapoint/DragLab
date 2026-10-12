# 4E.2 — Native tuning controls (2026-10-12)

The user approved continuation from the frame and explicitly requested the tuning elements together, including proper increments, right-click fine adjustment and middle-click reset. This is one requested control batch within 4E, not completion of the configuration-library phase.

## Delivered

- Engine: launch RPM, cam timing, cold start, limiter threshold/behavior/cut, two-step enable/behavior/cut and shift-light threshold.
- Induction: installed topology/count, nominal compressor boost target, pulley ratio, blow-off behavior, launch preload and anti-lag. Options follow installed capability; unsupported compressor controls are omitted. Diesel spark-based controls stay unavailable. Actual manifold boost still depends on the authoritative induction model, load/flow and limits; the target label does not promise measured boost.
- Drivetrain: actual transmission name, final drive, automatic shift RPM and existing prototype AWD center configuration. Conditional split/open/locked/on-demand/active/LSD controls retain installed topology locks, axle-isolation behavior and linked minimum/maximum bounds. Installed LSD limits are read-only.
- Wheels: external tire radius in inches, width relative to reference, pressure, driver mass, ballast mass/position/height and a layered proposed-car preview. Wheel dimensions affect existing physical gearing/inertia/grip; they do not imply a universal performance improvement.
- Shared native controls, concise labels and accessible info dialogs for both editable and locked hardware. Joined selected-value states, hover/mouse-down, switches, number fields and scrollbars reuse the accepted theme. No Tab traversal or focus highlight.
- Left drag quantizes to normal increments. Right-down does not jump to the pointer; right-drag captures relative movement at 10× lower sensitivity and uses the finer increment. Releasing ends capture. Middle-click restores the field default without applying or saving. Numeric entry uses the fine increment and confirms on Enter/blur; unfinished delayed text survives frame refresh. Non-finite values are rejected, and permitted bounds clamp finite inputs.
- Same authoritative F8-shared tune draft, installed hardware permissions, Current/Proposed/No wear curves, summary/detailed stats and explicit Apply/Revert. Apply retains the existing durable transaction; leaving discards unapplied values. Preview refresh is capped at 10 Hz during slider drag, with the final value refreshed on release. Controls stay mounted while graphs update, preserving pointer and text ownership. No driving probe is added.

## Normal / fine increments

Bounds derive from the car/kit and current unit preferences. Defaults are the existing vehicle/field defaults, clamped to valid hardware bounds; middle reset differs from reverting the applied tune.

| Control | Normal | Fine |
|---|---:|---:|
| Launch, limiter, shift light, automatic shift RPM | 100 RPM | 10 RPM |
| Cam timing | 1° | 0.1° |
| Boost target (bar / psi / kPa) | 0.01 / 0.5 / 0.5 | 0.001 / 0.05 / 0.05 |
| Pulley and tire width | 0.05× | 0.005× |
| Final drive | 0.05 | 0.005 |
| Outer tire radius | 0.1 in | 0.01 in |
| Tire pressure (bar / psi / kPa) | 0.05 / 1 / 1 | 0.005 / 0.1 / 0.1 |
| Driver / ballast mass | 1 / 5 kg | 0.1 / 0.5 kg |
| Ballast position / height | 0.05 m | 0.005 m |
| AWD share/preload | 1 percentage point | 0.1 point |
| AWD slip threshold | 0.1 percentage point | 0.01 point |
| Coupling response | 0.01 s | 0.001 s |
| Free-shaft inertia | 0.01 kg m² | 0.001 kg m² |
| Free-shaft friction in chosen torque unit | 0.1 | 0.01 |
| Viscous drag per rad/s in chosen torque unit | 0.001 | 0.0001 |

## Checks and limits

- Final Unity 6000.6.4f1 Windows development build passed; the final log has no compiler or USS errors. The executable and supporting build files are updated.
- Focused native Tuning gate: **394 checks / 0 runtime errors**, PASS. Real mouse/keyboard tests cover normal/fine drag, right-click alone on an unaligned converted default, middle reset, delayed numeric editing, finite/bound validation, locks/help, changed wheel proportions and read-only save bytes. The matrix covers all 13 authored cars, compatible twin-turbo/twin-supercharger/twincharged installs, limiter/two-step/shift-light/timing/gearbox/controller capabilities, carburetor/diesel/injection restrictions, all six AWD modes and upgraded center locks, linked bounds and bar/psi/kPa equivalence. Existing Apply/Revert/F8, failed durable Apply, reload, navigation discard and no engine/car gates are rerun. [Focused evidence](02-tuning-focused-verification.txt).
- Final full native regression: **1,133 checks / 0 runtime errors**, PASS. Includes the new tuning matrix plus shell/navigation/shine/input rules, Settings/difficulty/units/reset, owned cars/maintenance/dyno/grouped stats, dealership filters/comparison/commerce and paid used-car disclosure. [Full evidence](02-full-native-verification.txt). Counts overlap with the focused gate and must not be summed. Historical 4E.1 evidence remains unchanged.
- Reviewed rendered Engine, induction and wheel-control captures. Compact/1080p/true-4K target layout checks pass. The existing D3D12 upload-buffer warning appears during 4K capture; no native assertion or runtime error accompanies it. Physical 4K monitor and subjective feel/appearance remain for user review.
- Corrected collapsed joined-choice rows and oversized switch layout found by the real native matrix. Frame refresh retains unfinished numeric text; bound/display updates cannot silently invoke setters. Right-down/zero movement preserves exact unaligned defaults.
- UTF-8/link and whitespace checks pass. Font reserialization was restored only after proving whitespace-only equivalence with the clean baseline. Matching metadata accompanies both new scripts. Raw builds, screenshots, logs and isolated profiles remain local/ignored.
- No simulation/economy, paint, saved-library or shop/cart change is delivered. Earlier domain/economy gates are historical evidence, not rerun here. Technical completion is separate from user visual acceptance; stop after this control batch.

## Review checklist

1. Open Tuning and browse each tab on stock and upgraded cars. Review locks, concise info, wheel preview and Current → Proposed comparisons.
2. Left-drag, right-drag, middle-reset and type a numeric value. Confirm the increments, thumb/field synchronization and draft-only behavior.
3. Revert, Apply, F8 comparison and navigate away. Review resizing and the shared scrollbars/theme.

Technical delivery and visual acceptance are separate. Stop after this requested batch; saved configurations and later pages require further direction.
