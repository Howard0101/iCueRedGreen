# Template Lifecycle – Bootstrap & Upgrade (Codex CLI)

This section governs ONLY the template lifecycle workcase
(new repository bootstrap and existing repository upgrades).

## Authoritative template locations

Local latest template (authoritative):
- D:\Source\ai\Template\.template\version.txt

Repository template version:
- .template/version.txt

## Associated scripts (authoritative)

- Bootstrap (new repositories):
  .template/bootstrap/bootstrap-template.ps1

- Upgrade (existing repositories):
  .template/upgrade/template-upgrade.ps1

## Version format

Template versions are strictly SemVer:
MAJOR.MINOR.PATCH

No prefixes or suffixes are expected.

## Mandatory behavior after loading instructions

1) The assistant MAY automatically read both version files:
   - repo: .template/version.txt
   - local latest: D:\Source\ai\Template\.template\version.txt

2) Compare versions and act as follows:

### A) repo_current < local_latest
Print exactly one update hint:

Human-readable:
"Update hint: Template <repo_current> -> <local_latest>.
Suggested action: run `.template/upgrade/template-upgrade.ps1`.
Waiting for explicit 'go'."

Machine-readable (immediately below):
TEMPLATE_UPDATE repo=<repo_current> latest=<local_latest> status=available

Emit self-check information.

### B) repo_current == local_latest
Remain silent (no output).

### C) repo_current > local_latest
Print a notice:

Human-readable:
"Notice: Repo template version (<repo_current>) is newer than local template (<local_latest>).
Suggested check: update/pull D:\Source\ai\Template or verify branch context.
Waiting for explicit 'go'."

Machine-readable:
TEMPLATE_UPDATE repo=<repo_current> latest=<local_latest> status=repo_newer_than_local

Emit self-check information.

### D) repo version missing or unreadable
Suggest bootstrap or adopt mode and emit self-check information.

## Pre-flight reminder for larger work items

If a larger work item is detected AND a template update is available,
emit a single, non-blocking reminder before proceeding.

A larger work item is detected if ANY of the following apply:
- Keywords: refactor, release, migration, upgrade, rollout, breaking, restructure
- Explicit user intent to plan or implement changes
- Large scope indicators (many files, multiple modules, cross-repo changes)
- Explicit markers such as "major task" or equivalent

This reminder is emitted at most once per session/task.

## Self-check output (automatic)

When emitted, the self-check MUST be concise and include:
- repo_current version
- local_latest version
- active scope = template-lifecycle-only
- current mode = read-only (awaiting 'go')

## Scope restriction (IMPORTANT)

The following restrictions apply ONLY to this template lifecycle workcase:

- Until the user explicitly says "go", this workcase is strictly read-only.
- No bootstrap actions may be executed.
- No upgrade actions may be executed.
- No template-managed files may be modified, staged, or committed.

## Dry-run policy (upgrade only)

When an upgrade is initiated after explicit "go":
1) Run the upgrade script in dry-run / what-if mode first.
2) Summarize the expected changes as a short list (preferred).
3) If summarization is not meaningful, state dry-run completion without details.

After the dry-run:
- Always wait for a second explicit "go" before executing the real upgrade.
- If warnings, conflicts, or potential breaking changes are detected:
  - Recommend "stop and review"
  - Do not block execution if the user explicitly proceeds.


See also:
- `.template/docs/patch_checklist.md`

Note:
- Template patch process rules are defined in `.template/docs/patch_checklist.md` (incl. changelog update policy).

