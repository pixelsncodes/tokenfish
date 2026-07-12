namespace TokenFish.Infrastructure;

public sealed record PopupActivityDisplayState(
    string Label,
    string? IntervalText,
    string ValueText,
    string AutomationName);
