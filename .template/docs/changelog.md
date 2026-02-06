# Template Changelog

## v1.8.3
- Adopt mode preserves existing files by default; overwrites only template placeholder stubs.
[Added]
- Manifest opt-out: `managedFiles[].mode = "skip"` skips a managed entry explicitly and reports it as skipped (`mode=skip`).

[Changed]
- ReportOnly runs avoid interactive prompts for README configuration (short description + tooling `.SYNOPSIS` fallback).
- Upgrade script now errors clearly if `.template/version.txt` is missing unless the repo is explicitly adopted via `-AdoptFromVersion`.

[Fixed]
- Stabilized list operations by casting ranges passed to `AddRange()` to `[string[]]` to avoid PowerShell type/enumeration edge cases.

## v1.8.2
[Added]
- README marker contract for safe, targeted updates (project name, short description, tooling).
- Optional README migration for upgraded repos (`-MigrateReadmeToV1_8_2`).
- AI instruction for changelog assist that only proposes entries for implemented changes.

[Changed]
- Bootstrap/adopt workflow updates README placeholders automatically by default (opt-out switch).
- Tooling section can be regenerated from template-managed scripts.

[Governance]
- README changes are conservative: only within marker blocks, otherwise skip.
- Changelog proposals must not include ideas/proposals (only implemented items).

[Tooling]
- Tooling list is generated from manifest-managed `scripts/*` entries and script `.SYNOPSIS`.

[Docs]
- Removed broken encoding line in README template.

## v1.8.1
[Added]
- Introduced template lifecycle awareness (bootstrap vs. upgrade).
- Automatic comparison between repository template version and local authoritative template.
- Machine-readable update marker for tooling integration.
- Template lifecycle instructions under `.template/docs/ai/`.

[Changed]
- Upgrade flow enforces dry-run / what-if first, followed by a second explicit `go`.

[Governance]
- Strict read-only mode for template lifecycle operations until explicit `go`.

[Docs]
- Handout moved from ZIP root into `.template/docs/`.

## v1.8.0
[Changed]
- Consolidated instruction loading and guard behavior.
- Prepared repository template for explicit lifecycle management.

[Maintenance]
- No breaking or customer-visible changes.

## v1.7
[Added]
- Binding instruction governance via instruction guard.
- Phase-based workflow (Proposal → Go → Execution).
- Deterministic instruction loading (single entry point).

[Changed]
- Centralized AI instruction files under `ai/`.
- Reordered `_ai_general.md` (structure-only).

[Governance]
- Heading hierarchy, context anchor, and cleanup invariants.

[Tooling]
- `.gitattributes` as authoritative line-ending policy.
- `normalize-line-endings.ps1` repair tool.

## v1.6.2
[Changed]
- Finalized template vs. instruction separation.
- Deterministic instruction load order via `AGENTS.md`.
- Stable documentation structure under `docs/`.

[Governance]
- v1.6 line frozen and backward compatible.

## v1.5.0
[Added]
- Conservative merge / release mode.
- Windows platform instruction rules.
- Release preamble to prevent destructive merges.

[Changed]
- UTF-8 (no BOM) + CRLF enforcement for Windows tooling.
- Improved PowerShell writer guarantees.

## v1.4.4
[Changed]
- Finalized instruction auto-load rules.
- Clear separation between instruction loading and `go` gate.

[Governance]
- GUI auto-load guards stabilized.
- Versioning and AIU window semantics.

## v1.3
[Added]
- Optional documentation tooling.
- AI Codex workflow diagram.

## v1.2
[Added]
- Mermaid label quoting rule.

[Maintenance]
- ZIP repacked to restore full Codex instructions.

## v1.1
[Governance]
- Scope and numbering rules.
- ZIP archive as single source of truth.

## v1.0
[Governance]
- Initial frozen baseline.

### v1.8.3
- Coding style instructions clarified: scope limited to code/scripts; documentation headers reduced.
