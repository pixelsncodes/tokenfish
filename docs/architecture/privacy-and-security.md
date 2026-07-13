# Privacy and Security Architecture

TokenFish is a local-first desktop application. It works from local provider state and local user preferences without a TokenFish backend.

## Data Boundaries

- TokenFish has no telemetry or analytics.
- TokenFish has no remote backend, network listener, local HTTP service, or cloud storage.
- TokenFish does not collect or store API keys, credentials, cookies, or access tokens.
- TokenFish does not capture prompts, responses, conversation history, source code, terminal history, workspace data, or browser data.
- Only normalized allowlisted usage metrics may be persisted.
- Test fixtures must be synthetic and sanitized.

## Provider Communication

Codex communication must go through the locally installed Codex App Server over redirected stdin/stdout. TokenFish must not create a public network listener for Codex communication.

Claude communication must go through an allowlisted local status-line data bridge. TokenFish must accept only the documented bridge payload shape and must treat malformed or unexpected data as untrusted input.

The Claude status-line bridge persists only normalized five-hour and seven-day quota observations under `%LOCALAPPDATA%\TokenFish\bridge\claude-status-v1.json`. TokenFish does not persist raw Claude status-line JSON, edit Claude Code settings, inspect Claude authentication, or read Claude transcripts, prompts, workspaces, cookies, credentials, terminal history, repository identity, model names, costs, or account identifiers.

## Logging and Diagnostics

Logs must redact:

- Usernames.
- Home directories.
- Credentials.
- Command arguments.
- Raw provider payloads.

Diagnostic output must prefer coarse state, counts, and normalized usage metrics over raw provider data. Logs must not include prompts, responses, source code, terminal history, account identifiers, or workspace paths.

## Local Runtime

TokenFish does not expose a public network port. Codex communication uses redirected standard input/output with the local Codex App Server; Claude receives status-line input through the local bridge executable.

Normal use requires no administrator privileges. TokenFish does not automatically modify Claude Code configuration.
