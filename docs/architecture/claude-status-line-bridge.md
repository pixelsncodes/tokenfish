# Claude Status-Line Bridge

TokenFish reads Claude usage through a local status-line bridge so the desktop app can consume only normalized usage metrics. TokenFish does not edit Claude Code settings; users manually configure the bridge executable as a Claude Code status-line command.

The App publish places the bridge at this stable app-relative location:

`tools\claude\TokenFish.ClaudeBridge.exe`

Settings resolves the absolute executable path from the running App base directory plus that relative path. Moving the published TokenFish folder after Claude Code is configured requires updating the configured command.

Claude Code's documented status-line contract uses a `statusLine` settings object with `type` set to `command` and a shell command string in `command`. Claude Code pipes one JSON payload to the command on standard input and displays the command's standard output. On Windows, Claude Code runs status-line commands through Git Bash when available and otherwise through PowerShell, so TokenFish presents a PowerShell command with a forward-slash Windows executable path:

```json
{
  "statusLine": {
    "type": "command",
    "command": "powershell -NoProfile -Command \"& 'C:/Path/To/TokenFish/tools/claude/TokenFish.ClaudeBridge.exe'\""
  }
}
```

Use that Windows snippet only when Claude Code runs natively on Windows. Settings generates the actual `statusLine` object using the resolved published App path; users add or merge only that object into their Claude Code user settings, preserving unrelated settings. After Claude Code emits a normal status-line update, TokenFish reads the normalized local bridge state when it refreshes.

When Claude Code runs inside WSL, its settings file is normally `~/.claude/settings.json`. The bridge remains a Windows executable, so Settings generates a WSL-specific command. It converts an absolute drive path by lowercasing the drive letter and replacing backslashes with forward slashes. For example, `C:\Published\TokenFish\tools\claude\TokenFish.ClaudeBridge.exe` becomes `/mnt/c/Published/TokenFish/tools/claude/TokenFish.ClaudeBridge.exe`. A WSL UNC path is distribution-local instead: `\\wsl.localhost\Distro\tmp\TokenFish\tools\claude\TokenFish.ClaudeBridge.exe` becomes `/tmp/TokenFish/tools/claude/TokenFish.ClaudeBridge.exe`. The WSL command directly invokes that quoted path and does not use PowerShell:

```json
{
  "statusLine": {
    "type": "command",
    "command": "\"/mnt/c/Published/TokenFish/tools/claude/TokenFish.ClaudeBridge.exe\""
  }
}
```

TokenFish never edits, merges, backs up, or inspects Claude Code settings. Users configure either snippet manually, and moving the published TokenFish folder requires updating the configured command. TokenFish does not inspect Claude credentials, prompts, responses, workspaces, source code, or browser data to generate either command.

Claude Code sends one JSON payload to the bridge on standard input. The bridge allowlist is limited to:

- `rate_limits.five_hour.used_percentage`
- `rate_limits.five_hour.resets_at`
- `rate_limits.seven_day.used_percentage`
- `rate_limits.seven_day.resets_at`

The bridge ignores all other status-line fields and does not retain raw input. It does not read transcripts, prompts, workspaces, cookies, credentials, terminal history, repository data, account identifiers, model names, costs, or Claude context-window token totals. Context-window tokens are not treated as TokenFish session tokens.

Persisted state lives under the current user's local application data directory:

`%LOCALAPPDATA%\TokenFish\bridge\claude-status-v1.json`

The file may contain only schema version plus optional five-hour and seven-day observations. Each observation contains used percentage, optional UTC reset time, and UTC observation time. No paths, command lines, raw JSON, error text, identity fields, or history are stored.

Bridge writes use a bounded current-session mutex, same-directory temporary files, and atomic replacement or move. New invocations merge five-hour and seven-day observations independently while holding the lock, and older observations cannot replace newer per-window state. Temporary files are cleaned up after write attempts.
