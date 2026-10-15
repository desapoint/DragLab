# Pricing assertion and game-behavior audit — 2026-10-15

Requested follow-up to the engine-rebuild validation: establish whether the interrupted older tests indicate game bugs or obsolete test assumptions, and search for equivalent cases elsewhere.

## Findings and corrections

Reviewed purchase-price assertions in Editor and runtime verification code, together with the corresponding quote, transaction and native display paths. The game already includes stock shortages and 5–10% express fees in these paths; several older assertions compared payment with the catalogue base price instead.

| Verification | Stale assumption corrected |
| --- | --- |
| IndividualEngineVerification | Single-piston payment assumed the nominal unit price; now checks the delivered quote separately from retained identity, wear and neighbours |
| AxleWheelVerification | Two individual tires assumed two nominal unit prices; payment and physical-state assertions are now separate |
| AxleWheelVerification | Mixed-compound Buy same omitted constituent supply fees |
| PartEffectsVerification | Rev limiter, two-step and shift-light installation assumed the base module price |
| TireFailureVerification | Replacement tire set assumed the base set price |
| InterruptedRunRuntimeVerification | The legacy interrupted-run repair probe assumed a base driveshaft price; it now captures the actual supply quote before clicking |

Nominal-price comparisons that are not payments remain valid, including checking that one component costs less than its set. Authored car-offer purchases and exact used-engine offers retain their explicit offer-price contract. Existing payment checks already using `quote.Cost`, `quote.total`, plan cost or supply quotes were retained.

The corrected tests do not change prices, delivery rules, physics, installed hardware behavior or user saves. They also do not simply trust the same quote implementation: an independent deterministic matrix validates the quoted and charged amounts against the fee rule.

## Independent pricing matrix

`SupplyPricingVerification.Run` covers all 13 cars, every allowed fee percentage from 5 through 10, and empty/one-unit/full-set supply. It calculates expected amounts independently as base price plus the rounded percentage on the missing fraction, consuming availability sequentially for multi-unit purchases.

Cases cover single-piston paid service, complete piston sets, two-unit installed carts, failed-driveshaft Buy same and mixed front/rear tire Buy same. Checks include the displayed/payment preview, exact one-time debit, successful transaction publication, correct fitted identities and retention of displaced components/condition. This detects an incorrect quote shared by both UI and payment code as well as quote/payment divergence.

For example, a three-cylinder car's nominal piston unit price is 60. With no local stock and a 5% fee, the independent expectation, reviewed quote and actual charge are all 63. An assertion expecting 60 fails while the game is charging correctly.

Final expanded matrix: **PASS, 2,106 checks across 1,170 purchase cases**, 280.9 seconds. All expected, quoted and charged values agreed. Local rows and results are in `pricing/pricing-matrix.csv`, `pricing/verification.txt` and `pricing-all-rates.tsv.results.tsv`.

An additional **1,170-case exact-integer cross-check passed** against the recorded quotes and debits. The oracle uses integer numerator/denominator ceiling division, so it does not share the game's floating-point fee-rounding implementation. Future matrix runs use this exact oracle directly. `VerifyRecordedPricing` can independently recheck a completed matrix against current authored definitions; local evidence is `pricing/exact-recorded-verification.txt` and `pricing-exact.tsv.results.tsv`.

## Other test assumptions uncovered

The individual-engine wrapper runs an additional old PartsVerification suite after its own 879 checks. Its raw-definition versus installed-OEM comparison demanded `1e-8` equality despite different normalization paths (explicit installed fueling/ignition, valvetrain/hardware permissions, etc.). Measured all 13 models before changing the expectation: maximum difference was **0.002345 s and 0.015281 km/h**. No game code was changed. The calibration assertion now allows less than one fixed simulation tick and half the displayed 0.1 km/h precision; a new assertion requires repeated runs of the identical installed build to remain **exactly deterministic**.

PartsMarketVerification directly applied a saved build and then requested a payment quote without the post-operation normalization used by `MechanicsTransactions.Try`. Freshly created tire sets consequently lacked initialized typed members at that intermediate point. The fixture now uses the actual game transaction boundary; the market suite passes and ownership is validated. The native checkout probe additionally buys a complete tire set into storage and reloads it to verify the game publishes four typed members.

## Runtime evidence

| Check | Result |
| --- | --- |
| Independent supply pricing | PASS: 2,106 checks / 1,170 purchase cases |
| IndividualEngineVerify | PASS: 879 individual engine/clutch checks plus 102 OEM/parts checks |
| AxleWheelVerify | PASS: 541 checks, including scenarios previously hidden behind the failed price assertion |
| PartsEffectsVerify | PASS: 4,053 checks |
| TireFailureVerify | PASS: 5 checks |
| PartsMarketVerify | PASS: 408 checks |
| Windows BuildPlayerOnly | PASS |
| Native pricing checkout | PASS: 111 checks, zero runtime errors |

The native probe uses real mouse clicks for cancel and confirm, validates displayed cart totals against independent fee calculations, verifies actual balances and retained components, and reloads paid state from SQLite. It exercises full and individual sets at 5%/10% with empty, partial and full shelves, plus a stored new tire set. Fixture setup alone uses direct staging; payment and confirmation use the actual rendered cart/dialog.

The 1280×720 express confirmation screenshot was visually inspected: cart total, balance after, delivery-fee notice and confirmation total agree. Local captures are `reports/interface-screenshots/pricing-individual-express.png` and `pricing-set-express.png`.

Conclusion for the audited pricing cases: the old price assertions were incorrect; no gameplay pricing defect was reproduced. Assertions now distinguish payment mistakes from identity/condition mistakes, and the independent matrix and native checkout continue to guard the real behavior. The Windows build is updated in `Game/Builds/Windows/`.

This is a pricing/test-contract audit, not a claim that every unrelated simulation, migration or UI assertion in the repository was executed. The older forced-termination runtime probe was source-corrected but was not itself rerun; its failed-shaft payment/retention behavior is covered independently in the new matrix. The reset-floor and worn-tire rounding findings from the original audit remain separate open gameplay issues, not reclassified as test-only failures.

## Reproduction and review artifacts

Run `SupplyPricingVerification.Run` through the existing ValidationBatch manifest mechanism. Use `tools/Test-NativeMenu.ps1 -FixtureName <isolated-name> -PricingOnly` for native pricing. No player save is used.

Local evidence is under `reports/branch-audit/` (`unity-pricing-*.log`, manifests/results, raw-definition diagnostic) and `reports/player-fixtures/supply-pricing-20261015/`. Generated evidence and historical report rewrites remain untracked; the concise audit and verification sources are committed. Existing user report edits and untracked files are preserved.
