# Repository Release Workflow Overrides (Layer 3)

## Purpose
- Repository-local release workflow overrides.
- Loaded only for explicit release tasks.
- This file is optional: if no repo-specific overrides exist, Layer 1 + Layer 2 remain authoritative.

## Precedence
1. Layer 1 global core: `D:\Source\ai\_ai_release_workflow.md`
2. Layer 2 language overlay: language instruction files
3. Layer 3 repo-local override: this file

On conflict, this file can specialize local behavior but must not weaken lower-layer safety constraints.

## Anti-Duplication Rule
- Do not copy or restate Layer 1 core text or Layer 2 language defaults unless an explicit repository override is required.
- Keep this file delta-only: repository-specific values, constraints, and workflow specifics.

## Legacy Migration Note
- Older repositories may still contain copied core/overlay text in this file.
- During cleanup, move generic/core text out of this file and keep only repository-specific overrides.
- If no repository-specific overrides remain, keep this file minimal and state that no local overrides are defined.
- Use `docs/RELEASE_WORKFLOW_MIGRATION.md` as the standard checklist for generalized-script adoption migrations.

## Allowed Local Overrides
- Release branches/tags and approval ownership.
- Repository-specific artifact destinations and naming details.
- Repository-specific package file list requirements.
- Repository-specific publish destinations and verification checks.
- Repository-specific multi-component trigger mapping and component definitions.

## Not Allowed
- Reordering/removing global core phases.
- Skipping failure-stop behavior for failed validation.
- Converting release artifacts into committed source files.

## Repository Overrides
- No repository-specific overrides are defined yet.

## Precheck Reference
- Use `docs/RELEASE_PRECHECK.md` as the reusable, repository-local pre-release checklist.
- Keep precheck commands repo-relative in payload repos.
- Keep command-level operational detail out of this file; this file remains policy/override delta-only.

## Suggested Override Structure (Use When Needed)
### Release Versioning Scheme
- Define repository-specific release tag/version format if it differs from defaults.

### Release Package File List
- Define repository file-list contract with stable IDs (for example `RF-001`) and expected target paths.

### Release Component Matrix
- When a release includes 2+ independently versioned deliverables, `docs/RELEASE_COMPONENT_MATRIX.md` is mandatory.
- Matrix must include target release-version coverage before publish/tag.
- Keep matrix fields aligned with Layer 1 minimum requirements.

### Include/Exclude/Transform Rules
- Define repository-specific include/exclude rules.
- Define staging-time transformations (rename/convert) if required.

### Documentation Conversion Map
- If release packaging converts documentation formats (for example `.md -> .txt`), define source-to-target mappings explicitly.
- Define required output encoding/line-ending policy (for example UTF-8/CRLF as project policy requires).
- Define validation checks confirming converted files exist and are included in release staging/package.

### Dependency Exclusion + Notice Policy
- Define dependencies that must be excluded from release artifacts (for example licensed/proprietary runtime DLLs).
- Define required notice files and placement when exclusions apply.

### Deferred Execution Workflow (Wait for `go`)
- Define repository-specific deferred steps that require explicit user approval before execution.

### Prompt Gate Checklist
- Define mandatory release decision prompts and expected confirmations before critical steps.
- Keep prompts concise and map each prompt to the corresponding execution phase.

### Post-Publish Local Update Hook
- If repository policy requires local environment update after publish, define command/script and execution point.

### Verification and Publish Checks
- Define repository-specific validation checks before publish/tag.
- Define repository-specific publication destinations and post-publish verification.
