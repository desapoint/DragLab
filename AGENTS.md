# DragLab project instructions

These instructions apply to this repository and all its subdirectories.

## Scope and behavior

- Read `docs/objective.md`, `docs/IMPLEMENTATION_PHASES.md` and the current status/report before implementing the next increment. Preserve approved spontaneous requirements as well as the ordered phase plan.
- Work on one slice/subphase or requested change at a time. Keep a runnable checkpoint and do not silently advance later phases.
- For Slice 4, follow `docs/SLICE_4_PLAN.md`: deliver and locally commit one lettered phase, report its checks/build and interface review checklist, then stop for explicit user validation before starting the next. Track technical completion separately from user acceptance; silence or passing automation is not approval.
- Use the same authoritative simulation in races and the physical bench; experimental bench state must remain isolated from player assets and resources.
- Preserve held throttle across shifts, keyboard/slider ownership, installed hardware tuning permissions, explicit Apply/Revert, actual component identities, retained wear/faults/resources and recoverable saves. Do not fabricate missing OEM parts or heal condition during normalization.

## Required local Git workflow

- At the start of work, inspect `git status --short` and recent commits. Identify pre-existing user changes and preserve them.
- Every completed slice, subphase, implementation step and spontaneous user-requested change MUST be committed locally before moving to the next increment or reporting it complete. This includes bug fixes, data/calibration, documentation, tooling and repository-instruction changes.
- Make each commit a coherent, reviewable increment. Use descriptive messages, for example `feat(3e): add qualification events`, `fix(bench): retain throttle through shifts` or `docs: correct UTF-8 report encoding`. Use intermediate commits for substantial work when they form working checkpoints.
- Run checks appropriate to the change before committing. Record checks actually run and material limits in the commit body or relevant phase report. Documentation/repository-only changes need appropriate static checks, not an unnecessary full Unity simulation rerun. Do not label failing or unfinished work as a completed checkpoint.
- Stage explicit paths belonging to the current change, including corresponding tests, data, documentation and Unity `.meta` files. Inspect the staged diff/stat and run `git diff --cached --check`. Avoid blanket staging that sweeps in unrelated user changes.
- After committing, verify the commit and worktree status. Mention the commit hash in the completion response. If only unrelated user changes remain, identify them without modifying or committing them.
- The initial repository commit records the actual current baseline through 3D, including the documentation encoding correction. Do not invent separate historical slice commits for work predating Git.
- Keep history additive. Do not amend, reset, rebase, force-push or discard unrelated changes without explicit user authorization. This repository is local; configure a remote or publish/push only when requested.

## Tracked files and local artifacts

- Track `Game/Assets` with its metadata, `Game/Packages`, `Game/ProjectSettings`, current source art, development/test tools, design documents and concise review reports/matrices.
- Unity already uses Force Text serialization and Visible Meta Files. Preserve those settings and stable GUIDs; include the matching metadata when adding, renaming or deleting an asset.
- Keep Unity caches, IDE-generated files, `Game/Builds`, raw screenshots/audio/logs, generated save fixtures and checkpoint ZIPs out of Git. Leave these local files intact.
- The static `reports/phases/2a/before/legacy-earned-profile-v1.json` fixture is required by migration verification and must remain tracked. New required fixtures must be explicitly versioned rather than relying on ignored generated output.
- Preserve existing phase archives. Update concise reports alongside implementation and state clearly which gates were rerun versus retained historical baselines.

## Text encoding

- Read and write text with an explicit UTF-8 encoding. In Python use `encoding="utf-8-sig"` to read existing files with optional BOM and `encoding="utf-8"` or `"utf-8-sig"` to write intentionally. In PowerShell specify UTF-8 explicitly.
- Never rely on Windows default code-page decoding. Check edited Unicode text for garbled punctuation before committing. Respect `.gitattributes` for text line endings and binary assets.
