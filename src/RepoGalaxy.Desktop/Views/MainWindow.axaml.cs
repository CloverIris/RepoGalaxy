using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using RepoGalaxy.Desktop.ViewModels;

namespace RepoGalaxy.Desktop.Views;

public partial class MainWindow : Window
{
    private SplitView? _navigationSplit;
    private SplitView? _detailsSplit;
    private int _layoutMode = -1;

    public MainWindow()
    {
        InitializeComponent();
        _navigationSplit = this.FindControl<SplitView>("NavigationSplit");
        _detailsSplit = this.FindControl<SplitView>("DetailsSplit");
        SizeChanged += OnWindowSizeChanged;
        Opened += OnWindowOpened;
    }

    private void OnWindowOpened(object? sender, EventArgs e) => ApplyResponsiveLayout(ClientSize.Width);

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
}
