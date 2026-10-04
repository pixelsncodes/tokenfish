# TokenFish release readiness and improvement plan

Reviewed on October 3, 2026, against commit 56db704. Keep Codex and Claude, including the existing Claude bridge. The objective is a polished Windows tray app that people can download from GitHub and set up without developer tools. This document records the current audit and proposed implementation order; it does not claim the improvements are implemented.

## Current implementation scope

The user requested phases 1 through 4 first, with a design mockup shown before implementation. Include an optional minimal desktop widget and fix Settings placement. Both providers remain supported. Release automation and installers are outside this implementation pass.

### Design direction for review

Use a compact floating panel with generous but restrained spacing, consistent typography, charcoal surfaces in dark mode, and a controlled neon-yellow fish accent. Support corresponding light/system appearance and accessibility. Keep provider names, quota duration, remaining percentage, reset countdown, and freshness visible. Show token activity separately in expandable details; no invented allowances or costs. Consolidate setup, Settings, popup, and widget visual resources inside the existing WinUI project.

The user selected the two-provider widget and explicitly requested retaining the little fishes. Preserve the existing TokenFish mascot on every quota rail in both the popup and widget, with provider-colored fish and pellets ahead of the mouth. The fish advances as quota is consumed; the text always states the remaining percentage. Retain the neon-yellow mascot in the full-view header. Respect reduced motion and Windows animation preferences in the implementation.

The desktop widget is optional and hidden by default. The minimal design shows a fish, one thin remaining-usage line, the selected provider/window, and an explicitly labeled percentage. A two-row design can show both providers when both are enabled. Do not merge their limits. The normal desktop window stays in its corner without forcing itself above other applications; offer always-on-top as an explicit option. Remember widget visibility, provider/window selection, and corner/monitor placement through the existing settings mechanism. Clamp or relocate it when displays change. Avoid desktop-shell injection or a new database.

Click the fish or usage area to open the existing full popup beside the widget. Drag a small handle to move the widget and snap it near a corner. A compact menu supplies Settings and Hide widget. Hidden widgets can always be restored from Settings or the tray menu. Hiding the widget does not quit the app, stop collection, or hide the full view. A full popup opened from the widget must not disappear just because focus moves to its own Settings window.

Settings must open beside the surface that invoked it. Prefer adjacent placement with a small gap, flip sides when space is constrained, and clamp to the same monitor's work area. Tray launches use the tray anchor. An explicit invocation anchor takes precedence over an old remembered window position. Check all four corners, negative monitor coordinates, DPI changes, small work areas, and reopening an existing Settings window from a different anchor. The current calculator uses its anchor only to select a monitor and centers the window, which explains the reported behavior.

### Implementation order after mockup review

1. Fix the unintended white native border and Settings anchoring in a focused window-placement change. Verify native operation results and ensure the fix survives showing, focus changes, resize, and reopening. Add meaningful placement and lifecycle tests, plus a published visual check.
2. Correct onboarding's saved values, provider-specific step count, Windows/WSL Claude guidance, and apply-and-recheck configuration. Preserve valid saved provider choices and the working Claude bridge.
3. Add startup/collection deadlines, session invalidation and recreation with bounded retry behavior, independent quota/activity availability, and allowlisted recovery categories. Verify a stalled or exited server, shutdown, and one-provider failures.
4. Introduce shared appearance resources and refine the quota popup and Settings. Add the optional widget as a separate surface sharing snapshots, refresh, and display calculations with the popup. Test hiding, restoring, dragging, persistence, display removal, always-on-top selection, and full-view coexistence.
5. Expose existing Claude weekly usage and named Codex windows. Extend optional Codex summary contracts for lifetime tokens, peak daily activity, streaks, and longest turn, plus dated daily activity. Verify missing/null fields, UTC boundaries, unknown coverage, and older compatible responses.
6. Run the relevant build/test suite and local self-contained publish, then manually check both providers, all connection states, popup/widget/Settings interaction, keyboard access, high contrast, reduced motion, and 100% through 200% scaling. Report any manual cases that remain unverified.

The mockup uses illustrative values and previews layout/interaction only. The user approved the fish-based design and authorized completion while away. Production implementation now includes shared fish rails, both provider cards, an optional persisted corner widget, adjacent Settings placement, appearance controls, setup route fixes, bounded Codex requests and session recovery, and optional activity summaries. Release documentation, SDK selection, a local portable package script, and a Windows CI workflow have also been added. See [the candidate validation record](../releases/0.2.0-beta.1.md) for current evidence and outstanding release checks; the baseline and findings below describe the original audited build.

