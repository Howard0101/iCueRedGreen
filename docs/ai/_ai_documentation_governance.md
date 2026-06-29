# Documentation Governance Guidance (Layer 3 Local Policy)

## Scope
- Repository-local documentation governance defaults.
- Applies to docs, changelog maintenance, and documentation-link hygiene.

## Baseline
1. Keep `README.md` index-only; do not duplicate detailed release/changelog content.
2. Create optional docs files only when content exists.
3. Keep release procedure details in `docs/ai/_ai_release_workflow.md`.
4. Keep stable project rules in `docs/ai/_ai_instructions_project.md`.

## Changelog Requirements
1. Changelog files remain authoritative for implemented changes.
2. Do not record ideas/proposals/unimplemented work in changelog.
3. Validate changelog format with:
   - `pwsh -NoProfile -File .\scripts\release\check-changelog-format.ps1 -Mode Validate`
4. Use strict pre-release validation before packaging:
   - `pwsh -NoProfile -File .\scripts\release\check-changelog-format.ps1 -Mode Validate -Strict`

## Compatibility
- Older references may still point to changelog-only guidance files.
- Repository policy is now consolidated under this documentation-governance file.
