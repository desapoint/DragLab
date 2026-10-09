# 2G checkpoint — 2026-10-08

Wheel contact solver/model draglab-wheel-contact-v2: 595 isolated checks pass, including 288 launch/open-drive/brake cases across wheel inertia, left/right surface grip, static axle load, open/LSD/welded/spool differentials, locked/unlocked center and 60/120 Hz inputs. Energy and finite-state checks pass. Locked centers preserve open-axle left/right freedom. The same domain solver is exposed in the existing Unity Vehicle Workbench contact lab, with optional recording, live measurements, configuration, reset and repeatable scenario controls.

This is the planned isolated contact increment. Race/AI/physical bench behavior remains the 2F model, draglab-mass-load-v7, whose previous foundation gate passed 1,405 checks and whose Windows controls were verified. Phase 2H must couple existing transmissions to the new solver and replace estimated slip in the real car. No tire-life/thermal claim yet (2J).

Editor scripts compile and domain fixtures exercise the same solver. Interactive editor-tool mouse testing has not been automated; this is distinct from the verified Windows game controls. The checkpoint source and complete Windows folder remain runnable.
