using Microsoft.UI.Windowing;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TokenFish.Core.Models;
using TokenFish.App.Platform;
using TokenFish.Infrastructure;
using Windows.Graphics;

namespace TokenFish.App;

public sealed partial class SettingsWindow : Window
{
    private readonly CodexRuntimeSettingsPresenter _presenter;
    private readonly Func<RectInt32?> _getPreferredPlacementAnchor;
    private bool _isLoaded;
    private bool _isPositioned;
    private bool _updatingControls;

    public SettingsWindow(
        CodexRuntimeSettingsPresenter presenter,
        Func<RectInt32?>? getPreferredPlacementAnchor = null)
    {
        ArgumentNullException.ThrowIfNull(presenter);

        _presenter = presenter;
        _getPreferredPlacementAnchor = getPreferredPlacementAnchor ?? (() => null);
        InitializeComponent();
        Title = "TokenFish Settings";
        InitializeWindowSize();
        InitializeRuntimeModes();
        RootGrid.Loaded += OnRootGridLoaded;
    }

    private async void OnRootGridLoaded(object sender, RoutedEventArgs args)
    {
        RootGrid.Loaded -= OnRootGridLoaded;
        _isLoaded = true;
        ApplyState(_presenter.State, ensurePositioned: false);
        ApplyState(
            await _presenter.LoadAsync(CancellationToken.None),
            ensurePositioned: true);
    }

    private void InitializeRuntimeModes()
    {
        RuntimeModeComboBox.ItemsSource = _presenter.State.RuntimeModeOptions;
        RuntimeModeComboBox.DisplayMemberPath = nameof(CodexRuntimeModeOption.Label);
    }

