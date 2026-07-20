using Avalonia.Controls;
using Avalonia.Controls.Notifications;

namespace RepoGalaxy.Desktop.Services;

/// <summary>Session-bound in-app notification service for the desktop shell.</summary>
public class ToastNotificationService : INotificationService
{
    private static WindowNotificationManager? _notificationManager;

    public ToastNotificationService(Control? control)
    {
        if (control != null) Attach(control);
    }

    public static void Attach(Control control)
    {
        var topLevel = TopLevel.GetTopLevel(control);
        if (topLevel == null) return;
        _notificationManager = new WindowNotificationManager(topLevel)
        {
            MaxItems = 3,
            Position = NotificationPosition.BottomRight
        };
    }

    public void ShowInfo(string message) => _notificationManager?.Show(new Notification("RepoGalaxy", message, NotificationType.Information));
    public void ShowSuccess(string message) => _notificationManager?.Show(new Notification("RepoGalaxy", message, NotificationType.Success));
    public void ShowWarning(string message) => _notificationManager?.Show(new Notification("RepoGalaxy", message, NotificationType.Warning));
    public void ShowError(string message) => _notificationManager?.Show(new Notification("RepoGalaxy", message, NotificationType.Error));
}
