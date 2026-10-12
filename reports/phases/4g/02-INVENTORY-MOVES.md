# Native inventory destinations (2026-10-12)

User authorized the Move dialog, engine workshop/naming/swaps and deferred block preparation together. This is the first working checkpoint of that batch; later authorized work follows as separate commits.

Part details identify Storage / Car / Engine ownership and expose a shared Move dialog. Complete retained parts can target named storage, cars or detached engines; loose units can move between storage containers and retain their fitted-position installation flow. A copied pending cart validates destination fit, reservations and conflicting operations. The selected-car static dyno is available when both snapshots exist. Add move stages only; Cancel/X does not edit ownership or save. Inventory filters now select exact containers. Existing cart checkout saves the basket atomically. Competing moves into one destination slot are rejected.

Windows build passed. Isolated native Workshop checks passed **264 / zero runtime errors**, including the shared thirteen-car baseline, cancel/preview isolation, staged movement, retained identity at actual checkout and container filtering. [Evidence](native-moves-verification.txt). Domain/full-native matrices remain historical; no new physics. Raw fixture images/logs/saves are ignored. UTF-8 and whitespace checked; whitespace-only font serialization restored.

Review: Build → Inventory → retained part → Move; choose a destination and cancel or Add move. Verify Cart before checkout and the Location filter afterward. Engine preparation/naming/swaps and block preparation remain in progress within the user-approved batch. User visual acceptance is separate.