    private void InitializeWindowSize()
    {
        var scale = SettingsWindowSizing.GetRasterizationScale(this);
        AppWindow.Resize(SettingsWindowSizing.ToPhysicalSize(
            widthEffectivePixels: 520,
            heightEffectivePixels: 390,
            scale));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = true;
            presenter.IsResizable = false;
        }
    }

    private void OnRuntimeModeSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        _ = args;
        if (_updatingControls ||
            RuntimeModeComboBox.SelectedItem is not CodexRuntimeModeOption option)
        {
            return;
        }

        ApplyState(_presenter.SelectRuntimeMode(option.RuntimeMode));
    }

    private void OnProviderSelectionChecked(object sender, RoutedEventArgs args)
    {
        _ = args;
        if (_updatingControls)
        {
            return;
        }

        var providerSelectionMode = sender switch
        {
            RadioButton radioButton when radioButton == CodexProviderRadioButton =>
                ProviderSelectionMode.CodexOnly,
            RadioButton radioButton when radioButton == ClaudeProviderRadioButton =>
                ProviderSelectionMode.ClaudeOnly,
            RadioButton radioButton when radioButton == BothProvidersRadioButton =>
                ProviderSelectionMode.Both,
            _ => _presenter.State.ProviderSelectionMode
        };

        ApplyState(_presenter.SelectProviderSelectionMode(providerSelectionMode));
    }

    private void OnWslDistributionTextChanged(object sender, TextChangedEventArgs args)
    {
        _ = args;
        if (_updatingControls)
        {
            return;
        }

        ApplyState(_presenter.SetWslDistributionName(WslDistributionTextBox.Text));
    }

    private async void OnSaveClicked(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        ApplyState(_presenter.State with { IsSaving = true, CanSave = false });
        ApplyState(await _presenter.SaveAsync(CancellationToken.None));
        FocusRequestedControl(_presenter.State.FocusTarget);
    }

    private void OnCloseClicked(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        Close();
    }

    public void CaptureCurrentPlacement() =>
        SettingsWindowPlacementService.CaptureCurrentPosition(this);

    private void ApplyState(
        CodexRuntimeSettingsViewState state,
        bool ensurePositioned = true)
    {
        _updatingControls = true;
        try
        {
            CodexProviderRadioButton.IsChecked =
                state.ProviderSelectionMode == ProviderSelectionMode.CodexOnly;
            ClaudeProviderRadioButton.IsChecked =
                state.ProviderSelectionMode == ProviderSelectionMode.ClaudeOnly;
            BothProvidersRadioButton.IsChecked =
                state.ProviderSelectionMode == ProviderSelectionMode.Both;

            RuntimeModeComboBox.SelectedItem = state.RuntimeModeOptions
                .FirstOrDefault(option => option.RuntimeMode == state.RuntimeMode);

            if (WslDistributionTextBox.Text != state.WslDistributionName)
            {
                WslDistributionTextBox.Text = state.WslDistributionName;
            }

            RuntimeModeComboBox.IsEnabled = state.IsRuntimeModeEnabled;
            WslDistributionTextBox.IsEnabled = state.IsWslDistributionEnabled;
            WslDistributionHelpTextBlock.Visibility =
                state.IsRuntimeModeEnabled ? Visibility.Visible : Visibility.Collapsed;
            CodexSettingsRetainedTextBlock.Text = state.CodexSettingsRetainedMessage;
            CodexSettingsRetainedTextBlock.Visibility =
                state.IsCodexSettingsRetainedMessageVisible
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            ClaudeBridgeDescriptionTextBlock.Text = state.ClaudeBridgeDescription;
            ClaudeBridgeDescriptionTextBlock.Visibility =
                state.IsClaudeBridgeDescriptionVisible
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            SaveButton.IsEnabled = state.CanSave;
            SaveButton.Content = state.IsSaving ? "Saving..." : "Save";
            StatusTextBlock.Text = state.StatusMessage;
            StatusBorder.Visibility = state.IsStatusVisible ? Visibility.Visible : Visibility.Collapsed;
            StatusBorder.BorderBrush = GetStatusBorderBrush(state.StatusKind);
            RenderReadinessRows(state.ReadinessRows);
        }
        finally
        {
            _updatingControls = false;
        }

        ResizeToContent(ensurePositioned);
    }

    private void RenderReadinessRows(
        IReadOnlyList<ProviderReadinessDisplayState> readinessRows)
    {
        ProviderReadinessRowsStackPanel.Children.Clear();
        ProviderReadinessSection.Visibility =
            readinessRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        var secondaryBrush = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        foreach (var row in readinessRows)
        {
            var rowPanel = new StackPanel
            {
                Spacing = 2
            };
            rowPanel.Children.Add(new TextBlock
            {
                FontWeight = FontWeights.SemiBold,
                Text = $"{row.ProviderName}: {row.Label}",
                TextWrapping = TextWrapping.Wrap
            });
            rowPanel.Children.Add(new TextBlock
            {
                Foreground = secondaryBrush,
                Text = row.Description,
                TextWrapping = TextWrapping.Wrap
            });
            ProviderReadinessRowsStackPanel.Children.Add(rowPanel);
        }
    }

    private void ResizeToContent(bool ensurePositioned)
    {
        if (!_isLoaded || RootGrid.XamlRoot is null)
        {
            return;
        }

        RootGrid.UpdateLayout();
        RootGrid.Measure(new Windows.Foundation.Size(
            SettingsWindowLayoutCalculator.WidthEffectivePixels,
            double.PositiveInfinity));

        var measuredHeight = Math.Ceiling(RootGrid.DesiredSize.Height);
        AppWindow.Resize(SettingsWindowSizing.ToOuterPhysicalSize(this, measuredHeight));

        if (!ensurePositioned)
        {
            return;
        }

        if (!_isPositioned || !SettingsWindowPlacementService.IsCurrentPositionVisible(this))
        {
            SettingsWindowPlacementService.EnsureVisibleOnMonitor(
                this,
                _getPreferredPlacementAnchor());
            _isPositioned = true;
        }
    }

    private Brush GetStatusBorderBrush(CodexRuntimeSettingsStatusKind statusKind)
    {
        var resourceName = statusKind switch
        {
            CodexRuntimeSettingsStatusKind.Success => "SystemFillColorSuccessBrush",
            CodexRuntimeSettingsStatusKind.ValidationError => "SystemFillColorCautionBrush",
            CodexRuntimeSettingsStatusKind.PersistenceError => "SystemFillColorCriticalBrush",
            _ => "CardStrokeColorDefaultBrush"
        };

        return (Brush)Application.Current.Resources[resourceName];
    }

    private void FocusRequestedControl(CodexRuntimeSettingsFocusTarget focusTarget)
    {
        switch (focusTarget)
        {
            case CodexRuntimeSettingsFocusTarget.ProviderSelection:
                CodexProviderRadioButton.Focus(FocusState.Programmatic);
                break;
            case CodexRuntimeSettingsFocusTarget.RuntimeMode:
                RuntimeModeComboBox.Focus(FocusState.Programmatic);
                break;
            case CodexRuntimeSettingsFocusTarget.WslDistribution:
                WslDistributionTextBox.Focus(FocusState.Programmatic);
                break;
        }
    }
}
