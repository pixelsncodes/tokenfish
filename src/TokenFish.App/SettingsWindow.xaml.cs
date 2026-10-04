using Microsoft.UI.Windowing;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TokenFish.Core.Models;
using TokenFish.App.Platform;
using TokenFish.Infrastructure;
using Windows.Graphics;
using TokenFish.Core.Settings;

namespace TokenFish.App;

public sealed partial class SettingsWindow : Window
{
    private readonly CodexRuntimeSettingsPresenter _presenter;
    private readonly Func<RectInt32?> _getPreferredPlacementAnchor;
    private bool _isLoaded;
    private bool _isPositioned;
    private bool _updatingControls;
    private readonly IAppSettingsStore? _visualSettingsStore;
    private readonly SemaphoreSlim _visualSaveGate;
    private bool _loadingVisualControls;
    public event Action<AppSettings>? AppearanceChanged;

    public SettingsWindow(
        CodexRuntimeSettingsPresenter presenter,
        Func<RectInt32?>? getPreferredPlacementAnchor = null,
        IAppSettingsStore? visualSettingsStore = null,
        SemaphoreSlim? settingsSaveGate = null)
    {
        ArgumentNullException.ThrowIfNull(presenter);

        _presenter = presenter;
        _visualSettingsStore = visualSettingsStore;
        _visualSaveGate = settingsSaveGate ?? new SemaphoreSlim(1,1);
        _getPreferredPlacementAnchor = getPreferredPlacementAnchor ?? (() => null);
        InitializeComponent();
        Title = "TokenFish Settings";
        InitializeWindowSize();
        InitializeRuntimeModes();
        RootGrid.Loaded += OnRootGridLoaded;
        ClaudeSetupSection.SizeChanged += (_,_) => ResizeToContent(ensurePositioned:true);
        VersionTextBlock.Text = "TokenFish " + typeof(App).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion.Split('+')[0];
    }

