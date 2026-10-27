# Independent review: S06 typed cart preview contract

## Review decision

The contract is approved as an S06 planning boundary for read-only typed preview work under the user's existing whole-plan authorization. This review adds no new user-approval pause. Production caller activation remains a technical gate: keep the current nonempty-cart route until the complete typed replacement passes the required input matrix, parity, freshness, failure, and native performance checks. Do not add a fallback route for partially supported carts, change checkout, or advance into S07 under this preview checkpoint.

The contract correctly requires an ordered, read-only projection that shares authoritative domain decisions; exact typed input capture; complete fresh fences before each yielded stage and publication; no JSON or whole-profile mirror; no `MechanicsOwnership.Ensure` normalization; no partial publication; and no lowered cart, quantity, stock, position, or frame limits. Its scope includes mixed ordered cart rows, sales, moves, purchases, multiple cars and engines, block preparation, used/new stock, reservation holds, express supply, identities, condition, warnings, and inspection output. S07 checkout and transaction semantics remain out of scope.

## Clarifications required before implementation planning becomes a production switch

1. **Existing versus virtual IDs.** Preserve every existing physical item/member ID and condition exactly. Hypothetical new preview products need stable preview-local IDs derived from the request row and position/role; they must never be persisted. Do not require equality with random GUIDs generated only inside the legacy preview clone. Parity must compare product selection, arrangement, effects, condition, warnings, prices, and existing identities; virtual IDs need only remain deterministic within the preview request.

2. **Normalization behavior.** Add characterization proving `MechanicsOwnership.Ensure` is a no-op on a valid graph. The preview must not call `Ensure`. If the legacy clone path heals missing OEM parts, condition, tune, or fluids, record that difference explicitly: the typed preview follows the no-heal invariant and returns the authoritative validation error rather than manufacturing data. Do not silently normalize the captured profile or candidate facts.

3. **Keep partial planners dormant.** The suggested single-move milestone is useful for isolated domain planning and parity work, but must not activate a production route that accepts only simple moves while mixed/unsupported rows use a fallback. Continue using the current caller unchanged while typed planning remains partial. Switch all affected callers only after the complete supported matrix passes parity, freshness, failure, and native performance gates. A failure in the new planner must not fall back to the legacy clone path.

4. **Exact dependency closure.** Keep the typed read set limited to fields the authoritative operation actually consumes. Include the full transitive closure, but do not grow it into a profile-shaped mirror. Each output should trace to a specific consumed input or a shared authoritative planner result.

## Required evidence before route activation

The characterization and parity matrix should include the documented maxima (100 cart rows, quantity 99), multiple cars/standalone engines, source-order interactions, mixed move/sale/new/used/unit/set rows, position subsets and extras, block preparation, partial stock and express fees, active reservation holds and expiry, malformed input/error order, and same-revision plus A→B→A edits across yields. Compare exact existing identities and condition, ownership/slot outcomes, totals/fees/stock, warnings, inspection and dyno output against a frozen legacy oracle where legacy behavior does not conflict with the no-heal invariant.

Measure capture, each planning stage, validation, final layout/publication, and the actual interactive destination/quantity/position callbacks in the complete full-frame trace. Keep the 50 ms full-frame threshold and report nested instrumentation correctly. Do not claim that moving the work into preparation or a final layout frame clears the gate.

This document reviews the design boundary only; it makes no implementation, build, or native-preview claim. Continue the already-authorized S06 plan through its technical gates, one reviewed checkpoint at a time. Full S06 and all later slices remain open until their respective evidence is complete.
