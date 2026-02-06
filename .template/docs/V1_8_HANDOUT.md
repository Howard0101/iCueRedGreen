# Template Version 1.8 — Handout

## Document Scope

This document provides **orientation, intent, and usage context** for the template release.

**It is not a changelog.**
The authoritative record of implemented changes is maintained in:

- `.template/docs/changelog.md`

This handout may summarize concepts or intent, but MUST NOT duplicate or replace the changelog.


**Status:** ❄️ **Frozen**  
**Audience:** Repo owners, maintainers, reviewers  
**Scope:** What changed, how to upgrade/adopt, and what to expect

---

## What is V1.8?
Version 1.8 finalizes the template lifecycle with **deterministic upgrade/adoption tooling**, safer file handling, and clearer ownership boundaries between template and project content.

No breaking changes for normal usage. Existing repos can be upgraded safely.

---

## Highlights

## v1.8.3 Notes (Minor)
V1.8.3 is a small, conservative update focused on explicit opt-outs and better non-interactive reporting:

- **Explicit opt-out for managed files:** manifest entries can be skipped via `mode = "skip"`.
- **ReportOnly is non-interactive:** README short description and tooling extraction avoid prompts during report-only runs.

See `.template/docs/changelog.md` for the authoritative list of implemented changes.

---

### 1. Deterministic Template Lifecycle
- **Bootstrap** for repos without template
- **Upgrade** for repos with template
- **Adopt** path for legacy repos (e.g. V1.7 without version file)

Clear separation:
- *Bootstrap* ≠ *Upgrade*
- No implicit or silent adoption

---

### 2. Explicit Version Tracking
- Template version is stored in:
  ```
  /.template/version.txt   (SemVer X.Y.Z)
  ```
- Version is authoritative and required for upgrades.

---

### 3. Legacy Repo Adoption (V1.7 → V1.8)
Repos that already contain template files but **no version.txt** can be adopted:

```powershell
.template/upgrade/template-upgrade.ps1 -AdoptFromVersion 1.7.0
```

Rules:
- Adoption is **explicit**
- No guessing, no silent behavior
- Adoption is logged in reports

---

### 4. Safe, Transparent Upgrades
Upgrade script features:
- Modes: `Safe` (default), `Strict`, `ReportOnly`
- Backups in Safe mode
- Markdown **and** JSON reports
- Markdown summary printed to console

---

### 5. `.gitignore` is Merge-Additive
- `.gitignore` is **never overwritten**
- Template-managed entries live in a dedicated block:
  ```
  ### BEGIN TEMPLATE-MANAGED (DO NOT EDIT)
  ...
  ### END TEMPLATE-MANAGED
  ```
- Only missing template entries are added
- User entries are never removed

Authoritative source of template ignore entries:
```
.template/managed/gitignore.txt
```

---

### 6. Improved Line Ending Normalization (P2)
`normalize-line-endings.ps1` now supports:
- `-File <path>` (single file)
- `-AllFiles` (tracked + untracked, with excludes)
- `.gitattributes`-driven EOL policy
- Safe handling of binaries
- UTF-8 without BOM

---

### 7. Clear Parameter Semantics
Upgrade parameters are **strictly validated**:

- `-FromVersion` and `-AdoptFromVersion` are **mutually exclusive**
- `-FromVersion` requires an existing `version.txt`
- Violations result in **fatal errors**, not guesses

---

## Typical Commands

### Bootstrap (repo without template)
```powershell
.template/bootstrap/bootstrap-template.ps1
```

### Adopt legacy repo (e.g. V1.7)
```powershell
.template/upgrade/template-upgrade.ps1 -AdoptFromVersion 1.7.0
```

### Normal upgrade
```powershell
.template/upgrade/template-upgrade.ps1
```

### Dry run / report only
```powershell
.template/upgrade/template-upgrade.ps1 -DryRun
```

---

## What V1.8 Does NOT Do
- No automatic adoption
- No silent overwrites
- No forced documentation artifacts (e.g. diagrams)
- No repo-specific logic guessing

---

## Final Notes
- V1.8 is **stable and frozen**
- All decisions are explicit and documented
- Tooling is designed to be **Codex-friendly and token-efficient**
- Future versions can build on this foundation without ambiguity

---

**Template Version:** 1.8.0  
**Status:** Frozen ❄️
## Patch v1.8.2 (README Automation)
This patch introduces:
- Marker-based README updates for project name, short description, and tooling.
- Opt-in README migration on upgrade via `-MigrateReadmeToV1_8_2`.
- Tooling list generation from manifest-managed scripts and `.SYNOPSIS`.

## Patch v1.8.1 (Template Lifecycle AI Rules)

This patch adds template lifecycle instructions for Codex CLI:
- Automatic comparison of repository `.template/version.txt` with the local authoritative template version
  at `D:\Source\ai\Template\.template\version.txt`
- If an update is available, the assistant prints one human-readable hint plus a machine-readable marker:
  `TEMPLATE_UPDATE repo=<...> latest=<...> status=available`
- The template lifecycle workcase is read-only until you explicitly say "go".
- Upgrade flow recommends a dry-run/what-if first, then waits for a second explicit "go" before applying changes.

### Script workflow clarification
For scripts, a strict separation between design and implementation applies.
Script changes are designed and specified first, implemented through a dedicated handoff,
and reviewed afterwards. This ensures auditability and prevents accidental mutations.
### Coding style clarification
Coding style rules apply only to code and scripts. Documentation files follow separate, lightweight conventions.
