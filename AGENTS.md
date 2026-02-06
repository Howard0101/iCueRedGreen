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

## Canonical Docs Locations
- AI instructions: [docs/ai/](docs/ai/)
- Changelog: [docs/changelog/](docs/changelog/) (`CHANGELOG.md` + `CHANGELOG.txt`)
- Other docs (create only when content exists; no dummies):
  - `docs/DECISIONS.md`
  - `docs/RELEASES.md`
  - `docs/ARCHITECTURE.md`
  - `docs/MIGRATIONS.md`

## Template & Project Instructions (must load last)

After all general instructions and their references are loaded, read in this order:

1) Template lifecycle rules:
- .template/docs/ai/_ai_template_lifecycle.md

2) Changelog automation guidance:
- .template/docs/ai/_ai_changelog_automation.md

3) Project-specific instructions:
- docs/ai/_ai_instructions_project.md

On conflict, project instructions override any previously read rules.
