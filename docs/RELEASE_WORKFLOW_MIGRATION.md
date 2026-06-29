# Release Workflow Migration (Generalized Scripts Adoption)

## Purpose
- Migrate repository-local release instructions to the generalized release-script model.
- Keep release workflow instructions concise and delta-only.

## Scope
- Applies when a repository adopts generalized release scripts from the meta plane.
- Focuses on instruction-file cleanup and operational-detail relocation.

## Migration Checklist
1. Ensure baseline files exist:
   - `docs/ai/_ai_release_workflow.md`
   - `docs/RELEASE_PRECHECK.md`
2. Review `docs/ai/_ai_release_workflow.md` and remove generic Layer 1/Layer 2 text copies.
3. Keep only repository-specific overrides in `docs/ai/_ai_release_workflow.md`.
4. Move command-level operational details (script commands, step-by-step execution commands) to:
   - `docs/RELEASE_PRECHECK.md`
   - script files under `scripts/`
5. Re-run migration check and confirm no remaining slimming indicators.
6. For multi-component releases (2+ independently versioned deliverables), ensure:
   - `docs/RELEASE_COMPONENT_MATRIX.md` exists,
   - matrix includes target release version coverage before publish/tag.

## EOL Script Migration (Automatic)
1. Run template upgrade from the meta plane.
2. Confirm payload scripts were migrated to shared wrappers:
   - `scripts/check-line-endings.ps1`
   - `scripts/normalize-line-endings.ps1`
3. Confirm wrappers forward to `D:\Source\ai\scripts\...` and fail when meta path is unavailable.
4. Confirm payload `.gitattributes` remains the repository-local EOL policy authority.

## Recommended Migration Action
- From meta repo context, run:
  - `pwsh -NoProfile -File .\scripts\release\release-migrate-instructions.ps1 -TargetRepoPath <repo-path>`
- Apply create-if-missing migration scaffolding when needed:
  - `pwsh -NoProfile -File .\scripts\release\release-migrate-instructions.ps1 -TargetRepoPath <repo-path> -Apply`

## Upgrade Source Policy

- Default upgrade source is released payload (`D:\Source\ai\Releases\current\invent_repo`).
- For normal upgrades, do not pass `-TemplateSourcePath` or `-UseWorkingPayload`.
- Use `-TemplateSourcePath D:\Source\ai\Template -UseWorkingPayload` only when intentionally testing unreleased template changes.

## Success Criteria
- `docs/ai/_ai_release_workflow.md` is delta-only and policy/override focused.
- `docs/RELEASE_PRECHECK.md` holds executable pre-release command detail.
- No copied global core phases remain in repo-local release workflow instructions.
