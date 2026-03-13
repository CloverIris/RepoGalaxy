using System;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;

namespace RepoGalaxy.Desktop.Services;

/// <summary>
/// Toast 通知服务实现
/// </summary>
public class ToastNotificationService : INotificationService
{
    private readonly WindowNotificationManager? _notificationManager;

    public ToastNotificationService(Control? control)
    {
        // 获取 TopLevel 窗口
        var topLevel = control != null ? TopLevel.GetTopLevel(control) : null;
        if (topLevel != null)
        {
            _notificationManager = new WindowNotificationManager(topLevel)
            {
                MaxItems = 3,
                Position = NotificationPosition.BottomRight
            };
        }
    }

    public void ShowInfo(string message)
    {
        _notificationManager?.Show(new Notification("提示", message, NotificationType.Information));
    }

    public void ShowSuccess(string message)
    {
        _notificationManager?.Show(new Notification("成功", message, NotificationType.Success));
    }

    public void ShowWarning(string message)
    {
        _notificationManager?.Show(new Notification("警告", message, NotificationType.Warning));
    }

    public void ShowError(string message)
    {
        _notificationManager?.Show(new Notification("错误", message, NotificationType.Error));
    }
}
