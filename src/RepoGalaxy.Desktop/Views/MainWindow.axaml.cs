using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using RepoGalaxy.Desktop.ViewModels;

namespace RepoGalaxy.Desktop.Views;

public partial class MainWindow : Window
{
    private SplitView? _navigationSplit;
    private SplitView? _detailsSplit;
    private int _layoutMode = -1;
    private static readonly Geometry MaximizeGeometry = Geometry.Parse("M3,3 L17,3 L17,17 L3,17 Z");
    private static readonly Geometry RestoreGeometry = Geometry.Parse("M5,3 L17,3 L17,15 M3,5 L15,5 L15,17 L3,17 Z");

    public MainWindow()
    {
        InitializeComponent();
        _navigationSplit = this.FindControl<SplitView>("NavigationSplit");
        _detailsSplit = this.FindControl<SplitView>("DetailsSplit");
        SizeChanged += OnWindowSizeChanged;
        Opened += OnWindowOpened;
        PropertyChanged += (_, args) =>
        {
            if (args.Property == WindowStateProperty) { UpdateMaximizeIcon(); UpdateResizeChrome(); }
        };
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        ConfigureWindowChrome();
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
        if (this.FindControl<TextBlock>("ConnectionStatusText") is { } connection) connection.IsVisible = width >= 1150;
        if (this.FindControl<TextBlock>("AccountNameText") is { } account) account.IsVisible = width >= 1150;
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
        if (this.FindControl<PathIcon>("MaximizeIcon") is not { } icon) return;
        icon.Data = WindowState == WindowState.Maximized ? RestoreGeometry : MaximizeGeometry;
    }

    private void ConfigureWindowChrome()
    {
        SetRole("TitleBarDragRegion", WindowDecorationsElementRole.TitleBar);
        SetRole("SearchRegion", WindowDecorationsElementRole.User);
        SetRole("AccountRegion", WindowDecorationsElementRole.User);
        SetRole("MinimizeButton", WindowDecorationsElementRole.MinimizeButton);
        SetRole("MaximizeButton", WindowDecorationsElementRole.MaximizeButton);
        SetRole("CloseButton", WindowDecorationsElementRole.CloseButton);
        SetRole("ResizeNorth", WindowDecorationsElementRole.ResizeN);
        SetRole("ResizeSouth", WindowDecorationsElementRole.ResizeS);
        SetRole("ResizeWest", WindowDecorationsElementRole.ResizeW);
        SetRole("ResizeEast", WindowDecorationsElementRole.ResizeE);
        SetRole("ResizeNorthWest", WindowDecorationsElementRole.ResizeNW);
        SetRole("ResizeNorthEast", WindowDecorationsElementRole.ResizeNE);
        SetRole("ResizeSouthWest", WindowDecorationsElementRole.ResizeSW);
        SetRole("ResizeSouthEast", WindowDecorationsElementRole.ResizeSE);
        UpdateResizeChrome();
    }

    private void SetRole(string name, WindowDecorationsElementRole role)
    {
        if (this.FindControl<Control>(name) is { } control) WindowDecorationProperties.SetElementRole(control, role);
    }

    private void UpdateResizeChrome()
    {
        if (this.FindControl<Grid>("ResizeChrome") is { } chrome) chrome.IsVisible = WindowState == WindowState.Normal;
    }
}
