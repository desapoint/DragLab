# Mixture-conditioned backfire preview and Timber ringing isolation

## Scope

User-requested follow-up to the causality audit: investigate the Timber I6 ding
near 0:40 / 1:28 and make afterfire occurrence depend on unburned fuel, oxygen,
air/gas heat and exhaust heat, with reproducible uncertainty. **Preview-only C#;
no new generator/event model is installed in gameplay. Engine/induction/tire audio
and the original race simulation remain intact.** The preceding audit checkpoint
is commit `d97ec33`.

## Why ringing can survive the earlier replacement

The previous C# port faithfully retained seven explicit damped sinusoidal shell
modes (nominally 490–4807 Hz) as well as cavity responses. Light cleanup and less
ticking alter secondary transients/high metallic gain; they do not remove those
pitched modes. Consequently replacing the old production pop algorithm did not
remove every possible bell-like source. The engine bed is also unchanged, so its
supplementary tones cannot be ruled out by listening to a combined WAV alone.

`DiagnoseTimber` separates engine, previous backfire, shell-ablated backfire and
revised damped backfire at both supplied positions. One shared gain prevents raw
stems clipping without normalizing alternatives separately. The revised shell
retains texture but reduces modal gain/decay, adds pressure-like pitch droop and
noise modulation. The default `Texture.Reference` path preserves prior output;
`NoShell` is a diagnostic, not an automatically selected final sound.

The actual file has 35.9-second sections: 0:40 is local 3.1 seconds in A; 1:28 is
local 14.2 seconds in B. Both clips are provided; they are not the same driving
moment in this particular recording. Code proves the pitched source exists;
**user listening identifies whether it is the perceived ding.** Do not claim that
the unchanged engine has been corrected or that listening acceptance is complete.

**User listening subsequently located the ding in the engine-only clip.** It is
therefore not fixed by changing the backfire waveform. Private engine ablations
isolate the intake-gulp sine and mechanical clatter without editing runtime DSP.
At 0:40 the intake-only RMS is 0.01170; clatter-only is 0.00001272 (roughly 59 dB
quieter). The intake oscillator is the strongest isolated tonal candidate; final
ear confirmation is separate. Its equation is `90 + throttle * 75 Hz` plus a
second harmonic and decaying gulp excitation, routed outside the exhaust filter.
No-gulp and isolated-gulp WAVs are provided at both positions. The unmodified
diagnostic reference matches the archived engine bed sample for sample.

Because the perceived ding is in the engine, the final mixture matrix **retains
the previous LessTicking/Reference backfire timbre**, rather than silently choosing
the exploratory Damped shell variant. The shell ablations remain diagnostic only.

## New event model

`BackfireMixturePreview` is a reduced-order, eight-zone exhaust transport model,
independent of the old pop counters/timers. Every tick receives kilograms of fuel
and air, chamber burn fraction, charge temperature and an exhaust wall heat target.

- Chamber oxygen limits combustion. Fuel not burned in the chamber and oxygen
  remaining after that burn enter the exhaust inventory. Fully burned fuel cannot
  be used again to create an explosion.
- Adaptive mixed-volume advection transports each inventory down the path. Exit,
  retained, chamber-burned and exhaust-burned fuel are accounted for. Reverse-order
  updates avoid teleporting one tick's inlet mixture through every zone.
- Gas/wall temperatures exchange heat; chamber exhaust heat, incoming charge heat,
  normalized engine/exhaust thermal state and ambient cooling influence ignition.
- Fuel concentration must lie within a temperature-adjusted flammability window.
  A trace of fuel and oxygen diluted in mostly inert exhaust is insufficient,
  even if their ratio looks suitable. Cold walls can quench a mixture.
- Only an eligible mixture can win the seeded ignition-delay roll. Event time,
  zone, consumed fuel/oxygen, heat, released/acoustic energy and random seed are
  recorded. No fuel, no oxygen or no retained combustible mixture means no event,
  regardless of seed. Heat alone cannot create fuel.
- Each partial local reaction consumes actual retained material. Its released
  energy is split between heat and an explicit acoustic-energy fraction. Existing
  exhaust fuel may burn after a fuel cut; the cut itself supplies no new fuel.
- Seeds derive from master/configuration/tick/zone/substep. Acoustic cleanup does
  not decide chemical events. No Unity/gameplay random stream is consumed.

The bridge replays all ten actual race input configurations. It queries the shared
`FuelDynamics.Flow` law with realistic accounting on the **private replay object**,
restoring its original depletion setting in `finally` before advancing again.
This matters because the old acoustic auditions intentionally disable depletion.
No player save or live race object is touched; the fuel formula is not duplicated.
Fuel-cut duty already reduces delivered fuel, so it is not also misclassified as
unburned fuel. Ignition cut, cylinder contribution and active anti-lag provide
separate burn-fraction inputs. The recorded ET and original pop counters must still
match the source fixture (ET tolerance 1 ns for JSON decimal round-trip).

