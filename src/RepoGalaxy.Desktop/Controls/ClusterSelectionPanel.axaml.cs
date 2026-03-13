using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using RepoGalaxy.Desktop.Services;
using RepoGalaxy.Desktop.ViewModels;

namespace RepoGalaxy.Desktop.Controls;

public partial class ClusterSelectionPanel : UserControl
{
    private ClusterGroup? _cluster;
    private List<ClusterMemberItem> _memberItems = new();
    private INotificationService? _notificationService;
    private Action? _onClose;

    public ClusterSelectionPanel()
    {
        InitializeComponent();
        
        // 绑定按钮事件
        var closeBtn = this.FindControl<Button>("CloseButton");
        var selectAllBtn = this.FindControl<Button>("SelectAllButton");
        var bookmarkBtn = this.FindControl<Button>("BookmarkButton");
        var compareBtn = this.FindControl<Button>("CompareButton");
        var exportBtn = this.FindControl<Button>("ExportButton");
        
        if (closeBtn != null) closeBtn.Click += (s, e) => Close();
        if (selectAllBtn != null) selectAllBtn.Click += (s, e) => SelectAll();
        if (bookmarkBtn != null) bookmarkBtn.Click += (s, e) => BookmarkSelected();
        if (compareBtn != null) compareBtn.Click += (s, e) => CompareSelected();
        if (exportBtn != null) exportBtn.Click += (s, e) => ExportSelected();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// 初始化面板
    /// </summary>
    public void Initialize(ClusterGroup cluster, INotificationService? notificationService, Action? onClose)
    {
        _cluster = cluster;
        _notificationService = notificationService;
        _onClose = onClose;
        
        // 更新标题
        var nameText = this.FindControl<TextBlock>("ClusterNameText");
        var countText = this.FindControl<TextBlock>("MemberCountText");
        
        if (nameText != null) nameText.Text = cluster.SeedBubble.Name;
        if (countText != null) countText.Text = $"({cluster.Members.Count} 个项目)";
        
        // 添加成员项
        var membersPanel = this.FindControl<WrapPanel>("MembersPanel");
        if (membersPanel == null) return;
        
        membersPanel.Children.Clear();
        _memberItems.Clear();
        
        foreach (var member in cluster.Members)
        {
            var item = new ClusterMemberItem();
            item.SetMember(member.Bubble);
            item.Margin = new Avalonia.Thickness(4);
            
            // 监听选择变化
            var checkBox = item.FindControl<CheckBox>("SelectionCheckBox");
            if (checkBox != null)
            {
                checkBox.IsCheckedChanged += (s, e) => UpdateSelectedCount();
            }
            
            membersPanel.Children.Add(item);
            _memberItems.Add(item);
        }
        
        UpdateSelectedCount();
    }

    private void UpdateSelectedCount()
    {
        int count = _memberItems.Count(m => m.IsSelected);
        var countText = this.FindControl<TextBlock>("SelectedCountText");
        if (countText != null) countText.Text = $"已选 {count} 项";
    }

    private void SelectAll()
    {
        bool allSelected = _memberItems.All(m => m.IsSelected);
        foreach (var item in _memberItems)
        {
            item.IsSelected = !allSelected;
        }
        UpdateSelectedCount();
    }

    private void Close()
    {
        _onClose?.Invoke();
    }

    private void BookmarkSelected()
    {
        var selected = _memberItems.Where(m => m.IsSelected).Select(m => m.BubbleItem).Where(b => b != null).ToList();
        if (selected.Count == 0)
        {
            _notificationService?.ShowInfo("请先选择要收藏的项目");
            return;
        }

        foreach (var bubble in selected)
        {
            if (bubble != null) bubble.IsBookmarked = true;
        }
        
        _notificationService?.ShowSuccess($"已收藏 {selected.Count} 个项目");
    }

    private void CompareSelected()
    {
        var selected = _memberItems.Where(m => m.IsSelected).Select(m => m.BubbleItem).Where(b => b != null).ToList();
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

        _notificationService?.ShowInfo($"准备比较 {selected.Count} 个项目...");
    }

    private void ExportSelected()
    {
        var selected = _memberItems.Where(m => m.IsSelected).Select(m => m.BubbleItem).Where(b => b != null).ToList();
        if (selected.Count == 0)
        {
            _notificationService?.ShowInfo("请先选择要导出的项目");
            return;
        }

        // TODO: 实现导出功能
        _notificationService?.ShowSuccess($"已导出 {selected.Count} 个项目");
    }
}
