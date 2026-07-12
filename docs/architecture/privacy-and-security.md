# Privacy and Security Architecture

TokenFish is designed as a local-first desktop application. The app should work from local provider state and local user preferences without depending on a remote TokenFish backend.

## Data Boundaries

- TokenFish has no telemetry by default.
- TokenFish has no remote TokenFish backend.
- TokenFish must not store API keys, cookies, access tokens, prompts, responses, source code, terminal history, account email, usernames, or workspace paths.
- Only normalized usage metrics may be persisted.
- Test fixtures must be synthetic and sanitized.

## Provider Communication

Codex communication must go through the locally installed Codex App Server over redirected stdin/stdout. TokenFish must not create a public network listener for Codex communication.

Claude communication must go through an allowlisted local status-line data bridge. TokenFish must accept only the documented bridge payload shape and must treat malformed or unexpected data as untrusted input.

Provider integrations are intentionally outside the initial scaffold. Future provider code must preserve these data boundaries.

## Logging and Diagnostics

Logs must redact:

- Usernames.
- Home directories.
- Credentials.
- Command arguments.
- Raw provider payloads.

Diagnostic output must prefer coarse state, counts, and normalized usage metrics over raw provider data. Logs must not include prompts, responses, source code, terminal history, account identifiers, or workspace paths.

## Local Runtime

TokenFish must not expose a public network port. Any local IPC or process bridge must be private to the current user session and must validate inputs before parsing or dispatch.

Normal use must not require administrator privileges. Installation or update flows that need elevated privileges must be explicit and separate from normal runtime behavior.
