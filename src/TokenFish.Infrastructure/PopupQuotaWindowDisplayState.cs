namespace TokenFish.Infrastructure;

public sealed record PopupQuotaWindowDisplayState(
    string Label,
    string PercentageText,
    decimal ProgressValue,
    string ProgressAutomationName,
    string? RelativeResetText,
    string? ExactResetText)
{
    public string RemainingText => string.Create(System.Globalization.CultureInfo.InvariantCulture,
        $"{Math.Clamp(100m - ProgressValue, 0m, 100m):0.#}% left");
}
