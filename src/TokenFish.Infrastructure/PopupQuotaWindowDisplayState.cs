namespace TokenFish.Infrastructure;

public sealed record PopupQuotaWindowDisplayState(
    string Label,
    string PercentageText,
    decimal ProgressValue,
    string ProgressAutomationName,
    string? RelativeResetText,
    string? ExactResetText);
