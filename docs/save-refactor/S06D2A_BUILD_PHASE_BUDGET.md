# S06d2a native Parts browser build phase budget

The empty-cart native Parts build now yields around every expensive completion/validation stage:

1. After incremental capture finishes, the full `TryFinish` fence runs and yields in `Binding`.
2. `Binding` runs the typed cache's complete current-read-set fence and binding, or an explicit full read-set fence when no cache exists, then yields in `SettingUp`.
3. `SettingUp` performs a fresh full read-set fence and constructs the private `NativePartBrowser`, then yields in `ValidatingSetup`.
4. `ValidatingSetup` performs the fresh post-construction fence, then yields in `Preparing` before any item preparation.
5. `Preparing` performs the full pre-chunk fence and bounded `AdvanceBuild`. A finished model yields in `Publishing`.
6. `Publishing` performs the final full fresh fence before transitioning to `Complete`.

Every full fence remains in place and still compares current same-revision data. The model remains private in `Capturing`, `SettingUp`, and `Preparing`; `Model` is available only after final publication succeeds. A failed or cancelled builder remains terminal, including when a changed input is later restored. This separates synchronous owner-thread work across rendered `Advance` calls; it does not move validation to a worker, cache fresh prices, relax a fence, or alter item caps.

Focused checks cover each pending state, including hidden model/read-set/cache binding, and same-revision A-to-B-to-A mutation probes across binding, setup, post-setup validation, preparation and publication. The typed binder reports stale current inputs with a dedicated `InvalidOperationException` subtype so the builder retains the `InputsChanged` classification; other setup/bind errors remain ordinary terminal failures. Existing complete-build checks continue comparing exact source ordering, prices and model output against the synchronous reference. The coordinator owns Unity execution and records the measured frame/validation timing separately; per-operation costs within a phase can still exceed the target, so this phase split alone does not establish the 50 ms gate.
