# Project-specific Instructions

## Task Scheduler XML Encoding Exception
- [`docs/task-scheduler/*.xml`](../task-scheduler/) files must be saved as UTF-16 (LE with BOM).
- Do not run [scripts/normalize-line-endings.ps1](../../scripts/normalize-line-endings.ps1) on those Task Scheduler XML files.
- All other files must continue to follow the standard normalization rule.

## Template Governance Override
- Template-governance restrictions are overridden for this repository.
- doc-only changes in human-readable markdown do not require a version bump

## Local Publish Requirement
- When updating the local deployment, always publish with `-p:IncludeCueSdk=true` (requires the SDK DLL to exist locally).

## Release Workflow (Mandatory)
- Follow `docs/ai/iCUERedGreen_release_workflow.md` for release + local update steps and prompts.

Precedence:
- This file is read AFTER all general instructions and references.
- On conflict, this file overrides previously read rules.

## Release List Document

- Use `docs/RELEASES.md` as the human-readable release list file (plural).
- Keep release entries newest-first.
- `docs/RELEASES.md` may include planned/non-productive notes.
- When a productive release is produced, add/update a concrete released-version entry in `docs/RELEASES.md`.
- If `docs/RELEASES.md` exists, `README.md` MUST include a Documentation-section link to it.
- If `docs/RELEASES.md` does not exist, `README.md` MUST NOT link it.
- `docs/changelog/CHANGELOG.md` remains the authoritative implemented-change record.
- For multi-component releases (2+ independently versioned deliverables), maintain `docs/RELEASE_COMPONENT_MATRIX.md` and ensure it includes target release version coverage before publish/tag.

## Changelog Release Header Rule

- For each produced release (productive, beta, or pre-release), create/update a dedicated changelog version header for that exact released version.
- Do not keep produced release entries only under `Unreleased`; move or duplicate them into the matching version section during release finalization.
- If changelog files are included in release artifacts, sanitize hidden parameters in staged release-copy changelog files before package creation.
- Changelog sanitization for release artifacts must not mutate source changelog files.

## Changelog Internal Tags (Unreleased)

- For `docs/changelog/CHANGELOG.md` entries under `## Unreleased`, prefix each bullet with an internal scope/priority tag.
- Canonical tags:
  - `[meta-only]` (default): change does not affect distributable/release-relevant payload behavior.
  - `[payload-impact]`: change affects distributable payload behavior, upgrade behavior, or release-relevant repo behavior.
  - `[priority]`: change should be rolled out quickly (optional additive tag).
- Tagging rules:
  - Always include exactly one scope tag (`[meta-only]` or `[payload-impact]`).
  - Add `[priority]` only when explicitly classified as urgent.
  - By default, assign the appropriate tag(s) autonomously unless user/project rules specify otherwise.
- Release-finalization rule:
  - Internal tags are for triage in `Unreleased`; remove them when moving entries into released version sections.

## README and Release Docs Linkage Rules

- `README.md` remains index-only and human-readable; do not duplicate detailed release or changelog content in README.
- Release documentation links in `README.md` are conditional by file existence:
  - link `docs/RELEASES.md` only when the file exists
  - do not add placeholder links to non-existing release docs
- `docs/RELEASES.md` summarizes release communication context (human-readable).
- `docs/changelog/CHANGELOG.md` remains authoritative for implemented change details.
- Do not duplicate full implemented-change bullets from changelog into `docs/RELEASES.md`; prefer short references/summaries.

## Release Artifact Policy (Single vs Multi-Project)

- Single-project repositories default to human-readable release tracking:
  - `docs/RELEASES.md`
  - `docs/changelog/CHANGELOG.md`
- Multi-project solutions additionally require machine-readable solution release artifacts when release automation/version orchestration is used:
  - `ReleaseVersion.txt`
  - `ReleaseInfo.json`
- For multi-project solutions, machine-readable release artifacts describe the aggregate solution release; project-level versions remain project-specific.

## Changelog Process (Multi-Project, Flat Layout)

