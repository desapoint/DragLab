# Save storage direction — 2026-10-14

The user asked whether runtime state should move to a versioned database. This is a proposed dedicated increment, not authorization to convert the ledger during another fix.

A local SQLite store is a reasonable future fit for owned vehicles/engines, individual parts and members, inventory locations, condition/faults/resources, market offers and quantities, cart holds, payments, career and businesses. Keep authored default content in the current Unity assets/override data, referenced through stable definition IDs. Keep driving/audio/unit preferences independent from progression reset.

Create a new save at first launch. Store schema version, originating/current game build, content revision and physics/replay identity separately. Schema version determines migrations; a game patch does not necessarily require one. Apply ordered migrations inside a transaction, retain a recoverable original before importing existing JSON, validate IDs, ownership, balances, condition and quantities, then publish the new store. Never rebuild a player's database from defaults on an update. Unsupported future versions stay read-only.

Prefer one database per save slot, foreign-key constraints, transactional purchases/transfers and a bounded serialized writer. WAL can reduce read/write contention; choose durability settings deliberately and verify crash recovery rather than assuming a database removes disk-flush latency. UI publication must follow a successful durable transaction. Bench experiments remain separate from player assets. Partial imports, retries and failures must not create parts, heal wear or duplicate payment.

The current performance correction retains the JSON ledger and its transaction contracts. Schema 10 uses a compact gzip/base64 envelope to reduce full-write size; legacy flat JSON, migration backups, corruption recovery and future-codec protection remain. This is an encoding optimization, not the proposed database conversion. The default content does not need a duplicate database to support it.
