using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Desktop.Services;

public interface IDesktopNotificationService
{
    void ShowFeedNotification(FeedItem item);
}

/// <summary>Windows-friendly in-session notification adapter. It intentionally stops with the app process.</summary>
public sealed class DesktopNotificationService : IDesktopNotificationService
{
    private readonly INotificationService _notifications;
    public DesktopNotificationService(INotificationService notifications) => _notifications = notifications;
    public void ShowFeedNotification(FeedItem item) => _notifications.ShowInfo($"{item.Repository.FullName} · {item.Reason.Summary}");
}
