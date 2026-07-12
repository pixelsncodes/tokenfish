# TokenFish

TokenFish is a Windows desktop foundation for choosing between supported token providers and presenting the app in a minimal or arcade theme.

This initial scaffold includes the WinUI 3 app shell, shared core models, provider placeholder projects, infrastructure placeholder project, and core unit tests. Provider integrations, tray behavior, and persistence are intentionally not implemented yet.

## Build

```powershell
dotnet build TokenFish.sln -p:Platform=x64
dotnet test TokenFish.sln -p:Platform=x64
```
