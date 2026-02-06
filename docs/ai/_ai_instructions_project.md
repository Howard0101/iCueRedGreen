# Project-specific Instructions

## Task Scheduler XML Encoding Exception
- `docs/task-scheduler/*.xml` files must be saved as UTF-16 (LE with BOM).
- Do not run `scripts/normalize-line-endings.ps1` on those Task Scheduler XML files.
- All other files must continue to follow the standard normalization rule.

## Template Governance Override
- Template-governance restrictions are overridden for this repository.

Precedence:
- This file is read AFTER all general instructions and references.
- On conflict, this file overrides previously read rules.
