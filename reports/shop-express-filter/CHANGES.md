# Remove Express availability filter — 2026-10-14

Build's Availability choices now omit Express. The query no longer has a special Express-only branch. Existing All/Owned/Affordable/Unaffordable/In stock filters and price disclosures remain; delivery is a shortage cost, not a product category. Car wear/maintenance controls retain quick broken/threshold same-part replacement with an explicit combined express quote for missing supply. Saved-build missing-part delivery remains supported.

Checks: inspected the shared query and modal definitions; no Express availability choice/branch remains. This is a reversible two-line native filter change with no save, simulation, pricing or wear changes. The next combined Windows build will include it. No later interface phase is advanced.
