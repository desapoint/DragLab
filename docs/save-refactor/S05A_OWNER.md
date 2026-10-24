# S05a owner boundary

Status: owner-side v3 load overlay and transitional capture handoff are implemented; storage integration and the parent-owned migration gates are still in progress. This report describes the owner layer only and does not claim migration completion.

## Load boundary

`ProfileStore.Load` now consumes `SqliteStateStore.ReadState` with an owner materialization callback inside the gated read transaction. On ordinary v3 loads it decodes the remaining legacy JSON once, sets the committed storage token, applies the typed economy/fluid/receipt envelope directly, and validates the exact current v12/mechanics-1 source. It no longer runs broad `Sanitize` or `MechanicsOwnership.Ensure` after this strict boundary. Those operations could otherwise heal state after validation and later rewrite it. When `ReadState` performs a v2 migration, it supplies the exact owner-decoded profile that its validation callback returned; the owner uses that object rather than decoding the legacy source again. The typed overlay is applied before post-migration validation. Storage then compares SQL relationship indexes against this hydrated profile before the read transaction completes. Older logical profile contracts require their explicit legacy import/migration path; this checkpoint does not claim to migrate arbitrary old logical contracts.

`LegacyProfileDecoder` is restricted to the supported v12/mechanics-1 boundary with initialized driveline/equipment/ignition versions. On the v2 source path it checks raw JSON presence and numeric shape for all moved economy fields and all fourteen fluid fields on each car and detached engine before `JsonUtility` can materialize default values. It does not call `Ensure`, create components, restore missing car fluid state, or normalize arbitrary malformed source. It runs the full existing `MechanicsOwnership.Validate` against the original decoded profile after these narrow inline-null rules:

- An attached engine's inline fluid object is cleared only after its engine ID, parent vehicle ID, unique car, reciprocal `engineAssemblyId`, and vehicle definition all match. Car fluids remain authoritative; detached engine fluids are preserved for normal validation.
- An inactive rebuild object is cleared only when it has the exact default tuple: empty ID and engine ID, zero start/ready ticks and paid price, and no targets. A partial inactive reservation is rejected because the general validator otherwise ignores inactive jobs.
- An empty cart is cleared only for the default version-1 tuple: zero run/generation fields, empty car ID, and initialized empty line/supply lists. Empty-owner carts with any other values are rejected.
- Empty recovery work is cleared only when its ID, start/ready times and reward are all empty/zero, matching `PaidEconomy.Ensure`.
- Dealer wrappers are canonicalized only when exactly one real merchandise identity exists and the opposite wrapper is null or exactly empty/default (including neutral part condition fields). The saved discriminator is corrected and that empty inline member cleared; both-populated, identity-less, or partially populated wrappers remain for validation to reject.

The legacy fixture audit used by the parent is `save-refactor-s00/fixture-1cars-0spares.json.gz`; it includes the attached-engine fluid defaults, inactive rebuild, empty recovery-work and cart defaults, plus a valid reciprocal engine/car binding. The tracked S00 JSON fixture's populated `lastPass` remains untouched. Genuine nullable component references remain untouched.

## Save handoff

`TrySaveCore`, `BeginValidatedSave`, and `TryReset` now call `LegacyCaptureManaged` on the owner before dispatch. The worker receives the completed snapshot and captured path only; it does not receive or read a `PlayerProfile`. Each bridge write uses `WriteLegacy` and keeps the existing candidate `storageCommit` as its exact expected-commit fence. `BeginValidatedSave` still publishes the acknowledged commit and retires the merged journal only in `CompleteValidatedSave`. Reset uses the same capture with the reset flag after draining checkpoints/backups, retaining the backend's existing recoverable generation/reset flow.

This capture is the binding S05p transitional legacy bridge for existing callers. It does not activate the refill writer, and its owner-side capture cost needs measurement. The bridge remains temporary until the planned S13 caller retirement.

## Verification

No Unity checks, migration matrix, review, or Git commit were run by this owner. Those are parent-owned gates. Storage signatures are being implemented concurrently; this report records the agreed API names: `ReadState`, `SqliteStateLoadResult`, `SqliteTypedProfileData.ApplyTo`, `LegacyCaptureManaged`, and `WriteLegacy`.
