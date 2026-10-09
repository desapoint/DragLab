# 2I checkpoint

Car model: `draglab-fuel-v9`. Full Unity foundation gate: **2,195 passing checks**, UTC 2026-10-08T15:14:47.6159117Z, including **46 fuel checks**. Native Windows physical bench: **28 passing checks**, zero runtime/GUI/DSP errors. Evidence: reports/player-fixtures/slice2i-final-driving-refuel.

Consumption covers idle, load, neutral revving, injection overrun fuel cut, ignition versus fuel cut, and anti-lag. Quantity changes actual mass/load. Empty tanks stop combustion; refuel requires explicit restart and does not repair parts. Owned fuel survives normal return/retry/pause/quit/reload. Garage and physical bench have capped quantity sliders and free refuel/fill controls; bench state is isolated. Captured/CSV fuel fields and exact checkpoints repeat through the complete run.

Verification found raw carbureted factory fuel cuts differing from resolved installed builds. EngineBehaviorProfile now consistently uses modeled ignition cuts for carburetors, retaining the same OEM output calibration and compatible controls. UI review found the car sprite obscuring diagnostic text: the diagnostic panel is opaque, and its obsolete 'fuel follows' label was updated. These UI-only follow-ups were compiled with BuildPlayerOnly after the full gate and checked in the native bench.

Fictional consumption calibration, 120 Hz empty-tank quantization. Hard-crash run recovery remains 2K. Fuel pricing and broader economy balance remain Slice 3; refuelling costs zero now. Serialized older zero idle/BSFC calibration fields use documented defaults; initialized empty tanks never refill during migration. Bench reset/repair/refuel do not mutate the player's save.

