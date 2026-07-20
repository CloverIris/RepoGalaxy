using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoGalaxy.Desktop.Services;
using RepoGalaxy.GitHub.Auth;
using RepoGalaxy.GitHub.Services;

namespace RepoGalaxy.Desktop.ViewModels.Dialogs;

public enum LoginState { Idle, DeviceCode, Polling, Success, Error }

public sealed partial class LoginDialogViewModel : ObservableObject
{
    private readonly GitHubAuthService _deviceFlow;
    private readonly OAuthCodeFlowService _loopback;
    private readonly GitHubTokenManager _tokens;
    private readonly IAuthenticationAuditService _audit;
    private CancellationTokenSource? _cancellation;
    private string _deviceCode = string.Empty;
    private int _pollInterval;
    private DateTimeOffset _expiresAt;

    public bool CanUseBrowserLogin => _loopback.IsAvailable;
    [ObservableProperty] private LoginState _currentState = LoginState.Idle;
    [ObservableProperty] private string _userCodeDisplay = string.Empty;
    [ObservableProperty] private string _verificationUrl = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private int _progressPercent;
    [ObservableProperty] private string _errorText = string.Empty;
    [ObservableProperty] private string _patToken = string.Empty;
    public event EventHandler? LoginSuccess;
    public event EventHandler? Cancelled;

    public LoginDialogViewModel(GitHubAuthService deviceFlow, OAuthCodeFlowService loopback, GitHubTokenManager tokens, IAuthenticationAuditService audit)
    {
        _deviceFlow = deviceFlow; _loopback = loopback; _tokens = tokens; _audit = audit;
    }

    [RelayCommand] private async Task StartDeviceLoginAsync()
    {
        Reset(); _cancellation = new CancellationTokenSource(); CurrentState = LoginState.Polling; StatusText = "正在准备 GitHub 授权…"; _audit.Record("device-flow", "started");
        try
        {
            var response = await _deviceFlow.StartDeviceFlowAsync(_cancellation.Token);
            _deviceCode = response.DeviceCode; _pollInterval = response.Interval; _expiresAt = DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn);
            UserCodeDisplay = response.UserCode; VerificationUrl = response.VerificationUriComplete ?? response.VerificationUri;
            CurrentState = LoginState.DeviceCode; StatusText = "复制代码并在浏览器中确认授权。";
            _ = PollAsync(_cancellation.Token);
        }
        catch (Exception ex) { Fail("无法开始 GitHub 授权。", ex.GetType().Name); }
    }

    [RelayCommand] private void OpenVerificationPage()
    {
        if (string.IsNullOrWhiteSpace(VerificationUrl)) return;
        _deviceFlow.OpenVerificationPage(VerificationUrl);
        _audit.Record("browser", "opened", "github.com/login/device");
    }

    [RelayCommand] private async Task StartBrowserLoginAsync()
    {
        Reset(); CurrentState = LoginState.Polling; StatusText = "正在打开浏览器并等待本地回环验证…"; _audit.Record("loopback", "started");
        try
        {
            var token = await _loopback.ExecuteOAuthFlowAsync();
            if (token == null) { Fail("授权超时或被取消。", "timeout"); return; }
            await CompleteAsync(token.AccessToken, "OAuth 回环");
        }
        catch (Exception ex) { Fail("浏览器登录未完成。", ex.GetType().Name); }
    }

    [RelayCommand] private async Task LoginWithPatAsync()
    {
        if (string.IsNullOrWhiteSpace(PatToken)) { ErrorText = "请输入 Personal Access Token。"; CurrentState = LoginState.Error; return; }
        CurrentState = LoginState.Polling; StatusText = "正在验证凭证…"; _audit.Record("pat", "started");
        try
        {
            if (!await _deviceFlow.ValidateTokenAsync(PatToken)) { Fail("该 Token 无效或无权访问。", "invalid"); return; }
            await CompleteAsync(PatToken, "Personal Access Token");
        }
        catch (Exception ex) { Fail("凭证验证失败。", ex.GetType().Name); }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (DateTimeOffset.UtcNow < _expiresAt && !cancellationToken.IsCancellationRequested)
            {
                ProgressPercent = Math.Clamp((int)(100 - (_expiresAt - DateTimeOffset.UtcNow).TotalSeconds / 9), 0, 100);
                await Task.Delay(TimeSpan.FromSeconds(_pollInterval), cancellationToken);
                var result = await _deviceFlow.PollForTokenAsync(_deviceCode, _pollInterval, cancellationToken);
                if (result.Status == DeviceFlowStatus.Pending) continue;
                if (result.Status == DeviceFlowStatus.SlowDown) { _pollInterval = result.NextIntervalSeconds; StatusText = "GitHub 要求降低轮询频率，正在继续等待…"; continue; }
                if (result.Status == DeviceFlowStatus.Success && result.Token != null) { await CompleteAsync(result.Token.AccessToken, "设备码"); return; }
                Fail(result.Status == DeviceFlowStatus.AccessDenied ? "你已取消 GitHub 授权。" : "GitHub 授权未完成，请重试。", result.Status.ToString()); return;
            }
            if (!cancellationToken.IsCancellationRequested) Fail("授权已超时，请重新发起登录。", "expired");
        }
        catch (OperationCanceledException) { _audit.Record("device-flow", "cancelled"); }
        catch (Exception ex) { Fail("授权过程中发生网络错误。", ex.GetType().Name); }
    }

    private async Task CompleteAsync(string accessToken, string method)
    {
        if (!await _tokens.SaveTokenAsync(accessToken)) { Fail("无法安全保存凭证。", "storage"); return; }
        CurrentState = LoginState.Success; StatusText = "已安全保存凭证，正在验证账号…"; _audit.Record("credential", "saved", method); LoginSuccess?.Invoke(this, EventArgs.Empty);
    }
    [RelayCommand] private void Retry() => Reset();
    [RelayCommand] private void Cancel() { _cancellation?.Cancel(); _audit.Record("login", "cancelled"); Cancelled?.Invoke(this, EventArgs.Empty); }
    private void Fail(string message, string reason) { ErrorText = message; CurrentState = LoginState.Error; _audit.Record("login", "failed", reason); }
    private void Reset() { _cancellation?.Cancel(); _cancellation = null; _deviceCode = string.Empty; UserCodeDisplay = VerificationUrl = StatusText = ErrorText = string.Empty; ProgressPercent = 0; PatToken = string.Empty; CurrentState = LoginState.Idle; }
}
