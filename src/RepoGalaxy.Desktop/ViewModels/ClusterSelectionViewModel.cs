using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoGalaxy.Desktop.Controls;
using RepoGalaxy.Desktop.Models;
using RepoGalaxy.Desktop.Services;
using Serilog;

namespace RepoGalaxy.Desktop.ViewModels;

/// <summary>
/// 聚类多选面板 ViewModel
/// </summary>
public partial class ClusterSelectionViewModel : ViewModelBase
{
    private readonly ClusterGroup _cluster;
    private readonly INotificationService? _notificationService;
    private readonly Action? _onClose;

    [ObservableProperty]
    private string _clusterName = "相似项目";

    [ObservableProperty]
    private ObservableCollection<ClusterMemberViewModel> _members = new();

    [ObservableProperty]
    private int _selectedCount;

    public int MemberCount => Members.Count;

    public ClusterSelectionViewModel(
        ClusterGroup cluster,
        INotificationService? notificationService = null,
        Action? onClose = null)
    {
        _cluster = cluster;
        _notificationService = notificationService;
        _onClose = onClose;

        // 初始化成员列表
        InitializeMembers();
    }

    private void InitializeMembers()
    {
        ClusterName = _cluster.SeedBubble.Name;

        foreach (var member in _cluster.Members)
        {
            var vm = new ClusterMemberViewModel(member.Bubble);
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ClusterMemberViewModel.IsSelected))
                {
                    UpdateSelectedCount();
                }
            };
            Members.Add(vm);
        }

        UpdateSelectedCount();
    }

    private void UpdateSelectedCount()
    {
        SelectedCount = Members.Count(m => m.IsSelected);
    }

    [RelayCommand]
    private void SelectAll()
    {
        bool allSelected = Members.All(m => m.IsSelected);
        foreach (var member in Members)
        {
            member.IsSelected = !allSelected;
        }
    }

    [RelayCommand]
    private void Close()
    {
        _onClose?.Invoke();
    }

    [RelayCommand]
    private async Task BookmarkSelected()
    {
        var selected = Members.Where(m => m.IsSelected).Select(m => m.Bubble).ToList();
        if (selected.Count == 0)
        {
            _notificationService?.ShowInfo("请先选择要收藏的项目");
            return;
        }

        try
        {
            // TODO: 批量收藏
            foreach (var bubble in selected)
            {
                bubble.IsBookmarked = true;
            }

            _notificationService?.ShowSuccess($"已收藏 {selected.Count} 个项目");
            Log.Information("批量收藏了 {Count} 个项目", selected.Count);
        }
        catch (Exception ex)
        {
            _notificationService?.ShowError("收藏失败");
            Log.Error(ex, "批量收藏失败");
        }
    }

    [RelayCommand]
    private void CompareSelected()
    {
        var selected = Members.Where(m => m.IsSelected).Select(m => m.Bubble).ToList();
        if (selected.Count < 2)
        {
            _notificationService?.ShowInfo("请选择至少 2 个项目进行比较");
            return;
        }

        if (selected.Count > 5)
        {
            _notificationService?.ShowInfo("最多比较 5 个项目");
            return;
        }

        // TODO: 打开比较视图
        _notificationService?.ShowInfo($"准备比较 {selected.Count} 个项目...");
    }

    [RelayCommand]
    private async Task ExportSelected()
    {
        var selected = Members.Where(m => m.IsSelected).Select(m => m.Bubble).ToList();
        if (selected.Count == 0)
        {
            _notificationService?.ShowInfo("请先选择要导出的项目");
            return;
        }

        try
        {
            // 生成导出内容
            var exportData = GenerateExportData(selected);

            // TODO: 保存到文件或剪贴板
            await Task.Delay(100); // 模拟操作

            _notificationService?.ShowSuccess($"已导出 {selected.Count} 个项目");
            Log.Information("批量导出了 {Count} 个项目", selected.Count);
        }
        catch (Exception ex)
        {
            _notificationService?.ShowError("导出失败");
            Log.Error(ex, "批量导出失败");
        }
    }

    private string GenerateExportData(System.Collections.Generic.List<BubbleItem> items)
    {
        var lines = new System.Collections.Generic.List<string>
        {
            "# RepoGalaxy 导出列表",
            $"# 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"# 聚类: {ClusterName}",
            "",
            "| 名称 | 所有者 | 语言 | Stars | 描述 |",
            "|------|--------|------|-------|------|"
        };

        foreach (var item in items)
        {
            var safeDesc = item.Description?.Replace("|", "\\|") ?? "";
            lines.Add($"| {item.Name} | {item.Owner} | {item.PrimaryLanguage} | {(int)item.Stars:N0} | {safeDesc} |");
        }

        return string.Join("\n", lines);
    }
}

/// <summary>
/// 聚类成员 ViewModel (包装 BubbleItem 添加选择状态)
/// </summary>
public partial class ClusterMemberViewModel : ObservableObject
{
    public BubbleItem Bubble { get; }

    [ObservableProperty]
    private bool _isSelected;

    // 暴露 Bubble 的属性
    public string Name => Bubble.Name;
    public string Owner => Bubble.Owner;
    public string? PrimaryLanguage => Bubble.PrimaryLanguage;
    public long Stars => Bubble.Stars;

    public ClusterMemberViewModel(BubbleItem bubble)
    {
        Bubble = bubble;
        IsSelected = false;
    }
}
