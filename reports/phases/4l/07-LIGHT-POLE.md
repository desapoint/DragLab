# 4L.7 — raised start lights and grounded pole (2026-10-14)

Raise the existing start-light housing by 1.1 world units and add a narrow shaded metal support. The pole's foot is the same projected far-road-edge point previously used by the housing. Its top overlaps the housing slightly to avoid a visible gap. Housing, lenses and pole scroll as one upright assembly, retaining distant-object layering behind cars. Countdown, car scale, road, physics and persistence are unchanged.

Windows build succeeds; packaged native race integration passes **131 checks / 0 runtime errors** ([evidence](07-native.txt)). New checks verify foot alignment, the raised housing/pole joint and movement of the complete assembly. The same run retains input, instrument, projection, pause and resource-exit regressions. Reviewed the staged amber frame; captures are local under reports/player-fixtures/race-raised-lights/ and the interface gallery. UTF-8/whitespace checks pass; verified generated font whitespace was restored. This is technical completion, pending user visual acceptance. Stop before other UI controls.

A separately reported user-save failure after finishing a race is not claimed fixed by this visual checkpoint; it is the next bug investigation.
