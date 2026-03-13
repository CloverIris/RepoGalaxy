using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using RepoGalaxy.Desktop.Controls;
using RepoGalaxy.Desktop.ViewModels;
using RepoGalaxy.Core.Interfaces;

namespace RepoGalaxy.Desktop.Views.Pages;

public partial class ExploreView : UserControl
{
    private IServiceScope? _scope;
    private ClusterManager? _clusterManager;

    public ExploreView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        
        // 加载完成后注入 ClusterManager
        this.AttachedToVisualTree += (_, _) =>
        {
            // 创建作用域获取 Scoped 服务
            _scope = App.Services?.CreateScope();
            _clusterManager = _scope?.ServiceProvider.GetService<ClusterManager>();
            
            if (_clusterManager != null)
            {
                var bubbleCloud = this.FindControl<BubbleCloudControl>("BubbleCloud");
                bubbleCloud?.SetClusterManager(_clusterManager);
            }
        };
        
        // 清理作用域
        this.DetachedFromVisualTree += (_, _) =>
        {
            _scope?.Dispose();
            _scope = null;
            _clusterManager = null;
        };
    }
}
