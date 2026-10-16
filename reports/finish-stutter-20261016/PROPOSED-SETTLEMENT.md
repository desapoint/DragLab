# Remaining finish-stall proposal — approval required

Measured synchronous finish work blocks the live race: approximately 215 ms for practice and 1.10 seconds for a paid market refresh in the initial copied-save Editor probe. The existing synchronous sequence remains in place in this checkpoint. The safe camera correction and validated-save entry only reduce symptoms/work; they do not solve the entire stall.

Proposed implementation:

1. Capture the final beam/checkpoint and hold a stable, read-only profile reference. Keep all Unity assets, catalog access, scene objects, UI and live simulation on the main thread.
2. Clone only plain serialized save data with Unity's supported string JSON serializer on an isolated job. Publish nothing from that job.
3. Advance the existing candidate preparation, validation and seeded market generation over bounded main-thread steps. Poll already captured inspection jobs instead of waiting on unfinished ones. Preserve exact seed/quantity rolls, reservations and all asset identities/conditions.
4. Serialize the isolated candidate and write its SQLite transaction through the existing controlled writer. Do not wait on that job in a race frame. Block Garage/retry transactions while settlement is pending; continue rendering and shutdown simulation.
5. Publish rewards/profile only after a successful commit. A failed write keeps the old profile, visible error and retry. Queue subsequent shutdown resources after publication. Explicit quit/suspend boundaries drain pending work before saving final state.
6. Verify both finish orders, both cars' continuous screen motion, cold/warm markets, every-third-race refresh, slow/failing saves, no duplicate rewards, pause/focus, retry/exit and interrupted recovery. Compare frame costs with deliberate slow-disk injection. Keep current save format, authority and recovery rules.

Risks requiring approval: changing the ordering of durable settlement and pending actions can corrupt/lose state or duplicate rewards if publication or checkpoint barriers are wrong. Automatic approval review rejected applying this larger sequencing/background-persistence change under the current stutter-fix authorization. No part of that rejected rewrite is included in the delivered checkpoint.

Unity documents background use of JSON serialization for supported plain data: https://docs.unity.com/en-us/engine/6000.5/manual/scripting/compilation-and-code-reload/script-serialization/json-serialization . This does not authorize worker access to Resources, UnityEngine.Object, rendering or mutable simulation state.
