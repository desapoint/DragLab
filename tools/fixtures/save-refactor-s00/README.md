# S00 static synthetic performance fixtures

These nine explicitly versioned fixtures freeze the existing `6d19aae` production-state model for before/after save measurements. They are synthetic benchmark cases, not player saves or routine generated outputs. Raw generated copies, SQLite databases, backups and logs remain ignored under reports/save-refactor/S00.

Cases: 1/5/20 cars × 0/24/96 loose parts, parts/car market seed 731942, captured authored content. The initial bootstrap creates runtime IDs once; seed alone does not recreate those IDs. Compressed bytes preserve every identity/reference, condition, stock unit, preference and history value. Use these exact inputs rather than silently minting replacement GUIDs. See manifest.csv for raw and compressed SHA-256 hashes. Gzip headers have mtime 0.

The Editor-only baseline harness reads these fixture files preferentially and validates the profile before measuring the unchanged old implementation. JSON is allowed here only as a static diagnostic/import boundary. It is not the new production command or save representation. New typed command correctness fixtures must keep exact IDs/resources and compare against this baseline without healing or replacing state.

Changing fixtures is a separate explicit reviewable checkpoint; do not update these merely to make a failed measurement or invariant pass. Larger history/stock growth, packaged frame pacing and physical Android metrics are not covered by these cases.
