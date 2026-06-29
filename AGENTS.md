# AGENTS

## General Instructions (authoritative)
Instruction source is external and authoritative.

Load and apply the general instruction file:
1. `D:\Source\ai\_ai_general.md`

All referenced instruction files (e.g. versioning) are loaded implicitly as defined there.

These instructions are the single source of truth.
Do not duplicate or override them locally.

## Project Instructions (must load last)
After all general instructions and their references are loaded, read:
- [docs/ai/_ai_instructions_project.md](docs/ai/_ai_instructions_project.md)

On conflict, project instructions override any previously read rules.

## Documentation Governance (conditional)
Load this file for documentation/changelog/instruction-file tasks:
- [docs/ai/_ai_documentation_governance.md](docs/ai/_ai_documentation_governance.md)

## Canonical Docs Locations
- AI instructions: [docs/ai/](docs/ai/)
- Changelog: [docs/changelog/](docs/changelog/) (`CHANGELOG.md` + `CHANGELOG.txt`)
- Other docs (create only when content exists; no dummies):
  - `docs/DECISIONS.md`
  - `docs/RELEASES.md`
  - `docs/RELEASE_COMPONENT_MATRIX.md` (required for multi-component releases)
  - `docs/ARCHITECTURE.md`
  - `docs/MIGRATIONS.md`

## Conditional Release Workflow Instructions (load only for release tasks)

Do NOT auto-load release workflow instruction files for normal implementation tasks.

Load release workflow instructions only when release intent is explicit, for example:
- release planning/execution
- tag or publish workflow
- release artifact packaging/upload
- release notes/frozen release record updates

Release workflow files:
1) Global core (Layer 1):
- D:\Source\ai\_ai_release_workflow.md

2) Language overlay (Layer 2):
- loaded from language instruction files referenced by `D:\Source\ai\_ai_general.md`

3) Repository-local override (Layer 3):
- docs/ai/_ai_release_workflow.md
