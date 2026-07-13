using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TokenFish.Core.Models;
using TokenFish.Infrastructure;

namespace TokenFish.App;

public sealed partial class OnboardingWindow : Window
{
    private readonly OnboardingFlowController _flow;
    private readonly IClipboardService _clipboard;
    private readonly string _claudeSnippet;
    private bool _terminalResult;

    public OnboardingWindow(
        OnboardingFlowController flow,
        string claudeSnippet,
        IClipboardService? clipboard = null)
    {
        _flow = flow ?? throw new ArgumentNullException(nameof(flow));
        _claudeSnippet = claudeSnippet ?? throw new ArgumentNullException(nameof(claudeSnippet));
        _clipboard = clipboard ?? new WindowsClipboardService();
        InitializeComponent();
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
        }
        AppWindow.Resize(new Windows.Graphics.SizeInt32(760, 620));
        Closed += (_, _) =>
        {
            if (!_terminalResult)
            {
                Deferred?.Invoke();
            }
        };
        Render();
    }

    public event Action? Deferred;
    public event Action<AppSettings>? VerificationRequested;
    public event Action<AppSettings>? Completed;

    private void OnContinueClicked(object sender, RoutedEventArgs args)
    {
        if (_flow.Step == OnboardingStep.Finish)
        {
            Completed?.Invoke(_flow.CreatePendingSettings() with { IsOnboardingCompleted = true });
            return;
        }

        _flow.SetProviderSelection(ClaudeCheckBox.IsChecked == true, CodexCheckBox.IsChecked == true);
        _flow.SetWslDistributionName(WslDistributionTextBox.Text);
        if (!_flow.Continue())
        {
            Render();
            return;
        }

        if (_flow.Step == OnboardingStep.Verification)
        {
            VerificationRequested?.Invoke(_flow.CreatePendingSettings());
        }
        Render();
    }

    private void OnBackClicked(object sender, RoutedEventArgs args)
    {
        _flow.Back();
        Render();
    }

    private void OnNotNowClicked(object sender, RoutedEventArgs args)
    {
        _terminalResult = true;
        Deferred?.Invoke();
        Close();
    }

    public void CloseAfterCompletion()
    {
        _terminalResult = true;
        Close();
    }

    private void OnRuntimeModeChanged(object sender, SelectionChangedEventArgs args)
    {
        if (RuntimeModeComboBox.SelectedItem is ComboBoxItem { Tag: string tag } &&
            Enum.TryParse<CodexRuntimeMode>(tag, out var mode))
        {
            _flow.SetRuntimeMode(mode);
            WslDistributionTextBox.IsEnabled = _flow.IsWslDistributionEnabled;
        }
    }

    private void OnCopyClicked(object sender, RoutedEventArgs args)
    {
        try
        {
            _clipboard.SetText(_claudeSnippet);
            CopyStatusTextBlock.Text = "Copied";
        }
        catch
        {
            CopyStatusTextBlock.Text = "Copy failed. Select and copy the snippet manually.";
        }
    }

    private void Render()
    {
        StepTextBlock.Text = $"Step {(int)_flow.Step + 1} of 6";
        TitleTextBlock.Text = _flow.Step switch
        {
            OnboardingStep.Welcome => "Welcome to TokenFish",
            OnboardingStep.ProviderSelection => "Choose providers",
            OnboardingStep.CodexRuntime => "Codex runtime",
            OnboardingStep.ClaudeBridge => "Claude bridge",
            OnboardingStep.Verification => "Verification",
            _ => "Finish setup"
        };
        DescriptionTextBlock.Text = _flow.Step switch
        {
            OnboardingStep.Welcome => "TokenFish displays local Claude Code and Codex usage through supported local integrations.",
            OnboardingStep.ProviderSelection => "Choose at least one provider.",
            OnboardingStep.CodexRuntime => "Choose the runtime TokenFish will use. Changes later require an application restart.",
            OnboardingStep.ClaudeBridge => "TokenFish does not edit Claude Code configuration automatically.",
            OnboardingStep.Verification => "Provider readiness will be checked after settings are applied.",
            _ => "You can complete setup even if a provider is still waiting or has a problem."
        };
        ProviderSelectionPanel.Visibility = _flow.Step == OnboardingStep.ProviderSelection ? Visibility.Visible : Visibility.Collapsed;
        RuntimePanel.Visibility = _flow.Step == OnboardingStep.CodexRuntime ? Visibility.Visible : Visibility.Collapsed;
        ClaudePanel.Visibility = _flow.Step == OnboardingStep.ClaudeBridge ? Visibility.Visible : Visibility.Collapsed;
        ClaudeSnippetTextBox.Text = _claudeSnippet;
        ClaudeCheckBox.IsChecked = _flow.IsClaudeSelected;
        CodexCheckBox.IsChecked = _flow.IsCodexSelected;
        StatusTextBlock.Text = _flow.ValidationMessage;
        BackButton.IsEnabled = _flow.Step != OnboardingStep.Welcome;
        ContinueButton.Content = _flow.Step == OnboardingStep.Finish ? "Finish setup" : "Continue";
    }
}
