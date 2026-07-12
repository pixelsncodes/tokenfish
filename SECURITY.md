# Security Policy

## Supported Versions

TokenFish is pre-1.0 software. Until the first public release, security fixes are applied to the main branch only.

After versioned releases begin, the latest minor release line will receive security fixes. Older release lines may receive fixes when the project explicitly documents support for them.

## Reporting a Vulnerability

Report suspected vulnerabilities privately to the project maintainers. Do not open a public issue, discussion, pull request, or gist that contains credentials, logs, personal data, raw provider payloads, workspace paths, or other sensitive material.

When reporting privately, include:

- The affected TokenFish version or commit.
- A short description of the issue and expected impact.
- Reproduction steps using synthetic data.
- Redacted logs only when they are necessary to understand the issue.

The maintainers will acknowledge private reports, investigate the impact, and coordinate a fix before public disclosure when the issue is confirmed.

## Security Principles

- TokenFish is local-first and should not require a remote TokenFish backend for normal operation.
- TokenFish does not collect telemetry by default.
- TokenFish must not persist API keys, cookies, access tokens, prompts, responses, source code, terminal history, account email, usernames, or workspace paths.
- Provider integrations must accept only the minimum local data needed to compute normalized usage metrics.
- Logs and diagnostics must redact usernames, home directories, credentials, command arguments, and raw provider payloads.
- Test fixtures must be synthetic and sanitized.
- Normal use must not require administrator privileges.
