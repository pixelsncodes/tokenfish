# Threat Model

This model covers the initial public scaffold and the expected boundaries for future local provider integrations.

## Risks and Mitigations

### Credential Leakage

Risk: Credentials, cookies, access tokens, prompts, responses, source code, terminal history, account email, usernames, or workspace paths could be written to settings, logs, test fixtures, crash dumps, or provider snapshots.

Mitigations:

- Keep credential fields out of `AppSettings` and other persisted configuration.
- Persist only normalized usage metrics.
- Redact usernames, home directories, credentials, command arguments, and raw provider payloads from logs.
- Use synthetic, sanitized test fixtures only.
- Ignore local secrets, session artifacts, databases, logs, dumps, and provider snapshots in source control.

### Malicious or Malformed Provider Output

Risk: Local provider bridges may return malformed, oversized, or malicious payloads that trigger parser failures or unsafe UI state.

Mitigations:

- Treat all provider output as untrusted.
- Parse provider data with structured parsers and strict schemas.
- Reject unknown fields when a bridge contract requires allowlisting.
- Bound payload size and update frequency.
- Fall back to degraded provider state instead of crashing.

### Command Injection Through WSL Process Launching

Risk: Launching WSL or other local processes with unsanitized command strings could allow command injection.

Mitigations:

- Use argument arrays or platform process APIs instead of shell-concatenated commands.
- Keep command paths allowlisted.
- Do not pass raw provider payloads, workspace paths, prompts, or user-entered command fragments as shell text.
- Log process failures without command arguments.

### Unsafe File Permissions

Risk: Local settings, metrics, logs, or database files may be readable or writable by other users.

Mitigations:

- Store local data under the current user's application data directory.
- Create files with current-user-only permissions when supported.
- Avoid administrator-owned runtime files.
- Validate file permissions before reading sensitive local state.

### Sensitive Logs

Risk: Logs and diagnostics may contain identifiers, raw payloads, paths, or secrets.

Mitigations:

- Centralize log redaction before writing log entries.
- Redact usernames, home directories, credentials, command arguments, and raw provider payloads.
- Prefer enum states and normalized counters over raw strings.
- Keep crash dumps and diagnostic files out of source control.

### Dependency Compromise

Risk: A compromised package or transitive dependency could execute unsafe code or exfiltrate data.

Mitigations:

- Keep dependencies minimal.
- Review new dependencies before adding them.
- Prefer official SDKs or platform APIs when practical.
- Pin package versions in project files.
- Run dependency and build checks in CI before release.

### Database Tampering

Risk: A local metrics database may be modified by another process, causing incorrect usage display or parser failures.

Mitigations:

- Treat local database contents as untrusted input.
- Validate schema version and data ranges on read.
- Use transactional writes.
- Rebuild derived state when validation fails.
- Do not store credentials or raw provider data in the database.

### Localhost Exposure

Risk: A local listener may become reachable by browsers, other local users, containers, WSL, or the network.

Mitigations:

- Do not expose a public network port.
- Prefer redirected stdin/stdout or current-user local IPC over TCP listeners.
- Bind any future local-only listener to loopback and require explicit authorization.
- Reject cross-origin browser access unless a future design explicitly requires and secures it.

### Path Traversal

Risk: Provider data, settings, or diagnostics may include paths that escape intended storage directories.

Mitigations:

- Resolve and normalize paths before file access.
- Require paths to remain under allowlisted application directories.
- Reject relative traversal segments.
- Avoid persisting workspace paths.

### Denial of Service From Excessive Updates

Risk: Provider status updates may arrive too frequently or contain too much data, causing high CPU, memory growth, UI stalls, or excessive disk writes.

Mitigations:

- Debounce provider updates.
- Bound queue sizes and payload sizes.
- Drop stale intermediate updates when newer state is available.
- Persist metrics on a controlled cadence.
- Report degraded provider state when update processing exceeds limits.
