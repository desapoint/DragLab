# Menu performance physics fixture

`menu-physics-v22.json.gz` contains eight prepared simulation inputs captured before the 2026-10-22 lookup optimization. `menu-physics-v22.csv` records SHA-256 hashes of their full pre-change final state, actual component conditions and complete class inspection. These are deterministic test inputs, not player saves.

Run `DragLab.Runtime.MenuResponsivenessChecks.Optimized` with the Unity CLI to decompress these inputs and require exact output parity. The baseline entry point writes local diagnostic captures only and deliberately cannot overwrite these checked-in hashes. No reduced timestep, shortened race or simplified wear model is used.

Coverage: 3/4/6/8 cylinders, petrol/diesel, FWD/RWD/AWD, worn components and partially filled fuel. Fixture and hashes must change together only for a deliberately reviewed physics-version change; do not regenerate them merely to make an optimization pass.
