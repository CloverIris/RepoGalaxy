namespace RepoGalaxy.Desktop.Services;

/// <summary>
/// 通知服务接口
/// </summary>
public interface INotificationService
{
    void ShowInfo(string message);
    void ShowSuccess(string message);
    void ShowWarning(string message);
    void ShowError(string message);
}
