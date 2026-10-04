TokenFish 0.2.0-beta.1 - Windows x64 portable candidate

Extract the whole ZIP to a permanent folder and run TokenFish.App.exe.
Keep all bundled files together. No .NET development SDK is needed.
This candidate is unsigned and has no installer or automatic updater.
Windows 11 x64 is the beta target; clean-machine validation is pending.

Choose Codex, Claude, or both during setup.
Codex requires a separately installed, signed-in Codex CLI. Select the
Native Windows or WSL runtime matching where you installed it.
Claude requires manual status-line configuration. Settings provides
Windows and WSL snippets for tools/claude/TokenFish.ClaudeBridge.exe.
If you move this folder, update Claude Code's configured bridge path.

Click the tray icon for the full view. Settings -> Desktop enables the
small corner widget. Drag its grip to a corner; click usage to open the
full view; its menu opens Settings or hides it. Always-on-top is optional.
System, Light, and Dark appearance apply immediately. Saved connection
changes in Settings require a restart. Exit using the tray menu before
updating the application folder.

TokenFish stores settings and normalized Claude observations locally
under %LOCALAPPDATA%/TokenFish. It has no backend or telemetry and does
not store provider credentials. Quota percentages are independent of
token activity. Missing activity dates are not assumed to be zero.

See README.md and docs/releases/0.2.0-beta.1.md in the source repository
accompanying this candidate. The project license must be selected before
a public release. This is a local review candidate.
