# Local version control

Git history begins with the current working baseline through phase 3D, including the UTF-8 documentation correction. Older phase ZIP checkpoints remain local historical artifacts; they are not reconstructed as Git commits. The repository uses the local `main` branch and the existing Git author configuration. No remote is configured.

## What is versioned

- Unity source, scenes, resource/data assets, artwork and all associated `.meta` files in `Game/Assets`.
- Package manifest/lock and `Game/ProjectSettings`, including Editor version, Force Text serialization and Visible Meta Files.
- Current source art and its provenance/review files, development/test scripts, README, design documents and `AGENTS.md`.
- Concise phase/change/verification reports and calibration matrices/manifests. The static legacy migration fixture in `reports/phases/2a/before/legacy-earned-profile-v1.json` is retained because the verification code reads it.

Unity/IDE caches, user/editor settings, Windows builds, raw test logs/screenshots/audio/save fixtures/time-series traces, ZIP checkpoints and duplicate historical art exports are ignored. These files are left on disk and can be regenerated or reviewed locally. Existing archives are not overwritten. `.gitattributes` stores source/YAML text with LF endings and preserves binary assets byte-for-byte.

## Required workflow

Before a change, inspect `git status --short` and recent history. Preserve unrelated user edits. Complete one slice/subphase, implementation step or spontaneous requested change; validate it with appropriate checks, update its report/docs, stage only its relevant paths and inspect the staged diff. Run `git diff --cached --check`, then commit locally with a descriptive message before proceeding or reporting completion. Include the commit hash in the result. Documentation-only changes do not require a Unity rebuild.

A commit is required for bug fixes, tuning/data, documentation, tooling and repository instructions as well as planned feature phases. Do not invent historical commits, amend/rewrite existing history, push or add a remote without authorization. The full agent rules are in [AGENTS.md](../AGENTS.md).

For a new checkout, open `Game` with the Editor version in `Game/ProjectSettings/ProjectVersion.txt`, allow Unity to regenerate its Library, and use the existing preparation/verification/build tools. Generated test captures use isolated local saves; the normal player save is outside the tracked project. The existing Windows build remains local in `Game/Builds/Windows`.
