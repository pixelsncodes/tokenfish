namespace TokenFish.Core.Models;

public sealed record NormalizedQuotaWindow
{
    public ProviderKind Provider { get; }

    public string WindowId { get; }

    public string? DisplayLabel { get; }

    public UsageMetricLabelOrigin LabelOrigin { get; }

    public decimal? UsedPercentage { get; }

    public DateTimeOffset? ResetAt { get; }

    public TimeSpan? WindowDuration { get; }

    public UsageMetricAvailability Availability { get; }

    public DateTimeOffset CapturedAt { get; }

    public DataAuthority Authority { get; }

    public DataFreshness Freshness { get; }

    public string Source { get; }

    public bool IsAvailable => Availability == UsageMetricAvailability.Available;

    public NormalizedQuotaWindow(
        ProviderKind provider,
        string windowId,
        string? displayLabel,
        UsageMetricLabelOrigin labelOrigin,
        decimal? usedPercentage,
        DateTimeOffset? resetAt,
        TimeSpan? windowDuration,
        UsageMetricAvailability availability,
        DateTimeOffset capturedAt,
        DataAuthority authority,
        DataFreshness freshness,
        string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(windowId);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        if (availability == UsageMetricAvailability.Available && !usedPercentage.HasValue)
        {
            throw new ArgumentException(
                "Available quota windows must include a used percentage.",
                nameof(usedPercentage));
        }

        if (availability == UsageMetricAvailability.Unavailable && usedPercentage.HasValue)
        {
            throw new ArgumentException(
                "Unavailable quota windows cannot include a used percentage.",
                nameof(usedPercentage));
        }

        if (usedPercentage is < 0m or > 100m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(usedPercentage),
                "Used percentage must be between 0 and 100 inclusive.");
        }

        if (windowDuration is { } duration && duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(windowDuration),
                "Window duration must be greater than zero.");
        }

        ValidateLabel(displayLabel, labelOrigin);

        Provider = provider;
        WindowId = windowId;
        DisplayLabel = string.IsNullOrWhiteSpace(displayLabel) ? null : displayLabel;
        LabelOrigin = labelOrigin;
        UsedPercentage = usedPercentage;
        ResetAt = resetAt?.ToUniversalTime();
        WindowDuration = windowDuration;
        Availability = availability;
        CapturedAt = capturedAt.ToUniversalTime();
        Authority = authority;
        Freshness = freshness;
        Source = source;
    }

    public static NormalizedQuotaWindow Unavailable(
        ProviderKind provider,
        string windowId,
        DateTimeOffset capturedAt,
        DataAuthority authority,
        DataFreshness freshness,
        string source) =>
        new(
            provider,
            windowId,
            displayLabel: null,
            UsageMetricLabelOrigin.Unknown,
            usedPercentage: null,
            resetAt: null,
            windowDuration: null,
            UsageMetricAvailability.Unavailable,
            capturedAt,
            authority,
            freshness,
            source);

    private static void ValidateLabel(string? displayLabel, UsageMetricLabelOrigin labelOrigin)
    {
        if (labelOrigin == UsageMetricLabelOrigin.Unknown)
        {
            if (!string.IsNullOrWhiteSpace(displayLabel))
            {
                throw new ArgumentException(
                    "Unknown label origins cannot include a display label.",
                    nameof(displayLabel));
            }

            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(displayLabel);
    }
}
