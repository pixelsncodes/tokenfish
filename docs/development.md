# Developing TokenFish

## The development story

TokenFish began as a Windows tray usage companion and evolved through small, testable changes. The owner directed the product and visual choices, with Codex assisting in implementation, review, documentation, and release preparation.

The current beta followed a source/build audit, a redesign plan, and an approved mockup. Feedback kept both Codex and Claude in scope and made the fish an essential part of the identity. The work added the compact desktop widget, adjacent Settings placement, shared appearance, clearer quota/freshness labels, corrected onboarding routes, and bounded Codex requests. Desktop inspection reproduced the white window rim; a native client-area fix removed it. The token-eating mouth animation was restored after the static redesign lost that detail.

This is an iterative development record, not a claim that every environment has been validated. The current beta passed 825 automated cases and local desktop checks. Clean-machine, broader DPI/monitor, accessibility, and redistribution checks still matter before a stable release. See [the beta validation record](releases/0.2.0-beta.1.md).

## Solution structure

| Project | Responsibility |
| --- | --- |
| `TokenFish.App` | WinUI windows, tray integration, shared appearance, fish rails, widget and setup/settings UI |
| `TokenFish.Core` | Provider-neutral usage/settings models and contracts |
| `TokenFish.Infrastructure` | Refresh/runtime orchestration, persistence, display-state conversion and layout calculations |
| `TokenFish.Providers.Codex` | Local App Server transport, quota/activity parsing, runtime selection and recovery |
| `TokenFish.Providers.Claude` | Normalized local bridge observations and source freshness |
| `TokenFish.ClaudeBridge` | Status-line input normalization and allowlisted local quota output |
| `tests/` | Focused Core, Infrastructure and provider tests using synthetic data |

The desktop presentation consumes normalized display state. Provider-specific transport and parsing stay in their adapters. Keeping quota independent of optional activity lets the app display useful limits even when extra metrics are unavailable.

## Build and validate

Use Windows x64 and the .NET SDK pinned by `global.json` (10.0.401, with patch roll-forward). Restore/build installs the project dependencies; end users of the self-contained portable ZIP do not need this SDK.

```powershell
dotnet build TokenFish.sln -p:Platform=x64
dotnet test TokenFish.sln -p:Platform=x64
pwsh -NoProfile -File scripts/package-portable.ps1
```

The package script runs the build and tests, publishes to a fresh staging directory, checks required files, rejects common personal/debug artifacts, checks binaries for the development checkout path, and writes a portable ZIP plus SHA-256 manifests. The Windows GitHub workflow uploads these as run artifacts; publishing a GitHub release is a separate step.

The latest local x64 Debug build passed without warnings/errors. Tests: Core 100, Infrastructure 408, Codex 247, Claude 70. The Release publish has two known SDK/runtime IL2104 trimming warnings; see the validation record for their scope.

## Contribution expectations

Follow [AGENTS.md](../AGENTS.md): inspect the working tree, keep changes focused, preserve existing work, prefer the established structure, add focused behavioral tests, and run relevant build/test commands. Keep provider integrations, tray behavior and database work out of unrelated changes.

Use synthetic fixtures. Do not commit authentication, provider payloads, personal settings, database files, logs, or screenshots containing personal account/workspace details. The GitHub media uses clearly labeled example values.

For an isolated UI check, set `TOKENFISH_SETTINGS_PATH` to a separate absolute file and launch with `--show`, `--settings`, or `--inspect-windows`. Only app settings are redirected; provider authentication and Claude observations remain in their normal locations. Use the inspection flag only for development.

Visual changes need a desktop inspection as well as unit tests. Verify both providers, stale/missing states, window placement and settings, and the current Windows motion preference. Do not treat screenshot mockups as proof of native UI behavior.

## Regenerate the GitHub media

The mockups are drawn from the native palette, fish geometry and layout with example data. They are documentation assets, not another implementation of the desktop app. The small GIF uses the same two mouth poses and 340-millisecond loop; its fixed sample quota does not imply a live collection.

With Python and Pillow installed, run:

```powershell
python scripts/render-github-media.py
```

Windows uses Segoe UI. A DejaVu Sans fallback supports other development environments. Check the exported images after changing the renderer. The promo banner was created with the built-in imagegen tool; its source prompt and all deliverable paths are recorded in [media notes](media/README.md).

## Release boundaries

The beta is an unsigned portable Windows x64 folder. It currently has no installer, automatic update, launch-at-login, alerts, or validated ARM64 build. License selection, redistribution notices, publisher metadata, signing and the remaining clean-machine/display checks precede a stable release. These are tracked in the [release plan](tasks/release-readiness-plan.md).
