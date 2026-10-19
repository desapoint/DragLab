# Checkpoint F.4 — Chunk transient market stock without changing game identities

## Root cause
The F.3 paid-third snapshot contained 15,348 state_records, with 17,735
delete/insert/update operations despite only a handful of purchased physical
parts. The previous generic JSON projection split *every* generated market
offer's nested hardware/elements into independently addressed SQL rows.
A new seller epoch changes physical offer IDs, invalidating thousands of
those SQL addresses.

## Exact scoped change
Use deterministic bounded chunks **only** for these generated offer/stock arrays:
- `$/inventory/market/parts`: 128 new-part stock rows per chunk
- `$/inventory/market/engines`: 2 engine offers per chunk
- `$/inventory/dealer`: 8 seller entries per chunk
- `$/vehicleStock/listings`: 2 vehicle offers per chunk

Each chunk contains the **unaltered serialized JSON of the offers it covers**.
The outer collection still uses the pre-existing `$draglabRows` marker, so
no database schema change or save-version bump is needed. Rows have stable
keys `chunk:0`, `chunk:1`, etc., and increasing ordinal values.
`StateRows.Restore` handles both legacy individual `row:`/`id:` records
and new chunks, yielding exactly the same player profile. On the first save,
old nested generated-offer rows are removed as part of the *same durable
transaction*. Subsequent market epochs reuse the bounded chunk identities.

Important: *owned* `cars`, `engines`, `parts`, installed slots, typed
member wear, storage, cart reservations and the relational
owners/components/installed_slots tables are completely unchanged.
The move only affects transient stock that is regenerated in a market epoch.

## Safety
No change to Unity JSON serialization, market generation, source seeds,
prices, conditions, offer identities or cart hold semantics. The integrity
digest still covers every stored payload byte. SQLite WAL, synchronous=FULL,
optimistic commit tokens, transaction, backups and reset/recovery stay
unchanged.

## Verification
The editor fixture asserts the byte-for-byte logical JSON roundtrip for all
four generated market collection paths, normal legacy row restoration and
that chunking decreases the physical row count without collapsing owned
element records. The packaged player checks third-race reload/receipt and
reports `projected state records`, `changed state records`,
`state row delta SQL`, `record projection`, `integrity digest`,
`durable commit`, `entire save worker` and result-to-commit.

```powershell
git switch main
git pull --ff-only origin main
.\tools\Invoke-Unity.ps1 -Action SqliteStateVerify
.\tools\Invoke-Unity.ps1 -Action NewSupplyVerify
.\tools\Invoke-Unity.ps1 -Action PartsMarketVerify
.\tools\Invoke-Unity.ps1 -Action BuildPlayerOnly
.\tools\Test-NativeMenu.ps1 -MarketBaselineOnly -FrameProbe -FixtureName market-chunks-f4-20261019-a
.\tools\Test-NativeMenu.ps1 -FinishPacingOnly -FrameProbe -FixtureName market-heldwrite-f4-20261019-a
```

Reference F.3 third paid baseline: 15,348 projected records,
17,735 changed; projection 391.7 ms, SQL 187.6 ms, digest 104.6 ms,
COMMIT 227.8 ms, worker 978.1 ms, total settlement 1730.2 ms.
Compare under identical test conditions. These are previous measurements,
**not results of this checkpoint**.

One-time conversion of an existing individual-row SQLite save can still
require deleting historical rows; follow-on stock epochs should avoid that
churn. Must pass local Unity tests before claiming a measured performance
boost.