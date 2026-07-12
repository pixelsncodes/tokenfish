# Codex Usage Semantics

Inspected local Codex executable: `codex-cli 0.144.1`.

Inspected upstream source areas from `openai/codex` `main` on 2026-07-12:

- `codex-rs/app-server-protocol/src/protocol/v2/account.rs`
- `codex-rs/tui/src/status/rate_limits.rs`
- `codex-rs/tui/src/chatwidget/rate_limits.rs`

TokenFish uses these app-server methods:

- `account/rateLimits/read`
- `account/usage/read`

## Rate-Limit Windows

`account/rateLimits/read` returns quota-window data. The backward-compatible
`rateLimits` object contains optional `limitId`, optional `limitName`, `primary`,
and `secondary` fields. Newer Codex versions may also return
`rateLimitsByLimitId`, keyed by metered limit identifier.

Each returned window contains:

- `usedPercent`: percentage of that specific quota window consumed.
- `windowDurationMins`: provider-supplied window duration when available.
- `resetsAt`: absolute Unix timestamp in seconds when available.

The local Codex version returned one available default primary window and no
available default secondary window in the sanitized probe. The local response
also included one `rateLimitsByLimitId` bucket. `limitId` was present;
`limitName` was not present. Upstream Codex display code maps only explicit
`windowDurationMins` values to labels with a narrow tolerance: 5h, daily,
weekly, monthly, or annual. Fallback labels such as usage or secondary usage
are positional display fallbacks, not provider-supplied semantics.

TokenFish's popup reset countdown is local formatting of `resetsAt` relative to
the current clock. A countdown such as hours and minutes remaining is not a
provider label and must not be used to infer a quota-window name.

## Token Activity

`account/usage/read` returns activity data, not quota-window data. The response
contains `dailyUsageBuckets`; each bucket has:

- `startDate`: daily bucket start date.
- `tokens`: token activity count for that bucket.

The local Codex version returned dated daily buckets with numeric token counts
and no session-token field in the sanitized probe. TokenFish currently sums the
UTC capture date plus the previous six UTC dates from those buckets. That is a
latest-seven-UTC-dates activity total. It is not a quota, allowance, calendar
week guarantee, or session metric.

Session token activity is absent from the current Codex app-server methods
TokenFish calls. TokenFish represents it as an absent metric, not as a failed
quota window.