    private async void OnRootGridLoaded(object sender, RoutedEventArgs args)
    {
        RootGrid.Loaded -= OnRootGridLoaded;
        _isLoaded = true;
        ApplyState(_presenter.State, ensurePositioned: false);
        ApplyState(
            await _presenter.LoadAsync(CancellationToken.None),
            ensurePositioned: true);
        if (_visualSettingsStore is not null)
            SetVisualControls(await _visualSettingsStore.LoadAsync(CancellationToken.None));
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
            widthEffectivePixels: SettingsWindowLayoutCalculator.WidthEffectivePixels,
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
        await _visualSaveGate.WaitAsync();
        try { ApplyState(await _presenter.SaveAsync(CancellationToken.None)); }
        finally { _visualSaveGate.Release(); }
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

    public void RepositionForInvocation()
    {
        _isPositioned = false;
        ResizeToContent(ensurePositioned: true);
    }

    internal void SetVisualControls(AppSettings settings)
    {
        _loadingVisualControls = true;
        WidgetVisibleCheckBox.IsChecked=settings.IsDesktopWidgetVisible;
        WidgetOnTopCheckBox.IsChecked=settings.IsDesktopWidgetAlwaysOnTop;
        foreach(ComboBoxItem item in WidgetCornerComboBox.Items)
            if((string)item.Tag==settings.DesktopWidgetCorner.ToString()) WidgetCornerComboBox.SelectedItem=item;
        var theme=settings.ThemeMode switch {ThemeMode.Light=>"Light",ThemeMode.Dark or ThemeMode.Arcade=>"Dark",_=>"System"};
        foreach(ComboBoxItem item in ThemeComboBox.Items)
            if((string)item.Tag==theme) ThemeComboBox.SelectedItem=item;
        TokenFishAppearance.Apply(RootGrid,settings.ThemeMode);
        _loadingVisualControls=false;
    }

    private void OnSectionClicked(object sender,RoutedEventArgs args)
    {
        var section=(sender as Button)?.Tag as string;
        ConnectionsPanel.Visibility=section=="connections"?Visibility.Visible:Visibility.Collapsed;
        DesktopPanel.Visibility=section=="desktop"?Visibility.Visible:Visibility.Collapsed;
        AppearancePanel.Visibility=section=="appearance"?Visibility.Visible:Visibility.Collapsed;
        SaveButton.Visibility=section=="connections"?Visibility.Visible:Visibility.Collapsed;
        _isPositioned=false;
        ResizeToContent(ensurePositioned:true);
    }
    private void OnVisualOptionChanged(object sender,RoutedEventArgs args)=>SaveVisualOptions();
    private void OnVisualSelectionChanged(object sender,SelectionChangedEventArgs args)=>SaveVisualOptions();
    private async void SaveVisualOptions()
    {
        if(!_isLoaded||_loadingVisualControls||_visualSettingsStore is null||
            ThemeComboBox.SelectedItem is not ComboBoxItem themeItem||WidgetCornerComboBox.SelectedItem is not ComboBoxItem cornerItem) return;
        if(!Enum.TryParse<ThemeMode>((string)themeItem.Tag,out var theme)||
            !Enum.TryParse<DesktopWidgetCorner>((string)cornerItem.Tag,out var corner)) return;
        var visible=WidgetVisibleCheckBox.IsChecked==true; var onTop=WidgetOnTopCheckBox.IsChecked==true;
        await _visualSaveGate.WaitAsync();
        try
        {
            var current=await _visualSettingsStore.LoadAsync(CancellationToken.None);
            var saved=current with {ThemeMode=theme,IsDesktopWidgetVisible=visible,IsDesktopWidgetAlwaysOnTop=onTop,DesktopWidgetCorner=corner};
            await _visualSettingsStore.SaveAsync(saved,CancellationToken.None);
            TokenFishAppearance.Apply(RootGrid,saved.ThemeMode);
            AppearanceChanged?.Invoke(saved); VisualStatusTextBlock.Text="Applied";
        }
        catch {VisualStatusTextBlock.Text="Could not save. Please try again.";}
        finally {_visualSaveGate.Release();}
    }

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
            ClaudeSetupSection.Visibility =
                state.IsClaudeSetupVisible ? Visibility.Visible : Visibility.Collapsed;
            ClaudeSetupDescriptionTextBlock.Text = state.ClaudeSetupDescription;
            ClaudeBridgeExecutablePathTextBlock.Text = state.ClaudeBridgeExecutablePath;
            ClaudeStatusLineSettingsTextBox.Text = state.ClaudeStatusLineSettingsSnippet;
            ClaudeWslStatusLineSettingsTextBox.Text = state.ClaudeWslStatusLineSettingsSnippet;
            ClaudeSetupNextStepsTextBlock.Text = state.ClaudeSetupNextSteps;
            SaveButton.IsEnabled = state.CanSave;
            SaveButton.Content = state.IsSaving ? "Saving..." : "Save";
            StatusTextBlock.Text = state.StatusMessage;
            StatusBorder.Visibility = state.IsStatusVisible ? Visibility.Visible : Visibility.Collapsed;
            StatusBorder.BorderBrush = GetStatusBorderBrush(state.StatusKind);
            RestartStatusTextBlock.Text = state.PendingRestartMessage;
            CurrentProviderModeTextBlock.Text = state.RunningProviderSelectionLabel;
            SavedProviderModeTextBlock.Text = state.SavedProviderSelectionLabel;
            RestartStatusBorder.Visibility = state.IsPendingRestartVisible
                ? Visibility.Visible
                : Visibility.Collapsed;
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
        var preferredAnchor = _getPreferredPlacementAnchor();
        var targetWorkArea = SettingsWindowPlacementService.GetTargetWorkArea(
            this,
            preferredAnchor);
        AppWindow.Resize(SettingsWindowSizing.ToOuterPhysicalSize(
            this,
            measuredHeight,
            targetWorkArea));

        if (!ensurePositioned)
        {
            return;
        }

        if (!_isPositioned || !SettingsWindowPlacementService.IsCurrentPositionVisible(this))
        {
            SettingsWindowPlacementService.EnsureVisibleOnMonitor(
                this,
                preferredAnchor);
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