Scope:
- Applies when multiple executables/projects share one repository root without separate project folders (for example AutoIT or similar flat-layout languages).
- Does NOT replace language/project-folder-native changelog handling (for example C# solutions with separate project folders).

Rules:
1. Keep project-specific changelogs per executable/project under `docs/changelog/`:
   - `<ProjectName>.CHANGELOG.md`
   - `<ProjectName>.CHANGELOG.txt`
2. Keep entries newest-first.
3. Changelog categories MUST use square brackets (for example `[Added]`, `[Changed]`, `[Fixed]`), omit empty categories.
4. Record only implemented/confirmed changes (no proposals, no future ideas).
5. For project-level changes, update only the corresponding project changelog files.
6. Existing root changelog files (`docs/changelog/CHANGELOG.md` and `.txt`) are optional aggregate release logs and should only contain cross-project/release-level entries when aggregate mode is explicitly enabled.
7. If aggregate mode is enabled, each aggregate release section should identify project context clearly (for example project-tagged subsections).

Default recommendation:
- Use per-project changelog files as the authoritative source for project changes.
- Add aggregate release logs only if explicitly requested for release communication.

## Git Commit and Push Rule

- After any meaningful change (code, docs, config, tests), create a commit in the same work cycle unless explicitly told not to commit.
- Push in the same work cycle unless explicitly told not to push.
- Target push branch MUST be project-defined in this file (for example `origin/main`) and can be changed per repository workflow.
- If protected-branch or PR-only workflow is used, this section must define the allowed push/review path explicitly.
- Execute git write operations sequentially (`add` -> `commit` -> `push`); do not run dependent git writes in parallel.
- If `.git/index.lock` exists, first verify no active git process is running; only then remove stale lock and retry.

## TEMP Lifecycle Wrapper Rule

- Action phrase: `run temp lifecycle check`
- Execute: `pwsh -NoProfile -File .\scripts\invoke-temp-lifecycle-check.ps1 -Action <Clean|BootstrapReport|UpgradeReport|BothReport|LifecycleSuite|UpgradeReadmeObsoleteReport|ReleasePrecheck> [-ReleaseComponentCount <n>] [-ReleaseComponentMatrixPath <path>]`
- Use `LifecycleSuite` as quick default validation for template lifecycle checks.
- Use this wrapper for all TEMP lifecycle testing and TEMP cleanup commands.
- Do not run direct ad-hoc TEMP commands (for example manual `Remove-Item` or custom `pwsh -Command` TEMP cleanup/testing), unless the user explicitly approves an exception.
- If wrapper coverage is missing for a needed TEMP action, extend the wrapper first, then run through the wrapper.

## EOL Governance Reuse Rule

- Use shared EOL scripts via payload wrappers:
  - `pwsh -NoProfile -File .\scripts\check-line-endings.ps1`
  - `pwsh -NoProfile -File .\scripts\normalize-line-endings.ps1`
- Payload wrappers are strict forwarders to meta scripts at `D:\Source\ai\scripts\...`.
- If the meta script path is unavailable, fail instead of falling back to local duplicate logic.
- `.gitattributes` in the payload repository remains the local authority for EOL policy.

## Template Upgrade Alias

- Action phrase: `run template migration upgrade`
- Execute behavior:
  1. Load and follow `D:\Source\ai\docs\MIGRATIONS.md`.
  2. Run upgrade in report-only mode first and present a concise report summary.
  3. Wait for explicit `go` before any non-report/apply upgrade execution.

## Instruction Separation Rule (Project vs Release Workflow)

- Keep `docs/ai/_ai_instructions_project.md` focused on stable project constraints and non-release operational rules.
- Keep release execution flow, package file lists, release prompts/checklists, and publish steps in `docs/ai/_ai_release_workflow.md`.
- If release-specific content is found in project instructions, move it to release workflow instructions unless it is a long-lived project invariant.
- When both files exist, treat project instructions as baseline project policy and release workflow as release-task procedure.
