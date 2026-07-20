using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using RepoGalaxy.Desktop.ViewModels;
using RepoGalaxy.Desktop.Services;

namespace RepoGalaxy.Desktop.Views;

public partial class MainWindow : Window
{
    private SplitView? _navigationSplit;
    private SplitView? _detailsSplit;
    private int _layoutMode = -1;
    private WindowsMetroChrome? _nativeChrome;

    public MainWindow()
    {
        InitializeComponent();
        _navigationSplit = this.FindControl<SplitView>("NavigationSplit");
        _detailsSplit = this.FindControl<SplitView>("DetailsSplit");
        SizeChanged += OnWindowSizeChanged;
        Opened += OnWindowOpened;
        Closed += (_, _) => _nativeChrome?.Dispose();
        PropertyChanged += (_, args) =>
        {
            if (args.Property == WindowStateProperty) UpdateMaximizeIcon();
        };
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        _nativeChrome = WindowsMetroChrome.Attach(this, 48);
        ApplyResponsiveLayout(ClientSize.Width);
    }

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e) => ApplyResponsiveLayout(e.NewSize.Width);

    private void ApplyResponsiveLayout(double width)
    {
        var mode = width >= 1440 ? 3 : width >= 1280 ? 2 : width >= 1000 ? 1 : 0;
        if (mode == _layoutMode || _navigationSplit is null || _detailsSplit is null) return;
        _layoutMode = mode;

        if (mode == 3)
        {
            _navigationSplit.DisplayMode = SplitViewDisplayMode.CompactInline;
            _detailsSplit.DisplayMode = SplitViewDisplayMode.Inline;
            if (DataContext is MainWindowViewModel vm) vm.IsNavigationOpen = true;
        }
        else if (mode == 2)
        {
            _navigationSplit.DisplayMode = SplitViewDisplayMode.CompactInline;
            _detailsSplit.DisplayMode = SplitViewDisplayMode.Inline;
            if (DataContext is MainWindowViewModel vm) vm.IsNavigationOpen = false;
        }
        else if (mode == 1)
        {
            _navigationSplit.DisplayMode = SplitViewDisplayMode.CompactInline;
            _detailsSplit.DisplayMode = SplitViewDisplayMode.Overlay;
            if (DataContext is MainWindowViewModel vm) vm.IsNavigationOpen = false;
        }
        else
        {
            _navigationSplit.DisplayMode = SplitViewDisplayMode.Overlay;
            _detailsSplit.DisplayMode = SplitViewDisplayMode.Overlay;
            if (DataContext is MainWindowViewModel vm) vm.IsNavigationOpen = false;
        }
        if (DataContext is MainWindowViewModel viewModel) viewModel.SetDashboardRailInline(mode >= 2);
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || IsInteractive(e.Source)) return;
        if (e.ClickCount == 2) ToggleMaximize();
        else BeginMoveDrag(e);
        e.Handled = true;
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnMaximizeClick(object? sender, RoutedEventArgs e) => ToggleMaximize();
    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        UpdateMaximizeIcon();
    }

    private void UpdateMaximizeIcon()
    {
        if (this.FindControl<TextBlock>("MaximizeIcon") is not { } icon) return;
        icon.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }

    private static bool IsInteractive(object? source)
    {
        for (var control = source as Control; control is not null; control = control.Parent as Control)
            if (control is Button or TextBox or ComboBox) return true;
        return false;
    }
}
