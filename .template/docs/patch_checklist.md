## Template Patch Checklist

[D14]
- Every template patch MUST update `.template/version.txt` to the patch version (SemVer).

[D15]
- Update `.template/docs/changelog.md` ONLY for changes that were actually implemented in this patch.
- Do NOT add planned ideas, proposals, or unimplemented items to the changelog.
- This rule applies ONLY to `.template/docs/changelog.md` and does not restrict other documentation (e.g. handouts).

[D16]
- README automation must be conservative:
  - Only update within README marker blocks (`TEMPLATE:*`) once present.
  - If markers are absent but placeholders are detected, introduce markers first (safe patch).
  - If neither markers nor placeholders exist, do not modify README (only report).
- For upgrades, README migration MUST be opt-in via `-MigrateReadmeToV1_8_2` and must be idempotent.

[D17]
- Any script under `scripts/` that should appear in README Tooling MUST include a PowerShell comment-based help header with `.SYNOPSIS` (single-sentence purpose).
