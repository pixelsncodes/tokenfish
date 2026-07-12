namespace TokenFish.Core.Models;

public sealed record NormalizedActivityMetric
{
    public ProviderKind Provider { get; }

    public string MetricId { get; }

    public string? DisplayLabel { get; }

    public UsageMetricLabelOrigin LabelOrigin { get; }

    public long? Value { get; }

    public UsageActivityUnit Unit { get; }

    public DateTimeOffset? IntervalStart { get; }

    public DateTimeOffset? IntervalEnd { get; }

    public UsageMetricAvailability Availability { get; }

    public DateTimeOffset CapturedAt { get; }

    public DataAuthority Authority { get; }

    public DataFreshness Freshness { get; }

    public string Source { get; }

    public bool IsAvailable => Availability == UsageMetricAvailability.Available;

    public NormalizedActivityMetric(
        ProviderKind provider,
        string metricId,
        string? displayLabel,
        UsageMetricLabelOrigin labelOrigin,
        long? value,
        UsageActivityUnit unit,
        DateTimeOffset? intervalStart,
        DateTimeOffset? intervalEnd,
        UsageMetricAvailability availability,
        DateTimeOffset capturedAt,
        DataAuthority authority,
        DataFreshness freshness,
        string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metricId);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        if (availability == UsageMetricAvailability.Available && !value.HasValue)
        {
            throw new ArgumentException(
                "Available activity metrics must include a value.",
                nameof(value));
        }

        if (availability == UsageMetricAvailability.Unavailable && value.HasValue)
        {
            throw new ArgumentException(
                "Unavailable activity metrics cannot include a value.",
                nameof(value));
        }

        if (value is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Activity metric value must be zero or greater.");
        }

        if (intervalStart.HasValue != intervalEnd.HasValue)
        {
            throw new ArgumentException(
                "Activity metric intervals must include both start and end when either is known.",
                nameof(intervalEnd));
        }

        if (intervalStart.HasValue &&
            intervalEnd!.Value.ToUniversalTime() < intervalStart.Value.ToUniversalTime())
        {
            throw new ArgumentException(
                "Activity metric interval end cannot precede interval start.",
                nameof(intervalEnd));
        }

        ValidateLabel(displayLabel, labelOrigin);

        Provider = provider;
        MetricId = metricId;
        DisplayLabel = string.IsNullOrWhiteSpace(displayLabel) ? null : displayLabel;
        LabelOrigin = labelOrigin;
        Value = value;
        Unit = unit;
        IntervalStart = intervalStart?.ToUniversalTime();
        IntervalEnd = intervalEnd?.ToUniversalTime();
        Availability = availability;
        CapturedAt = capturedAt.ToUniversalTime();
        Authority = authority;
        Freshness = freshness;
        Source = source;
    }

    public static NormalizedActivityMetric Unavailable(
        ProviderKind provider,
        string metricId,
        UsageActivityUnit unit,
        DateTimeOffset capturedAt,
        DataAuthority authority,
        DataFreshness freshness,
        string source) =>
        new(
            provider,
            metricId,
            displayLabel: null,
            UsageMetricLabelOrigin.Unknown,
            value: null,
            unit,
            intervalStart: null,
            intervalEnd: null,
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