## Verified build baseline

The working tree was clean before the audit. The current x64 Debug solution build succeeded with zero warnings and errors. All 783 tests passed with zero failures or skips: Core 100, Infrastructure 375, Codex 238, and Claude 70. The older current-status document records 706 tests and should be updated during release documentation work.

The self-contained x64 Release publish succeeded. It emitted the two previously documented IL2104 assembly trimming warnings for Microsoft.Windows.SDK.NET and WinRT.Runtime. The resulting directory contains 344 files totaling 212,640,370 bytes, approximately 203 MiB before compression, including the Claude bridge. No PDB files were found in that directory. This was a local publish, not a GitHub release.

Validation commands:

    dotnet build TokenFish.sln -p:Platform=x64
    dotnet test TokenFish.sln -p:Platform=x64 --no-build --no-restore
    dotnet publish src/TokenFish.App/TokenFish.App.csproj -c Release -p:Platform=x64 -p:PublishProfile=win-x64 -p:PublishDir=<absolute-output-directory>/

The restricted shell initially prevented .NET first-run writes and NuGet access. A task-local CLI home and an approved dependency restore allowed validation to complete. These environment failures are not application build defects.

The published executable launched and remained running, but desktop inspection did not expose a targetable TokenFish window. A relaunch also failed to provide a targetable window. This does not establish that the app crashed or that its tray UI is broken. Live checks of the popup, the reported white border, provider connectivity, second-launch activation, and exit remain unverified in this audit. The earlier repository validation record is historical evidence, not a substitute for a new manual pass.

## Findings and priorities

### First priority: the reported white border

The user reports a white border around the app. [PopupWindowPlacement](../../src/TokenFish.App/Platform/PopupWindowPlacement.cs) already disables the presenter border and title bar, removes native frame flags after showing, and requests no DWM border. It discards the DwmSetWindowAttribute return value and the SetWindowPos result. A failed native operation or a later frame change is a plausible explanation, not a confirmed cause. The XAML root also has a dark background, so an outer white edge needs investigation at the native frame and client-area boundary.

Reproduce the exact edge before changing rendering. Check first show, repeat show, focus loss, reopen, resizing caused by new data, and monitor changes. Distinguish a DWM outline from exposed client background and intended focus indicators. Check native return values and reapply frame styling at the correct lifecycle event if reproduction supports that fix. Preserve keyboard focus visibility and Windows high contrast support.

Acceptance: no unintended white outer border in the published popup at 100%, 125%, 150%, and 200% scaling, in active and inactive states. Add focused tests for any new frame-state behavior; retain screenshot-based manual verification because style-mask unit tests cannot prove the rendered result.

### High priority: Codex recovery and partial availability

- [CodexSessionUsageCollector](../../src/TokenFish.Providers.Codex/CodexSessionUsageCollector.cs) caches its session. Collection failures do not invalidate or replace it. A dead child process therefore has no automatic recreation path after initialization succeeds. Dispose and invalidate unusable sessions, then recreate them on a later refresh with bounded backoff.
- Startup and collection use caller cancellation without an explicit request deadline. A server that stays alive but stops replying can leave refresh waiting indefinitely. Add separate startup and collection deadlines; keep scheduled polling alive after recoverable failures.
- [CodexProviderUsageCollector](../../src/TokenFish.Providers.Codex/CodexProviderUsageCollector.cs) treats limits and token activity as one sequential operation. Failure of the optional activity request discards otherwise usable limits for that attempt. Preserve quota data when activity is unsupported or temporarily unavailable, with freshness tracked for each result.
- The protocol converts every JSON-RPC error into generic feedback. Preserve allowlisted error categories so the interface can distinguish missing runtime, sign-in required, unsupported capability, timeout, and temporary failure without exposing raw provider responses.

Acceptance: a fake stalled server times out; an exited server is recreated; quota data remains visible when activity fails; repeated retries are bounded; shutdown cleans up the app-owned child process. Test that one provider's failure does not prevent the other's usable data from being presented.

### High priority: setup consistency

