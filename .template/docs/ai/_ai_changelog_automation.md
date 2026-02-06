# Changelog Automation Guidance

Author: Sven Widowski
Copyright: Sven Widowski
Version: 1.8.2

1) Changelog is the authoritative record of implemented changes.
2) When working on template patches, the assistant MAY propose changelog entries derived ONLY from:
   - actually modified template-managed files (manifest-managed), and
   - explicitly confirmed implemented changes.
3) The assistant MUST NOT add ideas, proposals, or unimplemented items to the changelog.
4) The assistant MUST wait for explicit "go" before writing or patching any files.
5) For multi-source inputs per version, the assistant must synthesize without duplicates.
6) Changelog categories must be in square brackets; omit empty categories.
