# Generic layered sprite kit

The kit contains four **original procedural placeholders**, not replicas or certified OEM art: hatchback, coupe, muscle car and crew-cab pickup. Revision 2 improves body proportions, rounded panel silhouettes, door seams, reflections, lamps and pickup bed/cab separation. Approved wheel, rotor and caliper artwork from revision 1 is reused byte-for-byte.

## Source and runtime layout

`art/vehicles/<type>/` holds source PNGs, `car-sprite.json`, hashes and QA. `Game/Assets/DragLab/Resources/Sprites/Vehicles/<type>/` contains runtime copies only; QA previews do not belong in Resources or the build. `art/revisions/v1/` preserves the previous body set. `tools/generate_placeholder_sprites.py` reproduces the current set with Pillow and retains the approved mechanical source bytes.

## Art coordinates

| Property | Value |
| --- | --- |
| Body canvas | 1024 × 384 transparent RGBA |
| Facing | Right |
| Image origin | Upper-left |
| Rear hub | (240, 276) |
| Front hub | (786, 276) |
| Tire art radius | 76 px |
| Mechanical canvas | 192 × 192 |
| Wheel/rotor/caliper pivot | (0.5, 0.5), centered on the same hub |
| Unity import scale | 200 pixels/unit |
| Rear brake scale | 0.88; modeled visual difference only |

The visual wheelbase is deliberately shared across placeholders for quick replacement and does not establish factory dimensions. Physical wheel radius/wheelbase live in vehicle data. Final art can use different anchors through its manifest without changing simulation.

## Drawing stack

Back to front: shadow → local wheel-well backs → rotating rotors → stationary calipers → rotating tire/rim → neutral body paint foundation → shading/linework → glass → trim/lights/mirrors. The body arch opening hides a small upper tire arc; arch backs prevent background showing through the upper gap. Wheel spokes contain transparency. Full tires remain circular and are never erased to fit a fender.

The body paint foundation fills fixture regions with neutral material so it remains continuous. Glass and trim restore those fixture colors above the tint. A full-canvas paint mask excludes the actual fixture alpha and is provided for future shader/repaint workflows. Shading stays separate from tint. The runtime currently multiplies the neutral foundation by a chosen color.

## Replacement workflow

Keep stable vehicle/art IDs or migrate explicitly. Preserve existing assets in a new revision before rework. Prepare a complete side profile; measure its wheel hubs; export separate mechanical and exterior layers. Update canvas dimensions, pivots, axle anchors and stack depths in the manifest. Keep calipers outside the rotating transform set. Front/rear may share wheel art but can have distinct brake scale or assets.

Use the racing-car-sprites skill for future asset extraction and review. Inspect at native/game scale and on white, gray, black and checkerboard backgrounds; test 0°, 45°, 90° and 180° rotations, wheels hidden, isolated mechanics and contrasting recolor. Packaging does not imply real car identity or verified physics values. This kit's evidence and recorded review are stored with the source packages.

Track/effect placeholders include an asphalt tile, barrier tile, lane stripe, finish checker, start-tree housing, four bulb states, smoke and exhaust flame. Some are supplied for subsequent rendering work and are not yet instantiated by the starter scene. All are original and can be replaced independently.
