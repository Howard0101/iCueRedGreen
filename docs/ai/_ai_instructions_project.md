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

Precedence:
- This file is read AFTER all general instructions and references.
- On conflict, this file overrides previously read rules.
