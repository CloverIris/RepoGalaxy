using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace RepoGalaxy.GitHub.Services;

/// <summary>
/// 本地 OAuth 回调服务器
/// 监听浏览器重定向，获取授权码
/// </summary>
public class LocalCallbackServer : IDisposable
{
    private HttpListener? _listener;
    private readonly int _port;
    private readonly string _callbackPath;
    private readonly ILogger<LocalCallbackServer>? _logger;
    private TaskCompletionSource<string>? _codeTcs;
    
    public string CallbackUrl => $"http://localhost:{_port}{_callbackPath}";
    
    public LocalCallbackServer(int port = 5000, string callbackPath = "/callback", ILogger<LocalCallbackServer>? logger = null)
    {
        _port = port;
        _callbackPath = callbackPath;
        _logger = logger;
    }
    
    /// <summary>
    /// 启动服务器并等待授权码
    /// </summary>
    public async Task<string?> WaitForCallbackAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        _codeTcs = new TaskCompletionSource<string>();
        
        try
        {
            Start();
            _logger?.LogInformation("等待 OAuth 回调: {CallbackUrl}", CallbackUrl);
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            
            try
            {
                var code = await _codeTcs.Task.WaitAsync(cts.Token);
                return code;
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("等待 OAuth 回调超时");
                return null;
            }
        }
        finally
        {
            Stop();
        }
    }
    
    private void Start()
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{_port}/");
        _listener.Start();
        
        _ = Task.Run(async () =>
        {
            try
            {
                while (_listener.IsListening)
                {
                    var context = await _listener.GetContextAsync();
                    _ = HandleRequestAsync(context);
                }
            }
            catch (ObjectDisposedException)
            {
                // 正常关闭
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "回调服务器错误");
            }
        });
    }
    
    private void Stop()
    {
        try
        {
            _listener?.Stop();
            _listener?.Close();
        }
        catch { }
    }
    
    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        
        try
        {
            var url = request.Url?.ToString() ?? "";
            _logger?.LogDebug("收到请求: {Url}", url);
            
            // 检查是否是回调路径
            if (request.Url?.AbsolutePath == _callbackPath)
            {
                var query = request.Url.Query;
                var code = ExtractCode(query);
                var error = ExtractError(query);
                
                if (!string.IsNullOrEmpty(code))
                {
                    _logger?.LogInformation("收到授权码");
                    _codeTcs?.TrySetResult(code);
                    
                    // 返回成功页面
                    await SendResponseAsync(response, GetSuccessHtml(), 200);
                }
                else if (!string.IsNullOrEmpty(error))
                {
                    _logger?.LogWarning("授权失败: {Error}", error);
                    _codeTcs?.TrySetException(new InvalidOperationException($"OAuth error: {error}"));
                    await SendResponseAsync(response, GetErrorHtml(error), 400);
                }
                else
                {
                    await SendResponseAsync(response, GetErrorHtml("No code received"), 400);
                }
            }
            else
            {
                // 返回等待页面
                await SendResponseAsync(response, GetWaitingHtml(), 200);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "处理请求失败");
            try
            {
                await SendResponseAsync(response, GetErrorHtml(ex.Message), 500);
            }
            catch { }
        }
    }
    
    private static string ExtractCode(string query)
    {
        var match = Regex.Match(query, @"[?&]code=([^&]+)");
        return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value) : "";
    }
    
    private static string ExtractError(string query)
    {
        var match = Regex.Match(query, @"[?&]error=([^&]+)");
        return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value) : "";
    }
    
    private static async Task SendResponseAsync(HttpListenerResponse response, string html, int statusCode)
    {
        response.StatusCode = statusCode;
        response.ContentType = "text/html; charset=utf-8";
        
        var buffer = Encoding.UTF8.GetBytes(html);
        response.ContentLength64 = buffer.Length;
        
        await response.OutputStream.WriteAsync(buffer);
        response.OutputStream.Close();
    }
    
    private static string GetWaitingHtml() => @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>RepoGalaxy - 等待授权</title>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; 
               background: #0b0e14; color: #fff; display: flex; align-items: center; 
               justify-content: center; height: 100vh; margin: 0; }
        .container { text-align: center; }
        .spinner { width: 40px; height: 40px; border: 3px solid #30363d; 
                   border-top-color: #58a6ff; border-radius: 50%; 
                   animation: spin 1s linear infinite; margin: 0 auto 20px; }
        @keyframes spin { to { transform: rotate(360deg); } }
        h1 { font-size: 24px; margin-bottom: 10px; }
        p { color: #8b949e; }
    </style>
</head>
<body>
    <div class='container'>
        <div class='spinner'></div>
        <h1>等待 GitHub 授权...</h1>
        <p>请在浏览器中完成授权，此页面将自动关闭</p>
    </div>
</body>
</html>";
    
    private static string GetSuccessHtml() => @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>RepoGalaxy - 授权成功</title>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; 
               background: #0b0e14; color: #fff; display: flex; align-items: center; 
               justify-content: center; height: 100vh; margin: 0; }
        .container { text-align: center; }
        .icon { font-size: 48px; margin-bottom: 20px; }
        h1 { font-size: 24px; margin-bottom: 10px; color: #238636; }
        p { color: #8b949e; }
        .btn { background: #238636; color: white; padding: 10px 20px; 
               text-decoration: none; border-radius: 6px; display: inline-block; 
               margin-top: 20px; }
    </style>
</head>
<body>
    <div class='container'>
        <div class='icon'>✅</div>
        <h1>授权成功！</h1>
        <p>GitHub 授权已完成，您可以关闭此页面</p>
        <a href='repogalaxy://auth/success' class='btn'>返回 RepoGalaxy</a>
    </div>
</body>
</html>";
    
    private static string GetErrorHtml(string error) => $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>RepoGalaxy - 授权失败</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; 
               background: #0b0e14; color: #fff; display: flex; align-items: center; 
               justify-content: center; height: 100vh; margin: 0; }}
        .container {{ text-align: center; padding: 20px; }}
        .icon {{ font-size: 48px; margin-bottom: 20px; }}
        h1 {{ font-size: 24px; margin-bottom: 10px; color: #f85149; }}
        .error {{ color: #f85149; background: #da36331a; padding: 10px; 
                  border-radius: 6px; margin-top: 10px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='icon'>❌</div>
        <h1>授权失败</h1>
        <p class='error'>{error}</p>
    </div>
</body>
</html>";
    
    public void Dispose()
    {
        Stop();
    }
}
