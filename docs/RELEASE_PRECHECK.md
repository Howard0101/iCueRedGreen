# Release Precheck (Payload Plane)

## Scope
- Scope: payload-plane (generated repository usage).
- This file defines repository-local, pre-release checks with repo-relative commands only.
- Do not use meta-plane absolute paths in this file.

## Baseline Precheck
1. Confirm current branch and working-tree state:
   - `git status --short`
2. Confirm release workflow instructions are present and up to date:
   - `docs/ai/_ai_release_workflow.md`
3. Validate documentation links (if script exists in repo):
   - `pwsh -NoProfile -File .\scripts\verify-ai-docs.ps1 -Mode check`
4. Validate line-ending policy (if script exists in repo):
   - `pwsh -NoProfile -File .\scripts\check-line-endings.ps1`
5. Run language/project-specific release checks from overlay/project workflow rules.
6. Confirm release artifact destination and naming contract before packaging.
7. For multi-component releases (2+ independently versioned deliverables), verify:
   - `docs/RELEASE_COMPONENT_MATRIX.md` exists.
   - matrix contains target release version coverage.

## Working-Tree Policy Gate
- Before build/package execution, confirm one of:
  - clean working tree, or
  - explicit approval to proceed with pending changes.

## Dependency Exclusion Gate
- If dependencies are intentionally excluded from release artifacts (for example licensed/proprietary runtime files), ensure:
  - exclusion is documented in release workflow instructions,
  - required notice file is included in artifact package.

## Go / No-Go
- Go only when required checks are complete and no blocking errors remain.
- No-Go on failed build/test/package validation, unresolved docs/link failures, missing required release artifacts, or missing/outdated component matrix for multi-component releases.
