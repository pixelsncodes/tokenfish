using TokenFish.Core.Models;

namespace TokenFish.Infrastructure;

public sealed class OnboardingFlowController
{
    private readonly AppSettings _initialSettings;

    public OnboardingFlowController(AppSettings settings)
    {
        _initialSettings = settings ?? throw new ArgumentNullException(nameof(settings));
        Step = OnboardingStep.Welcome;
        Select(settings.ProviderSelectionMode);
        RuntimeMode = settings.CodexRuntimeMode;
        WslDistributionName = settings.CodexWslDistributionName ?? string.Empty;
    }

    public OnboardingStep Step { get; private set; }
    public bool IsClaudeSelected { get; private set; }
    public bool IsCodexSelected { get; private set; }
    public CodexRuntimeMode RuntimeMode { get; private set; }
    public string WslDistributionName { get; private set; }
    public bool IsWslDistributionEnabled => IsCodexSelected && RuntimeMode != CodexRuntimeMode.NativeWindows;
    public string? ValidationMessage { get; private set; }

    public void Select(ProviderSelectionMode mode)
    {
        IsClaudeSelected = mode is ProviderSelectionMode.ClaudeOnly or ProviderSelectionMode.Both;
        IsCodexSelected = mode is ProviderSelectionMode.CodexOnly or ProviderSelectionMode.Both;
        ValidationMessage = null;
    }

    public void SetProviderSelection(bool claude, bool codex)
    {
        IsClaudeSelected = claude;
        IsCodexSelected = codex;
        ValidationMessage = null;
    }

    public void SetRuntimeMode(CodexRuntimeMode runtimeMode) => RuntimeMode = runtimeMode;

    public void SetWslDistributionName(string? value) => WslDistributionName = value ?? string.Empty;

    public bool Continue()
    {
        if (Step == OnboardingStep.ProviderSelection && !IsClaudeSelected && !IsCodexSelected)
        {
            ValidationMessage = "Select at least one provider.";
            return false;
        }

        Step = GetNextStep(Step);
        ValidationMessage = null;
        return true;
    }

    public void Back() => Step = GetPreviousStep(Step);

    public AppSettings CreatePendingSettings() => _initialSettings with
    {
        IsOnboardingCompleted = false,
        ProviderSelectionMode = IsClaudeSelected && IsCodexSelected
            ? ProviderSelectionMode.Both
            : IsClaudeSelected ? ProviderSelectionMode.ClaudeOnly : ProviderSelectionMode.CodexOnly,
        CodexRuntimeMode = RuntimeMode,
        CodexWslDistributionName = IsWslDistributionEnabled &&
            !string.IsNullOrWhiteSpace(WslDistributionName)
            ? WslDistributionName.Trim()
            : null
    };

    private OnboardingStep GetNextStep(OnboardingStep step) => step switch
    {
        OnboardingStep.Welcome => OnboardingStep.ProviderSelection,
        OnboardingStep.ProviderSelection when IsCodexSelected => OnboardingStep.CodexRuntime,
        OnboardingStep.ProviderSelection when IsClaudeSelected => OnboardingStep.ClaudeBridge,
        OnboardingStep.CodexRuntime when IsClaudeSelected => OnboardingStep.ClaudeBridge,
        OnboardingStep.CodexRuntime => OnboardingStep.Verification,
        OnboardingStep.ClaudeBridge => OnboardingStep.Verification,
        OnboardingStep.Verification => OnboardingStep.Finish,
        _ => OnboardingStep.Finish
    };

    private OnboardingStep GetPreviousStep(OnboardingStep step) => step switch
    {
        OnboardingStep.ProviderSelection => OnboardingStep.Welcome,
        OnboardingStep.CodexRuntime => OnboardingStep.ProviderSelection,
        OnboardingStep.ClaudeBridge when IsCodexSelected => OnboardingStep.CodexRuntime,
        OnboardingStep.ClaudeBridge => OnboardingStep.ProviderSelection,
        OnboardingStep.Verification when IsClaudeSelected => OnboardingStep.ClaudeBridge,
        OnboardingStep.Verification => OnboardingStep.CodexRuntime,
        OnboardingStep.Finish => OnboardingStep.Verification,
        _ => OnboardingStep.Welcome
    };
}

public enum OnboardingStep
{
    Welcome,
    ProviderSelection,
    CodexRuntime,
    ClaudeBridge,
    Verification,
    Finish
}
