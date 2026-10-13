# Screenshot replacement across resolutions (2026-10-13)

If the same screenshot name changes actual dimensions, publishing its new image now removes the previous generated copy from the other review destination. This prevents a stale duplicate remaining at the root or in `resolution-tests`. The owner/provenance ledger is updated for the new location.

Repeat/failure/path/publisher tests pass, including a same-name alternate-to-normal change with no stale duplicate. Recorded images were recollected with actual-dimension routing; the normal root contains no PNG outside the initial 1280×720 size. No Unity behavior or build is changed by this script-only correction.