- [AppSettings](../../src/TokenFish.Core/Models/AppSettings.cs) defaults to WSL login shell. Detect installed native and WSL runtimes, recommend a usable choice, and allow explicit override. Existing saved choices must continue to work.
- [OnboardingWindow](../../src/TokenFish.App/OnboardingWindow.xaml.cs) copies provider choices into controls, but does not initialize the runtime combo selection or saved distribution text from the flow. Display the actual settings before the user continues.
- Onboarding receives only the native Windows Claude snippet, while Settings already offers Windows and WSL guidance. Reuse both supported variants in onboarding without modifying Claude Code settings automatically.
- Progress uses fixed enum positions and a total of six even when provider-specific steps are skipped. Calculate progress from the selected route.
- Returning from verification to change providers or runtime saves new settings, but [TokenFishApplicationRuntimeHost](../../src/TokenFish.Infrastructure/TokenFishApplicationRuntimeHost.cs) starts only once. Reverification can therefore inspect the earlier running configuration. Define and test a controlled apply-and-recheck operation for setup.
- Completing setup while a provider is waiting remains useful, but the result should clearly explain what works and what action is still needed.

Acceptance: fresh setup, saved settings, Codex-only, Claude-only, and combined routes show correct values and progress. Back/edit/recheck validates the edited configuration. Both Claude command variants match the established setup guide.

### Medium priority: presentation loses useful data

- [TrayPopupDisplayStateAdapter](../../src/TokenFish.Infrastructure/TrayPopupDisplayStateAdapter.cs) filters Claude to its five-hour window even though the bridge snapshot already supports seven-day usage. Show both when reported; a missing weekly value remains unavailable.
- Codex bucket names are parsed but discarded when building quota display labels. Multiple buckets with the same duration can appear indistinguishable. Preserve the provider's display name and group windows by bucket, with a stable fallback identifier.
- Codex activity is labeled only as tokens and dates in the popup. Explicitly identify it as activity across the latest seven UTC dates, separate from weekly quota consumption. Do not imply a local calendar week or token allowance.
- The account usage parser requires a summary object but ignores its values. Extend the contract for supported optional summary fields without changing the meanings of existing compatibility fields.

## Proposed user experience

Keep the compact tray-first model and the existing WinUI structure. Use a single visual system across popup, setup, and Settings: consistent surfaces, spacing, typography, rounded corners, and one controlled neon-yellow brand accent. The current popup is forced dark, Settings uses system styling with Mica, and setup uses another hardcoded palette. Support System, Light, and Dark appearance deliberately rather than exposing stored theme options that have no visible implementation. Retain the fish motif, keep animation restrained, and respect reduced motion. Final icon artwork is a separate implementation task; the existing brand note explicitly defers final artwork.

The popup should answer three questions immediately: how much is left, when it resets, and how fresh the reading is. Each provider gets its own compact card with a name and connection state, named quota windows, a prominent remaining percentage, a small used-percentage label, reset countdown, and exact reset time on focus or hover. Keep percentages explicitly labeled so changing emphasis does not reverse the meaning of the bar. Never combine provider percentages or different quota buckets into one total.

Keep refresh and Settings accessible in the header. Put token activity and historical statistics in an expandable details area so they do not crowd the quota view. Provide clear waiting, stale, offline, and sign-in states with a relevant action. Old readings should show their observation time; a refresh attempt alone must not make old bridge data appear new.

Organize Settings into Connections, Appearance, General, and About. Keep advanced WSL options under Connections. Show whether a change applies now or requires reconnection. Include a deliberate launch-at-login option, refresh cadence, optional threshold/reset alerts, and version information. Do not add telemetry or automatic Claude configuration as part of UI work.

Use shared visual resources and small reusable controls inside the existing project. Avoid an architectural rewrite. Evaluate larger font settings, keyboard navigation, screen readers, high contrast, small work areas, monitor transitions, and popup scrolling when both providers expose several windows.

## Additional statistics

OpenAI's current [App Server documentation](https://learn.chatgpt.com/docs/app-server) describes quota buckets, optional account details, token-activity summaries, daily buckets, credits, and earned resets. These are capability-dependent: a documented field is not proof that every installed CLI or account returns it. This audit did not query the user's live provider data.

