# S06d2a independent Luna-high review

Date: 2026-10-25
Baseline: `0d3c7ae`
Scope: ready Shop product groups and the exact supply projection used with them.

## Verdict

**Correctness review: PASS for this bounded implementation component. Local working commit: approved.** The source keeps the legacy path for cold or invalidated data and preserves current stock, cart holds, prices, ownership and fitment results on the ready path.

**Responsiveness gate: FAIL / OPEN.** This is not completion of S06d2a or S06, and it does not authorize advancing to owned-car refresh or S07. The real native path used ready groups and stock binding, but the measured consumer frame remains above the requested 50 ms target.

## Source review

- Shop groups and the alias index are immutable snapshots published only after complete source-order grouping, against the captured key snapshot, revision and definition references. Ready lookup checks current authored semantics and never runs warmup synchronously. Shop readiness no longer waits for the unrelated market fit/layout warmup.
- The fallback retains the original `GroupBy(NewProductKey, StringComparer.Ordinal)` behavior. Ready groups preserve group encounter order and alias order. Representative choice remains fitted definition, selected-model prefix, then ordinal ID. Ownership, search aliases, stock, holds, price, express delivery and compatibility are read from current state.
- `CounterIndexed` uses `Interface` only to narrow candidates. It preserves selected-spec priority and model order, then uses the existing exact `Fits` and `Count`; missing or exceptional index cases retain the legacy `Counter` path.
- Ready ledger binding is operation-local. It preserves direct-ID precedence, then the first equivalent stock row in market order, null misses, live remaining units, quote arithmetic and the existing `CartReservations.Held` path. It does not write the profile market index or cache player stock in authored groups.
- Stable-key comparison checks public in-place edits, including every `AwdHardwareSpec` and nested `AwdSpec` field. The remaining canonical-key serialization is pre-existing work; this change does not retire runtime JSON or the broader legacy profile-copy paths.

## Verification evidence

| Check | Result |
| --- | --- |
| Ready product-group Editor wrapper | PASS, run `5742`, 1,585 entries (1,583 assertions + 2 timing notes) |
| Shop supply projection Editor checks | PASS, 37,690 assertions |
| Windows player build | PASS, run `6426`, exit 0 |
| Deterministic native verification | PASS, run `43138`, 70 checks / 0 runtime errors |
| Final matched native visual acceptance | Not claimed by this source review |

The focused checks cover source-order/group parity, representative selection, aliases and search, stock/price/affordability/ownership/cart holds, direct authored mutation and same-count replacement invalidation, selected/no-car Counter parity, exact direct-ID and first-equivalent-row selection, quote/fee arithmetic, and live ledger quantities.

The native capture exercised the actual ready path with stock binding (`readyGroups=true`, `bound=true`). Its measured values were: lookup 7.028 ms; current inspection 6.804 ms; inclusive Shop population 55.839 ms; ledger setup 9.191 ms; inclusive Shop enumeration 32.376 ms; classification 7.226 ms; representative selection 4.447 ms; supply count 3.124 ms; quote/hold 3.346 ms; entry creation 13.199 ms; aliases 1.864 ms; and stock binding 4.875 ms. These are nested timings and must not be summed.

The measured navigation frame was 124.561 ms, outer UI 93.724 ms, and `nativeParts` 89.475 ms. Shop population and the parts consumer frame still exceed 50 ms. I make no overall latency-improvement claim from this sample. The per-product timing instrumentation is present in the measured source, so any later removal or adjustment of it needs a matched capture.

## Remaining gate

Commit this as a correctness-verified working component only. Keep the whole S06 responsiveness gate open; continue attributing and reducing the measured ready-path consumer cost, then rerun the matched seeded player and its native/UI parity checks before marking this checkpoint complete or advancing. Technical review here does not imply human visual, phone, S06, JSON-retirement, or S07 acceptance.
