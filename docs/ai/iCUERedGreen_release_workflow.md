# iCUERedGreen Release + Local Update Workflow (Codex)

Purpose: repeatable release/publish flow plus required confirmation prompts.

## Scope
- Applies to iCUERedGreen repository release + local update.
- Includes version bump, changelog/release docs, publish, and local update.
- Uses IncludeCueSdk for local publish/update.

## Preconditions
- You have the SDK DLL available locally (not in git).
- Working tree is clean or you accept committing all touched files.
- Use PowerShell 7+ when available.

## Workflow Steps
1. Read instructions (general + project).
2. Confirm target version (set productive as needed).
3. Update project version in .csproj.
4. Update docs/changelog/CHANGELOG.md and CHANGELOG.txt.
5. Update docs/RELEASES.md with new entry.
6. Normalize line endings for touched files (exclude docs/task-scheduler/*.xml).
7. Run tests: dotnet test .\\iCUERedGreen.sln.
8. Publish: dotnet publish .\\iCUERedGreen.Tray\\iCUERedGreen.Tray.csproj -c Release -r win-x64 --self-contained false -o .\\artifacts\\publish\\win-x64 -p:IncludeCueSdk=true.
9. Run local update: pwsh -NoProfile -File .\\scripts\\update-published.ps1.
10. Git status, stage, commit (Conventional Commits), push.

## Prompts (must be asked and answered before changes)
Q1: Confirm target version?
Q2: Go to update changelog + release docs?
Q3: Go to publish with IncludeCueSdk and update locally?
Q4: Go to commit and push?

## Notes
- Always include IncludeCueSdk for local publish/update.
- SDK DLL and iCUESDK folder must remain untracked in git.
- After publish, always run update script.
- Commit all touched files.