| Candidate | Data source | Recommendation |
| --- | --- | --- |
| Remaining percentage and reset countdown | Reported usage and reset timestamp | First release; derive remaining as clamped 100 minus used |
| Claude seven-day quota | Existing normalized bridge observation | First release, only when supplied |
| Named Codex quota groups | Bucket limitName or fallback limitId | First release |
| Today UTC and latest seven UTC dates token activity | Dated dailyUsageBuckets | First release details; show coverage and unavailable states |
| Seven-day activity chart | Dated dailyUsageBuckets | First release details; absent dates must not silently become observed zero usage |
| Lifetime tokens and peak daily tokens | Optional summary.lifetimeTokens and peakDailyTokens | First release details when returned |
| Current and longest activity streak | Optional summary.currentStreakDays and longestStreakDays | Secondary details when returned |
| Longest-running turn | Optional summary.longestRunningTurnSec | Secondary details when returned |
| Account plan and authentication state | account/read or reported planType | Connection context; minimize identity display |
| Workspace credit status and available earned resets | Optional credits and rateLimitResetCredits | Later read-only details after confirming supported response shapes |
| Consumption rate and approximate time to exhaustion | Locally retained quota observations | Later; label estimates, handle resets and gaps, and require sufficient samples |

Do not present token counts as remaining quota, money spent, saved API costs, or guaranteed remaining messages. This integration does not establish a complete meter for every ChatGPT website feature or model. Active-thread token notifications also do not prove coverage of unrelated sessions. Cross-session or model breakdowns require separate evidence and a scoped design.

Prefer provider-returned daily history for the initial release so no new database is needed. If local trend history is later added, scope it separately: persist only normalized values and timestamps, document retention, offer deletion/export, and isolate account changes. Earned-reset redemption is an account-changing feature and is outside the initial statistics work.

## GitHub distribution gaps

There is no tracked GitHub workflow, license file, SDK pin, or installer. The manifest still contains a template publisher identity and fixed version. Only x64 unpackaged publishing has been validated; the existence of other publish profiles does not establish ARM64 or x86 support. Separate unpackaged publish directories can run separate instances according to the existing validation record.

The README's multiline publish commands use backslashes, which are not PowerShell line continuations. Replace them with copyable one-line commands or correct PowerShell formatting. Rewrite the README for download-and-run users, including prerequisites, supported versions, screenshots, both provider setups, privacy, and troubleshooting.

Recommended rollout:

1. Publish an x64 self-contained portable ZIP as a tagged beta, after reliability and presentation fixes. Users extract the complete folder and launch the app; do not advertise the current multi-file publish as a single executable.
2. Add a per-user installer for the stable release, with a stable bridge path, shortcuts, uninstall behavior, and upgrade handling. Moving a portable folder currently breaks configured Claude commands; document repair and keep installer upgrades from moving the bridge unexpectedly.
3. Pin the .NET SDK and dependencies for reproducible builds. Add Windows CI to build, test, publish, scan artifacts, archive them, and attach checksums to tagged releases. Exclude credentials, settings, local history, diagnostics, and developer paths from release assets.
4. Choose a project license, version policy, real publisher metadata, and branded application/tray assets before the stable release. Document unsigned-download behavior accurately; treat signing as a distribution decision with actual certificate availability, not a promise.
5. Test extraction and launch on a clean non-developer Windows account with no .NET installation. Codex still needs a supported installed and signed-in runtime; Claude still needs the manual bridge configuration. Define supported Windows and CLI versions from actual compatibility results.

Initially use release notes and a manual download/update route. A background updater, ARM64 support, and broader history analytics can follow after the first stable release.

## Implementation sequence and release gates

| Phase | Focus | Completion evidence |
| --- | --- | --- |
| 1 | White border and setup correctness | Published visual pass, correct control values and route progress, back/edit/recheck tests |
| 2 | Bounded requests, session recovery, independent data results | Focused fake-process and collector tests; one-provider-failure smoke |
| 3 | Shared UI resources and clearer quota cards | Both providers, all connection states, keyboard/high contrast/reduced motion, DPI and monitor checks |
| 4 | Weekly Claude display and supported Codex stats | Parser fixtures for missing/null/new fields, named buckets, UTC boundaries, activity coverage |
| 5 | Portable beta and release automation | Clean Windows extraction/run, artifact scan, checksums, versioned tag assets, updated user documentation |
| 6 | Stable installer and final polish | Install/upgrade/uninstall, stable Claude command path, second launch and process cleanup, signed or accurately documented unsigned distribution |

Every behavioral phase gets focused tests, the relevant build/test run, and published smoke checks. Keep provider, tray, and any future persistence changes in separate focused commits. Do not remove or reinterpret UsageWindow, UsageWindowResetAt, SessionTokens, or WeeklyTokens during the first-release work.

A passing unit suite is a baseline, not release acceptance. Do not mark the white border, installer behavior, real provider refresh, or clean-machine setup complete without the corresponding manual evidence.
