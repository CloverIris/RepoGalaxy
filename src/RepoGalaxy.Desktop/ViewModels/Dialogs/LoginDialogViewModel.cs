using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoGalaxy.GitHub.Auth;
using RepoGalaxy.GitHub.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RepoGalaxy.Desktop.ViewModels.Dialogs;

/// <summary>
/// 登录对话框状态
/// </summary>
public enum LoginState
{
    Idle,           // 初始状态，选择登录方式
    DeviceCode,     // 显示设备码
    Polling,        // 轮询中
    Success,        // 登录成功
    Error           // 出错
}

/// <summary>
/// GitHub 登录对话框 ViewModel - 简化版
/// 主要支持 Device Flow（最安全便捷）
/// </summary>
public partial class LoginDialogViewModel : ObservableObject
{
    private readonly GitHubAuthService _authService;
    private readonly GitHubTokenManager _tokenManager;
    private CancellationTokenSource? _cts;
    private string _deviceCodeInternal = "";
    private string _userCode = "";
    private int _pollInterval = 5;
    private DateTime _expiresAt;

    [ObservableProperty]
    private LoginState _currentState = LoginState.Idle;

    [ObservableProperty]
    private string _userCodeDisplay = "";

    [ObservableProperty]
    private string _verificationUrl = "";

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private int _progressPercent;

    [ObservableProperty]
    private string _errorText = "";

    [ObservableProperty]
    private string _patToken = "";

    // 事件
    public event EventHandler? LoginSuccess;
    public event EventHandler? Cancelled;
    public event EventHandler<string>? CopyToClipboard;

    public LoginDialogViewModel(GitHubAuthService authService, GitHubTokenManager tokenManager)
    {
        _authService = authService;
        _tokenManager = tokenManager;
    }

    /// <summary>
    /// 开始 Device Flow 登录（推荐方式）
    /// </summary>
    [RelayCommand]
    private async Task StartDeviceLogin()
    {
        Reset();
        CurrentState = LoginState.Polling;
        StatusText = "正在获取授权码...";
        _cts = new CancellationTokenSource();

        try
        {
            // 1. 获取设备码
            var response = await _authService.StartDeviceFlowAsync(_cts.Token);
            
            _deviceCodeInternal = response.DeviceCode;
            _userCode = response.UserCode;
            _pollInterval = response.Interval;
            _expiresAt = DateTime.Now.AddSeconds(response.ExpiresIn);
            
            UserCodeDisplay = response.UserCode;
            VerificationUrl = response.VerificationUriComplete ?? response.VerificationUri;
            CurrentState = LoginState.DeviceCode;
            StatusText = "请复制下方代码到浏览器授权";

            // 2. 开始轮询
            _ = PollForTokenAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            CurrentState = LoginState.Error;
        }
    }

    /// <summary>
    /// 复制代码并打开浏览器
    /// </summary>
    [RelayCommand]
    private void CopyAndOpenBrowser()
    {
        if (!string.IsNullOrEmpty(_userCode))
        {
            CopyToClipboard?.Invoke(this, _userCode);
            _authService.OpenVerificationPage(VerificationUrl);
            StatusText = "代码已复制，请在浏览器中粘贴";
        }
    }

    /// <summary>
    /// 轮询获取 Token
    /// </summary>
    private async Task PollForTokenAsync(CancellationToken ct)
    {
        try
        {
            while (DateTime.Now < _expiresAt && !ct.IsCancellationRequested)
            {
                // 更新进度
                var elapsed = DateTime.Now - (_expiresAt.AddSeconds(-900)); // 15分钟总时长
                var total = TimeSpan.FromMinutes(15);
                ProgressPercent = Math.Min(100, (int)((DateTime.Now - _expiresAt.AddSeconds(-900)).TotalSeconds / 900 * 100));

                await Task.Delay(_pollInterval * 1000, ct);

                var result = await _authService.PollForTokenAsync(_deviceCodeInternal, _pollInterval, ct);

                switch (result.Status)
                {
                    case DeviceFlowStatus.Success:
                        if (result.Token != null)
                        {
                            await _tokenManager.SaveTokenAsync(result.Token.AccessToken);
                            CurrentState = LoginState.Success;
                            LoginSuccess?.Invoke(this, EventArgs.Empty);
                        }
                        return;

                    case DeviceFlowStatus.Pending:
                        StatusText = "等待授权...";
                        break;

                    case DeviceFlowStatus.SlowDown:
                        _pollInterval = result.NextIntervalSeconds;
                        StatusText = "请求频繁，已自动降速";
                        break;

                    case DeviceFlowStatus.Expired:
                        ErrorText = "授权已过期，请重新尝试";
                        CurrentState = LoginState.Error;
                        return;

                    case DeviceFlowStatus.AccessDenied:
                        ErrorText = "用户取消了授权";
                        CurrentState = LoginState.Error;
                        return;

                    case DeviceFlowStatus.Error:
                        ErrorText = result.ErrorMessage ?? "授权失败";
                        CurrentState = LoginState.Error;
                        return;
                }
            }

            if (!ct.IsCancellationRequested)
            {
                ErrorText = "授权超时（15分钟）";
                CurrentState = LoginState.Error;
            }
        }
        catch (OperationCanceledException)
        {
            // 用户取消，不处理
        }
        catch (Exception ex)
        {
            ErrorText = $"登录失败: {ex.Message}";
            CurrentState = LoginState.Error;
        }
    }

    /// <summary>
    /// 使用 PAT 登录
    /// </summary>
    [RelayCommand]
    private async Task LoginWithPat()
    {
        if (string.IsNullOrWhiteSpace(PatToken))
        {
            ErrorText = "请输入 Personal Access Token";
            CurrentState = LoginState.Error;
            return;
        }

        Reset();
        CurrentState = LoginState.Polling;
        StatusText = "正在验证 Token...";

        try
        {
            var isValid = await _authService.ValidateTokenAsync(PatToken);
            
            if (isValid)
            {
                await _tokenManager.SaveTokenAsync(PatToken);
                CurrentState = LoginState.Success;
                LoginSuccess?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ErrorText = "Token 无效，请检查";
                CurrentState = LoginState.Error;
            }
        }
        catch (Exception ex)
        {
            ErrorText = $"验证失败: {ex.Message}";
            CurrentState = LoginState.Error;
        }
    }

    /// <summary>
    /// 重试
    /// </summary>
    [RelayCommand]
    private void Retry()
    {
        Reset();
    }

    /// <summary>
    /// 取消/关闭
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private void Reset()
    {
        _cts?.Cancel();
        _cts = null;
        _deviceCodeInternal = "";
        _userCode = "";
        UserCodeDisplay = "";
        VerificationUrl = "";
        StatusText = "";
        ErrorText = "";
        ProgressPercent = 0;
        PatToken = "";
        CurrentState = LoginState.Idle;
    }
}
