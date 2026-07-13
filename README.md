# TokenFish

TokenFish is a local Windows desktop MVP for viewing normalized usage from Codex, Claude, or both providers. It runs as a compact tray application with a shared refresh action, provider readiness in Settings, and a content-sized popup validated at 100% and 125% display scaling.

This repository documents a local MVP. It is not a public release, installer, or MSIX-distributed product.

## Providers And Settings

Settings lets you choose `Codex`, `Claude`, or `Codex and Claude`. Saved provider and Codex runtime changes apply after TokenFish is restarted; Settings shows the saved and running selections when a restart is pending.

Codex uses the local Codex App Server. Supported runtime modes are:

- WSL login shell
- WSL direct
- Native Windows

For WSL modes, the distribution name is optional; an empty value uses the default distribution. Settings reports whether each selected provider is ready, waiting for data, stale, or unavailable. Refresh keeps the latest successful usage visible and presents generic recovery feedback when a provider cannot refresh.

Claude usage is supplied through the local TokenFish Claude status-line bridge. Settings generates separate manual snippets for Claude Code running natively on Windows and inside WSL. TokenFish never edits Claude Code settings. See [the Claude bridge guide](docs/architecture/claude-status-line-bridge.md) for the authoritative setup and data-boundary details.

## Build And Test

```powershell
dotnet build TokenFish.sln -p:Platform=x64
dotnet test TokenFish.sln -p:Platform=x64
```

## Publish Locally

App publish:

```powershell
dotnet publish src/TokenFish.App/TokenFish.App.csproj \
  -c Release \
  -p:Platform=x64 \
  -p:PublishProfile=win-x64 \
  -p:PublishDir="<output directory>/"
```

The App publish is self-contained and includes `tools\claude\TokenFish.ClaudeBridge.exe`. It can emit accepted SDK trim-analysis warnings for `Microsoft.Windows.SDK.NET` and `WinRT.Runtime` (`IL2104`); they are SDK/runtime warnings, not TokenFish application warnings.

Independent bridge publish:

```powershell
dotnet publish src/TokenFish.ClaudeBridge/TokenFish.ClaudeBridge.csproj \
  -c Release \
  -p:Platform=x64 \
  -p:PublishProfile=win-x64 \
  -p:PublishDir="<output directory>/"
```

The independent bridge output is a self-contained single executable. Local publishing does not install or release TokenFish. No installer or MSIX is currently provided.

## Privacy And Current Status

TokenFish is local-first: it has no TokenFish backend, telemetry, analytics, network listener, credential storage, or automatic Claude configuration. It persists normalized allowlisted Claude quota observations rather than raw status-line JSON. See [privacy and security](docs/architecture/privacy-and-security.md) and [current local-MVP status](docs/current-status.md) for the validation record, known limitations, and deferred work.
