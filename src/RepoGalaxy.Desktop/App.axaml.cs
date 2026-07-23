using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using RepoGalaxy.Data.Services;
using RepoGalaxy.Desktop.Services;
using RepoGalaxy.Desktop.ViewModels;
using RepoGalaxy.Desktop.Views;
using RepoGalaxy.Recommendation.Services;
using Serilog;

namespace RepoGalaxy.Desktop;

public partial class App : Application
{
    private readonly IServiceProvider? _serviceProvider;
    private CancellationTokenSource? _startupCancellation;
    private bool _mainWindowStarted;

    public static IServiceProvider? Services => ((App?)Current)?._serviceProvider;

    public App()
    {
    }

    public App(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && _serviceProvider is not null)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var startupWindow = new StartupWindow();
            desktop.MainWindow = startupWindow;

            startupWindow.Opened += async (_, _) => await StartApplicationAsync(desktop, startupWindow);
            startupWindow.RetryRequested += async (_, _) => await StartApplicationAsync(desktop, startupWindow);
            startupWindow.RestoreRequested += async (_, _) => await RestoreAndRetryAsync(desktop, startupWindow);
            startupWindow.Closed += (_, _) =>
            {
                if (_mainWindowStarted) return;
                _startupCancellation?.Cancel();
                desktop.Shutdown();
            };

            desktop.Exit += (_, _) => ShutdownServices();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task StartApplicationAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        StartupWindow startupWindow)
    {
        if (_mainWindowStarted || _serviceProvider is null) return;

        _startupCancellation?.Cancel();
        _startupCancellation?.Dispose();
        _startupCancellation = new CancellationTokenSource();
        var token = _startupCancellation.Token;
        var coordinator = _serviceProvider.GetRequiredService<IApplicationStartupCoordinator>();
        var progress = new Progress<StartupState>(startupWindow.Apply);

        try
        {
            var result = await coordinator.InitializeAsync(progress, token);
            if (!result.Success || token.IsCancellationRequested || _mainWindowStarted) return;

            await _serviceProvider.GetRequiredService<IAppearanceService>().RestoreAsync(token);
            var mainWindow = new MainWindow();
            ConfigureInitialGeometry(mainWindow, startupWindow);
            ToastNotificationService.Attach(mainWindow);

            var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
            mainWindow.DataContext = viewModel;
            mainWindow.Opened += async (_, _) =>
            {
                try
                {
                    await viewModel.InitializeAsync();
                }
                catch (Exception error)
                {
                    Log.Error(error, "\u4e3b\u754c\u9762\u5f02\u6b65\u521d\u59cb\u5316\u5931\u8d25");
                    viewModel.SyncStatus = "\u672c\u5730\u5185\u5bb9\u52a0\u8f7d\u5931\u8d25\uff0c\u8bf7\u624b\u52a8\u91cd\u8bd5";
                }
            };

            _mainWindowStarted = true;
            desktop.MainWindow = mainWindow;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
            startupWindow.Close();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            Log.Error(error, "\u5e94\u7528\u542f\u52a8\u534f\u8c03\u5931\u8d25");
            startupWindow.Apply(new(
                StartupPhase.Failed,
                "\u542f\u52a8\u5931\u8d25",
                error.Message,
                1,
                CanRestore: true));
        }
    }

    private async Task RestoreAndRetryAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        StartupWindow startupWindow)
    {
        if (_serviceProvider is null) return;

        _startupCancellation?.Cancel();
        _startupCancellation?.Dispose();
        _startupCancellation = new CancellationTokenSource();
        var token = _startupCancellation.Token;
        startupWindow.Apply(new(
            StartupPhase.Restoring,
            "\u6b63\u5728\u6062\u590d\u672c\u5730\u6570\u636e",
            "\u6b63\u5728\u4ece\u6700\u8fd1\u7684\u5b8c\u6574\u5907\u4efd\u6062\u590d\u2026",
            .5));

        try
        {
            var restored = await _serviceProvider
                .GetRequiredService<IApplicationStartupCoordinator>()
                .RestoreLatestBackupAsync(token);
            if (!restored)
            {
                startupWindow.Apply(new(
                    StartupPhase.Failed,
                    "\u6ca1\u6709\u53ef\u7528\u5907\u4efd",
                    "\u672a\u627e\u5230\u53ef\u6062\u590d\u7684\u5b8c\u6574\u5907\u4efd\u3002",
                    1));
                return;
            }

            await StartApplicationAsync(desktop, startupWindow);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    private static void ConfigureInitialGeometry(Window mainWindow, Window startupWindow)
    {
        var screen = startupWindow.Screens.ScreenFromWindow(startupWindow)
            ?? startupWindow.Screens.Primary;
        if (screen is null) return;

        var scale = screen.Scaling;
        var workArea = screen.WorkingArea;
        var width = Math.Min(1440, workArea.Width / scale * .9);
        var height = Math.Min(900, workArea.Height / scale * .9);
        mainWindow.Width = Math.Max(mainWindow.MinWidth, width);
        mainWindow.Height = Math.Max(mainWindow.MinHeight, height);
        mainWindow.Position = new PixelPoint(
            workArea.X + (workArea.Width - (int)Math.Round(mainWindow.Width * scale)) / 2,
            workArea.Y + (workArea.Height - (int)Math.Round(mainWindow.Height * scale)) / 2);
    }

    private void ShutdownServices()
    {
        _startupCancellation?.Cancel();
        Log.Information("\u5f00\u59cb\u5173\u95ed RepoGalaxy \u540e\u53f0\u670d\u52a1");
        _serviceProvider?.GetService<DatabaseLifecycleService>()?.MarkCleanShutdown();
        try
        {
            _serviceProvider?.GetService<DiscoverySyncService>()?.StopAsync().GetAwaiter().GetResult();
        }
        catch (Exception error)
        {
            Log.Warning(error, "\u505c\u6b62\u540c\u6b65\u670d\u52a1\u65f6\u53d1\u751f\u53ef\u6062\u590d\u9519\u8bef");
        }
        (_serviceProvider as IDisposable)?.Dispose();
    }
}
