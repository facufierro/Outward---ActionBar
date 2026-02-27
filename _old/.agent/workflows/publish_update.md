---
description: Automates the process of releasing a new mod version (update version numbers, changelog, and build).
---

1. **Input Required**:
   - Ask the user for the **Target Version** (e.g., `1.0.2`).
   - Ask for the **Changelog Notes** (list of changes).

2. **Update Version Strings**:
   - Target File: `d:\Projects\Games\Outward\ActionBar\ActionUI.Plugin\ModInfo.cs`
     - Action: Replace `public const string ModVersion = "X.X.X";` with the **Target Version**.
   - Target File: `d:\Projects\Games\Outward\ActionBar\ActionUI.Plugin\manifest.json`
     - Action: Replace `"version_number": "X.X.X"` with the **Target Version**.

3. **Update Documentation**:
   - Target File: `d:\Projects\Games\Outward\ActionBar\CHANGELOG.md`
     - Action: Prepend the new version header (e.g., `## 1.0.2`) and the **Changelog Notes** to the top of the file.
   - Target File: `d:\Projects\Games\Outward\ActionBar\README.md` (Optional)
     - Action: If the user provided significant changes that affect the description, ask if the README needs updating. Otherwise, proceed.

4. **Build Project**:
   - Action: Run the build script.
   - Command: `powershell -ExecutionPolicy Bypass -File .\build_mod.ps1`
   - Directory: `d:\Projects\Games\Outward\ActionBar`
   // turbo

5. **Verify and Notify**:
   - Action: Check that the new zip file exists in `d:\Projects\Games\Outward\ActionBar\bin`.
   - Action: Notify the user that the release is ready and provide the path to the artifact.
