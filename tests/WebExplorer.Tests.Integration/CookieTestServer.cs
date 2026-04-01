using System.Net;
using System.Net.Sockets;
using System.Text;

namespace WebExplorer.Tests.Integration;

internal sealed class CookieTestServer : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly HttpListener _listener;
    private readonly Task _serverTask;
    private int _activeDelayRequests;
    private int _maxObservedConcurrentDelayRequests;

    public CookieTestServer()
    {
        var port = GetFreeTcpPort();
        BaseUrl = $"http://127.0.0.1:{port}";
        SetCookieUrl = $"{BaseUrl}/set-cookie";
        EchoCookieUrl = $"{BaseUrl}/echo-cookie";
        DelayedResponseUrl = $"{BaseUrl}/delay?ms=750";

        _listener = new HttpListener();
        _listener.Prefixes.Add($"{BaseUrl}/");
        _listener.Start();
        _serverTask = Task.Run(() => RunAsync(_cancellationTokenSource.Token));
    }

    public string BaseUrl { get; }

    public string SetCookieUrl { get; }

    public string EchoCookieUrl { get; }

    public string DelayedResponseUrl { get; }

    public int MaxObservedConcurrentDelayRequests => Volatile.Read(ref _maxObservedConcurrentDelayRequests);

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _listener.Close();
        try
        {
            _serverTask.GetAwaiter().GetResult();
        }
        catch
        {
            // Best-effort shutdown.
        }
        _cancellationTokenSource.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await WriteResponseAsync(context, cancellationToken);
        }
    }

    private async Task WriteResponseAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";
        string body;

        if (path.Equals("/set-cookie", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.AddHeader("Set-Cookie", "wxp-session=retained; Path=/");
            body = "<html><body><h1>Cookie Set</h1></body></html>";
        }
        else if (path.Equals("/echo-cookie", StringComparison.OrdinalIgnoreCase))
        {
            var cookies = WebUtility.HtmlEncode(context.Request.Headers["Cookie"] ?? string.Empty);
            body = $"<html><body><p>{cookies}</p></body></html>";
        }
        else if (path.Equals("/delay", StringComparison.OrdinalIgnoreCase))
        {
            var active = Interlocked.Increment(ref _activeDelayRequests);
            UpdateMaxConcurrent(active);
            try
            {
                var delay = 750;
                if (int.TryParse(context.Request.QueryString["ms"], out var parsedDelay) && parsedDelay > 0)
                    delay = parsedDelay;

                await Task.Delay(delay, cancellationToken);
                body = $"<html><body><p>delay={delay}</p></body></html>";
            }
            finally
            {
                Interlocked.Decrement(ref _activeDelayRequests);
            }
        }
        else
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            body = "<html><body><h1>Not Found</h1></body></html>";
        }

        var bytes = Encoding.UTF8.GetBytes(body);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, cancellationToken);
        context.Response.OutputStream.Close();
    }

    private void UpdateMaxConcurrent(int observed)
    {
        while (true)
        {
            var current = Volatile.Read(ref _maxObservedConcurrentDelayRequests);
            if (observed <= current)
                return;

            if (Interlocked.CompareExchange(ref _maxObservedConcurrentDelayRequests, observed, current) == current)
                return;
        }
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
