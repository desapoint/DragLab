# Independent front/rear wheel geometry — physics checkpoint

2026-10-13. First technical checkpoint in the user-requested staggered-wheel feature. The full request still requires mixed tire specifications, controls, inspection and visuals; work continues within that authorized request.

Optional axle diameter/width/pressure fields inherit old all-wheel settings when absent. Apply/Copy/Clamp/JSON preserve them; existing global settings and equal-axle behavior are retained. Wheel geometry contributes its actual translating mass at the correct axle, individual rotating inertia, pressure/width friction, thermal mass and wear. Driven rolling radius supplies gearing; all-wheel acceleration-equivalent mass is a sum of each inertia divided by its own radius squared.

Wheel contact now accepts four radii/inertias. Its mass-weighted constraints, tire impulses, brake torque, limited-slip coupling and energy ledger support unequal wheel geometry. Locked AWD still constrains angular axle speed, producing scrub with mismatched circumferences instead of silently enforcing equal road speed. Race and physical bench continue using the same simulation. Tire feedback receives the actual axle radii. No part, condition or resource is granted by this additive tune extension.

Checks: new thirteen-car geometry and unequal-inertia energy tests **144/0**; existing contact/energy regression **595** passed; existing mass regression **212** passed. All ran through the Unity Editor compiler. Existing player UI/build remains the preceding checkpoint until the final feature integration build. [Geometry evidence](geometry-verification.txt). The old mass phase report is preserved; rerun evidence is retained locally in this folder. No later UI phase is advanced.
