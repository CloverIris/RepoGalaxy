using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;

namespace RepoGalaxy.Desktop.ViewModels;

public partial class RepoDetailViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string _repoName = "";

    [ObservableProperty]
    private string _owner = "";

    [ObservableProperty]
    private string _description = "";

    [ObservableProperty]
    private int _starCount;

    [ObservableProperty]
    private int _forkCount;

    [ObservableProperty]
    private int _watchCount;

    [ObservableProperty]
    private int _issueCount;

    [ObservableProperty]
    private DateTime _lastUpdated;

    public string LastUpdatedText => $"更新于 {GetTimeAgo(LastUpdated)}";

    public event EventHandler? CloseRequested;
    public event EventHandler? StarRequested;
    public event EventHandler? CloneRequested;
    public event EventHandler? OpenGitHubRequested;

    [RelayCommand]
    private void Close()
    {
        IsOpen = false;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task Star()
    {
        StarRequested?.Invoke(this, EventArgs.Empty);
        await Task.Delay(100);
    }

    [RelayCommand]
    private void Clone()
    {
        CloneRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void OpenGitHub()
    {
        OpenGitHubRequested?.Invoke(this, EventArgs.Empty);
    }

    public void LoadFromBubble(Controls.RepoBubble bubble)
    {
        RepoName = bubble.RepoName;
        Owner = bubble.Owner;
        StarCount = bubble.StarCount;
        ForkCount = bubble.ForkCount;
        LastUpdated = bubble.LastUpdated;
        
        // 模拟数据
        WatchCount = StarCount / 10;
        IssueCount = Random.Shared.Next(0, 100);
        Description = $"这是一个由 {bubble.Owner} 开发的优秀开源项目。";
        
        IsOpen = true;
    }

    private static string GetTimeAgo(DateTime dateTime)
    {
        var span = DateTime.Now - dateTime;
        return span.TotalDays switch
        {
            < 1 => $"{span.Hours}小时前",
            < 30 => $"{(int)span.TotalDays}天前",
            < 365 => $"{(int)(span.TotalDays / 30)}个月前",
            _ => $"{(int)(span.TotalDays / 365)}年前"
        };
    }
}