New chemical events drive the shared pressure source and actual fitted exhaust
transfer with a modeled zone origin, rather than the old weighted random origin.
The acoustic amplitude follows the square root of released acoustic energy before
the existing-style output soft saturation. The unchanged pop-free engine bed is
mixed back in. Each comparison is the **complete original recording → 1 second
silence → complete new mixture-driven recording**. Existing A/B WAVs are preserved.

## Explicit approximations and remaining integration work

This is a more causal model, **not measured chemistry, CFD, flame-front simulation
or proof of real-car timing/SPL**. It is not ready to be described as fully accurate.

Current authored assumptions include gas holdup/pipe radii and section lengths,
constant heat capacity, chamber-to-exhaust heat partition, wall capacities/time
constants, a hydrocarbon-vapor flammability surrogate, ignition hazard and acoustic
coupling. Petrol/diesel use separate gross heating/oxygen requirements, but detailed
fuel species, evaporation, CO/H2 chemistry and catalyst surface reactions are not
solved. Chamber burn fraction comes from existing aggregate cut/condition state,
not new per-cylinder chemical measurements. Initial wall heat and its target are
estimated from normalized existing thermal values; there is no newly measured
absolute exhaust gas sensor state.

The race airflow publication omits closed-throttle pumping, so the preview declares
a 12% base-airflow vacuum proxy. Anti-lag's existing exhaust-energy control reduces
the preview chamber-burn share by an authored factor. Neither proxy changes game
power, fuel use, induction, temperature or damage. Acoustic amplitude is calibrated
relative output, not a Pascals-to-microphone/SPL conversion. The existing source's
small texture impulses remain acoustic texture, not invented chemical event mass.

Before runtime integration: review the isolated ringing clips and new complete
sequences; author/validate mixture/thermal/geometry parameters; decide catalyst and
continuous-flame versus discrete-bang treatment; publish real timestamped events
through a lossless bounded channel; prepare reusable source/propagation buffers;
then verify callback allocations and physical Android behavior. Editor source
construction intentionally allocates and must not run in a real-time callback.
The other audio model gaps remain in the [causality audit](../audio-causality-20261020/CHANGES.md).

## Verification

The C# gate covers absent fuel/oxygen, complete burn, dilution, hot/cold gas and
walls, residual fuel after cut, same/different seeds, inventory/chemical-energy
bookkeeping, finite state, preserved default source, isolated shell differences,
physical fuel demand despite disabled depletion, no new fuel during full fuel cut,
unchanged authoritative race results/counters and gameplay RNG isolation.

Initial test refinement caught two real validation issues before acceptance:
disabled audition depletion must not produce zero physical fuel input, and a
cold/too-dilute retained mixture must not be forced to ignite just to satisfy a
positive test. Controls now distinguish a hot flammable residual from those valid
negative cases. The all-silent bridge result is not accepted as matrix coverage.
Independent WAV checks must confirm exact original PCM, complete section lengths,
silence separation, headroom and preservation of old A/B files before delivery.

Final evidence: **137 C# gates** pass across controls, engine-layer isolation and
all ten full replay configurations. Independent checks confirm exact original PCM,
48 kHz stereo PCM16, complete original/new sections, silent separators, headroom,
positive residual-fuel/oxygen funding for every ignition, mass ledgers and unchanged
SHA-256 for all ten old A/B WAVs. See `verification.txt`, `artifact-verification.txt`,
`engine-layer-diagnostic.txt` and `case-summary.csv`. Clean fuel-cut and diesel
controls remain quiet; old pop counts are not forced onto the new chemistry model.
Counts are model output, not measured real-engine event-frequency claims.

The final **49-check core-only recheck** adds non-finite geometry/thermal/flow and
atomic rejection gates after rendering. Valid simulation/render paths are unchanged;
see `core-verification.txt`. The ten-case matrix is not redundantly rerendered for
input-rejection-only changes.

To reproduce on a clean checkout, first run the existing C# Unity entry
`DragLab.Editor.V8BackfireMatrixExport.Run` with `--draglab-audio-wav` to generate
the local actual-race captures. Then run `DragLab.Editor.BackfireMixtureAudition.Run`.
`Verify` runs core controls alone; `DiagnoseTimberEngine` repeats only engine layer
isolation. Generated inputs/WAVs remain local, not versioned fixtures or Python
generator dependencies.
