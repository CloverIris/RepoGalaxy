using System.Net;
using System.Text;
using System.Security.Cryptography;

namespace RepoGalaxy.GitHub.Services;

public sealed record OAuthCallback(string? Code, string? State, string? Error);

/// <summary>One-shot loopback listener for an OAuth callback registered with GitHub.</summary>
public sealed class LocalCallbackServer : IDisposable
{
    private readonly int _port;
    private readonly string _path;
    private HttpListener? _listener;
    public string CallbackUrl => $"http://localhost:{_port}{_path}";

    public LocalCallbackServer(int port = 5000, string callbackPath = "/callback")
    {
        _port = port;
        _path = callbackPath.StartsWith('/') ? callbackPath : "/" + callbackPath;
    }

    public async Task<OAuthCallback?> WaitForCallbackAsync(TimeSpan timeout, string expectedState, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{_port}/");
        _listener.Start();
        try
        {
            while (!timeoutCts.IsCancellationRequested)
            {
                var context = await _listener.GetContextAsync().WaitAsync(timeoutCts.Token);
                if (!string.Equals(context.Request.Url?.AbsolutePath, _path, StringComparison.Ordinal))
                {
                    await WritePageAsync(context.Response, 404, "页面不存在", "请返回 RepoGalaxy 完成登录。");
                    continue;
                }
                var query = System.Web.HttpUtility.ParseQueryString(context.Request.Url?.Query ?? string.Empty);
                var callback = new OAuthCallback(query["code"], query["state"], query["error"]);
                var stateMatches = callback.State is not null && FixedTimeEquals(expectedState, callback.State);
                var succeeded = callback.Error == null && callback.Code != null && stateMatches;
                await WritePageAsync(context.Response, succeeded ? 200 : 400,
                    succeeded ? "登录已完成" : "登录未完成",
                    succeeded ? "你可以关闭此页面并返回 RepoGalaxy。" : "授权被取消或验证失败，请回到应用重试。");
                return succeeded ? callback : new OAuthCallback(null, null, callback.Error ?? "invalid_state");
            }
        }
        catch (OperationCanceledException) { }
        finally { Stop(); }
        return null;
    }

    private static async Task WritePageAsync(HttpListenerResponse response, int status, string title, string body)
    {
        var html = $"<!doctype html><html lang='zh-CN'><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>RepoGalaxy · {title}</title><style>body{{margin:0;min-height:100vh;display:grid;place-items:center;background:#f7f7f8;color:#1f2329;font:16px/1.6 'Segoe UI',sans-serif}}main{{max-width:420px;padding:42px;text-align:center;background:#fff;border:1px solid #e5e6eb;border-radius:18px;box-shadow:0 16px 48px #00000012}}h1{{font-size:24px;margin:0 0 12px}}p{{color:#6b7280;margin:0}}</style><main><h1>{WebUtility.HtmlEncode(title)}</h1><p>{WebUtility.HtmlEncode(body)}</p></main></html>";
        var bytes = Encoding.UTF8.GetBytes(html);
        response.StatusCode = status; response.ContentType = "text/html; charset=utf-8"; response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes); response.Close();
    }

    private void Stop() { try { _listener?.Stop(); _listener?.Close(); } catch { } }
    private static bool FixedTimeEquals(string expected, string actual) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
    public void Dispose() => Stop();
}
