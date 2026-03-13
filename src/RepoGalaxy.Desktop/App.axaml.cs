using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Desktop.ViewModels;
using RepoGalaxy.Desktop.Views;
using Serilog;
using System;

namespace RepoGalaxy.Desktop;

public partial class App : Application
{
    private readonly IServiceProvider? _serviceProvider;
    
    /// <summary>
    /// 全局服务访问器
    /// </summary>
    public static IServiceProvider? Services => ((App?)Current)?._serviceProvider;

    public App()
    {
    }

    public App(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 初始化数据库
        InitializeDatabase();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            
            // 使用 DI 创建 ViewModel
            if (_serviceProvider != null)
            {
                var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
                mainWindow.DataContext = viewModel;
            }

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// 初始化数据库
    /// </summary>
    private void InitializeDatabase()
    {
        try
        {
            if (_serviceProvider != null)
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<RepoGalaxyDbContext>();
                
                // 确保数据库创建
                dbContext.Database.EnsureCreated();
                
                // 种子数据
                DatabaseSeeder.Seed(dbContext);
                
                Log.Information("数据库初始化完成");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "数据库初始化失败");
        }
    }
}
