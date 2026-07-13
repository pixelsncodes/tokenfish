# TokenFish Local MVP Status

## Completion

The local MVP is complete at the repository level. TokenFish is a local Windows tray application, not a public release or installed product.

Implemented local-MVP capabilities include:

- WinUI application lifetime, idempotent shutdown, native tray menu, and same-publish single-instance activation.
- Codex App Server collection through WSL login-shell, WSL direct, or native Windows runtime modes, with optional WSL distribution selection.
- Claude status-line parsing, a local bridge executable, normalized bridge-state storage, and Claude snapshot collection.
- Codex, Claude, and combined provider selection; Settings readiness; shared tray/popup refresh; generic recovery presentation; and in-memory latest-successful snapshots.
- Compact accessible popup and Settings surfaces, active-monitor Settings placement, stable quota progress, and content sizing validated at 100% and 125% scaling.
- Self-contained App publish with a bundled Claude bridge and a separate self-contained bridge publish.

## Validation Record

The completed local validation recorded:

- Build completed with 0 warnings and 0 errors.
- Test suite completed with 706 passed, 0 failed, and 0 skipped.
- `git diff --check` passed.
- App and independent bridge publishes passed. The App publish emitted only accepted `Microsoft.Windows.SDK.NET` and `WinRT.Runtime` `IL2104` trim-analysis warnings.
- Publish scans found no TokenFish PDBs, local repository or username-path leakage, or packaged TokenFish settings, state, logs, credentials, dumps, prompts, responses, histories, source captures, or browser data.
- Published App smoke, same-publish second-launch redirection, tray exit, process cleanup, and Application event-log checks passed.
- Popup sizing passed at 100% and 125%; Settings sizing passed at 125%.
- Native Windows and WSL Claude guidance passed, and real WSL Claude integration passed without administrator privileges.
- Codex and Claude connected together; shared tray/popup refresh, repeated-refresh gating, close/reopen during refresh, progress-bar stability, and stale-state presentation passed.

The following manual states were not forced because no safe reversible method was available without altering valid provider or bridge state: waiting, unavailable/disconnected, and recovery/failure. Automated coverage exists for those states; this is not a manual-pass claim.

## Limitations And Deferred Work

- This is a local MVP. There is no installer or MSIX, Windows startup registration, auto-update, notifications, themes, or telemetry.
- No valid branded icon source exists; the current crossed-square assets are WinUI placeholders. Branded icon work remains deferred.
- Separate unpackaged publish directories can run as separate App instances. Launching the same executable from the same publish directory is the supported validated single-instance scenario.
- Moving a published App directory requires updating Claude Code's configured bridge command. Automatic Claude configuration, backup, merge, and rollback are intentionally unsupported.
- Legacy compatibility cleanup remains deferred. Do not remove or reinterpret `UsageWindow`, `UsageWindowResetAt`, `SessionTokens`, or `WeeklyTokens`.

## Handoff

No release has been created and nothing has been pushed. The recommended next phase is documentation-independent release work only when explicitly requested: installer/release design or integration of a real branded icon source. Otherwise, no further engineering is required for the local MVP.
