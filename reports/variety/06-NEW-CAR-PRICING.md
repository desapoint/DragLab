# Dealership new-car premium — 2026-10-12

The user requested a larger price gap: fully restored used cars still have prior ownership/mileage and must cost less than equivalent new stock.

## Delivered

- Operating Policy version 4 exposes `newVehiclePremium = 0.5`: 50% above the larger of authored catalogue price and the existing condition/history/package-value floor. Existing dealer spread, resale safeguards and included supplies remain in that floor. Apply the premium only to new offers; do not infer newness from 100% condition.
- Separate catalogue-price metadata from saved asking price. New stock, repeated views/quotes, save reload and replacement stock do not multiply the premium again. Old saved new offers gain catalogue metadata without rerolling seller identities, parts, history, knowledge, supplies or saved asks; displayed/quoted prices use current policy.
- Corrected checkout to pass the confirmed charge separately from the immutable listing. Repricing old stock must not alter its ask in a clone and then fail exact listing validation. Original contents transfer; the confirmed price charges once.
- Fixed first-car choices, their maintenance reserve, saved used asks, seeded used spread, inspection fee and resale values remain unchanged. A restored used equivalent with all newly replaced components remains used. Custom builds with substantially more valuable hardware are not capped to an unrelated stock trim's price.

## Calibration

The [13-model matrix](new-car-pricing-matrix.csv) compares previous new prices, current new prices and restored used equivalents at the maximum current seeded asking-price spread (26%), with 100,000 km on the car and all components freshly replaced at 100%. Low-mileage pre-owned equivalents remain used too.

Examples: Juniper new $5,032 → $7,548, restored used up to $6,341; Bison new $7,500 → $11,250, restored used up to $9,450; top V8 new $11,000 → $16,500, restored used up to $13,860. Whole-dollar rounding can differ by $1 from multiplying an already-rounded previous price. Normal worn/faulty stock retains its condition/history-based pricing.

## Checks and limits

- Market/pricing verification: **106 checks**, PASS ([evidence](pricing-market-verification.txt)). Covers all 13 pristine used/new comparisons, no compounding/mutation, legacy metadata migration/idempotence, repriced old-stock atomic checkout, exact seller identities, replacement stock, changed-policy quote rejection, invalid premium values, deterministic rotation, fixed starter funding/survival and retained used resources.
- Paid operations: **351 checks**, PASS ([evidence](pricing-paid-verification.txt)). Includes atomic charges/rollback, whole/partial sales, immediate resale/part-out safeguards, bounded funded recovery, durable save/migration and no duplicate settlement. Historical 3D report preserved; this checkpoint stores the current run separately.
- Windows Unity 6000.6.4f1 development build succeeded with no compiler/USS errors. Final focused native dealership probe: **173 checks / 0 runtime errors**, PASS ([evidence](pricing-native-verification.txt)). Real mouse events cover all model prices, currency formatting, affordability thresholds, confirmations/cancel/actual purchase, stale offers and failed saves, comparisons, fitted groups, F8 and compact/1080p/true-4K render-target layout gates.
- Reviewed final 1280×720 dealer-grid and purchase-confirmation captures. Raw screenshots/logs/fixtures/builds remain local and ignored. Build-only font whitespace restored after content-equivalence checks.
- UTF-8, local links and staged diff whitespace checks passed. Prior full native 728/0 and full funded-career/balance/inspection gates are historical baselines, not rerun. Price progression pacing remains for human review; this does not claim a new complete Slice 3 audit or advance a UI phase.

## Review

Compare New versus Used for the same trim and inspect the purchase confirmation. Verify first-car selection remains affordable, new prices are higher, Cancel leaves balance untouched and Confirm charges the shown amount. Existing saves should keep their exact listings/resources. This requested calibration is technically delivered; subjective price/pacing acceptance remains separate.
