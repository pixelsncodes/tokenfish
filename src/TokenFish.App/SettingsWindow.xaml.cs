using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TokenFish.App.Platform;
using TokenFish.Infrastructure;

namespace TokenFish.App;

public sealed partial class SettingsWindow : Window
{
    private readonly CodexRuntimeSettingsPresenter _presenter;
    private bool _updatingControls;

    public SettingsWindow(CodexRuntimeSettingsPresenter presenter)
    {
        ArgumentNullException.ThrowIfNull(presenter);

        _presenter = presenter;
        InitializeComponent();
        Title = "TokenFish Settings";
        InitializeWindowSize();
        InitializeRuntimeModes();
        RootGrid.Loaded += OnRootGridLoaded;
    }

    private async void OnRootGridLoaded(object sender, RoutedEventArgs args)
    {
        RootGrid.Loaded -= OnRootGridLoaded;
        ApplyState(_presenter.State);
        ApplyState(await _presenter.LoadAsync(CancellationToken.None));
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

    private void ApplyState(CodexRuntimeSettingsViewState state)
    {
        _updatingControls = true;
        try
        {
            RuntimeModeComboBox.SelectedItem = state.RuntimeModeOptions
                .FirstOrDefault(option => option.RuntimeMode == state.RuntimeMode);

            if (WslDistributionTextBox.Text != state.WslDistributionName)
            {
                WslDistributionTextBox.Text = state.WslDistributionName;
            }

            WslDistributionTextBox.IsEnabled = state.IsWslDistributionEnabled;
            SaveButton.IsEnabled = state.CanSave;
            SaveButton.Content = state.IsSaving ? "Saving..." : "Save";
            StatusTextBlock.Text = state.StatusMessage;
            StatusBorder.BorderBrush = GetStatusBorderBrush(state.StatusKind);
        }
        finally
        {
            _updatingControls = false;
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
            case CodexRuntimeSettingsFocusTarget.RuntimeMode:
                RuntimeModeComboBox.Focus(FocusState.Programmatic);
                break;
            case CodexRuntimeSettingsFocusTarget.WslDistribution:
                WslDistributionTextBox.Focus(FocusState.Programmatic);
                break;
        }
    }
}
