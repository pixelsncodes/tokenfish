using TokenFish.Core.Models;
using TokenFish.Providers.Codex.RateLimits;
using TokenFish.Providers.Codex.Usage;

namespace TokenFish.Providers.Codex;

public sealed class CodexUsageSnapshotFactory
{
    public ProviderUsageSnapshot Create(
        CodexRateLimitsSnapshot rateLimits,
        CodexAccountUsageSnapshot accountUsage,
        DateTimeOffset capturedAt)
    {
        ArgumentNullException.ThrowIfNull(rateLimits);
        ArgumentNullException.ThrowIfNull(accountUsage);

        return new ProviderUsageSnapshot(
            ProviderKind.Codex,
            ProviderConnectionState.Connected,
            CreateUsageWindowMetric(rateLimits.Primary),
            rateLimits.Primary.ResetsAt,
            TokenCountMetric.Unavailable(DataAuthority.TokenFishDerived, DataFreshness.Unknown),
            CreateWeeklyTokenMetric(accountUsage, capturedAt),
            capturedAt);
    }

    private static PercentageUsageMetric CreateUsageWindowMetric(CodexRateLimitWindow primaryWindow) =>
        primaryWindow.UsedPercent.HasValue
            ? new PercentageUsageMetric(
                primaryWindow.UsedPercent.Value,
                DataAuthority.LocalProviderReported,
                DataFreshness.Live)
            : PercentageUsageMetric.Unavailable(
                DataAuthority.LocalProviderReported,
                DataFreshness.Live);

    private static TokenCountMetric CreateWeeklyTokenMetric(
        CodexAccountUsageSnapshot accountUsage,
        DateTimeOffset capturedAt)
    {
        if (!accountUsage.HasDailyUsageBuckets)
        {
            return TokenCountMetric.Unavailable(DataAuthority.TokenFishDerived, DataFreshness.Unknown);
        }

        var captureDate = DateOnly.FromDateTime(capturedAt.ToUniversalTime().Date);
        var oldestIncludedDate = captureDate.AddDays(-6);

        try
        {
            var total = 0L;
            foreach (var bucket in accountUsage.DailyUsageBuckets!)
            {
                if (bucket.StartDate > captureDate)
                {
                    throw new CodexUsageSnapshotNormalizationException(
                        "Codex usage data could not be normalized.");
                }

                if (bucket.StartDate < oldestIncludedDate)
                {
                    continue;
                }

                checked
                {
                    total += bucket.Tokens;
                }
            }

            return new TokenCountMetric(
                total,
                DataAuthority.TokenFishDerived,
                DataFreshness.Live);
        }
        catch (OverflowException exception)
        {
            throw new CodexUsageSnapshotNormalizationException(
                "Codex usage data could not be normalized.",
                exception);
        }
    }
}
