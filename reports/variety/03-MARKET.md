# New-game purchase and persistent vehicle market

Fresh or confirmed-reset games now open the native dealership with $4,700 and no free car. Fixed used Juniper 1.6 / 1.4 Economy / 1.6 Club listings cost $4,012 / $3,952 / $3,841 before later balance adjustments; each includes safe factory components, authored mileage, wear and liquids. Legacy earned saves keep their ownership and resources. No actual player save was reset during verification.

After the first purchase, stock contains all 13 new models/trims and seven seeded used listings. Actual fitted hardware, individual condition, histories, mileage and liquids belong to the listing and transfer to the buyer. New inventory replenishes; used listings sell once. Stock refreshes after every third completed non-practice pass. Viewing, cancelling or reloading cannot reroll it. Exact listing/profile/catalogue/policy quotes reject changed or sold stock atomically. Used pricing respects retained asset values and varies between saved deals; immediate part-out is bounded by the existing package pricing floor.

Both dealership interfaces read the same saved stock. The native grid has New/Used filters, mileage/condition, actual fitted-parts information and current/no-wear dyno curves. Existing shared controls, filters, F8 comparisons and explicit confirmation remain. No additional Slice 4 page was advanced.

Checks: market domain 39; paid operations 351 across 13 cars; reset/locked-save/backup/preferences 17. Windows build succeeded; real native input completed 160 checks with zero runtime errors at 1280/1024/1080p and a 4K render target. Screenshots were inspected. The expanded roster requires search expectations to count actual matches rather than assume one pickup. Reset tests now expect empty ownership and fixed seller choices; other runtime fixtures buy an actual starter in their isolated profile before their historical workflow.

Serialization fixes: empty legacy inline lists/fluids must not masquerade as an exact used offer; new stock must not receive used-condition variation; immutable payment-request bytes are retained in quote hashing; a reset creates a new seller identity namespace while keeping starter specifications fixed. Existing strict component health checks remain; history comparisons retain the established bounded numeric round-trip tolerance.

Career opponent authoring and starter skill balance are the next authorized checkpoint. Opening prices may be recalibrated there and reported. New saves have schema 7; this local implementation is technically reviewed separately from user acceptance.
