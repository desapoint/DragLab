# Screenshot grouping by actual size (2026-10-13)

The resolution-folder clarification is enforced from PNG header dimensions, not just naming conventions. Initial 1280×720 captures go at the review root; every other actual size goes to `resolution-tests`, including images with no resolution suffix. The ledger records each location for repeat-run cleanup. Filename hints remain only for an incomplete/non-PNG diagnostic artifact.

The noninteractive export checks pass with normal and alternate PNG headers deliberately named contrary to their dimensions, along with prior repeat, failed-run, publisher-preservation and path guards. This is a tooling refinement, not an additional game interface or test-suite acceptance claim. The ongoing full native run uses this exporter; source/date manifests remain available in `run-info`.
