using System.Net;
using FivetranClient.Infrastructure;

namespace FivetranClient;

public class HttpRequestHandler
{
    private readonly HttpClient _client;
    private readonly SemaphoreSlim? _semaphore;
    private readonly object _lock = new();
    private DateTime _retryAfterTime = DateTime.UtcNow;
    private static TtlDictionary<string, Task<HttpResponseMessage>> _responseCache = new();

    /// <summary>
    /// Handles HttpTooManyRequests responses by limiting the number of concurrent requests and managing retry logic.
    /// Also caches responses to avoid unnecessary network calls.
    /// </summary>
    /// <remarks>
    /// Set <paramref name="maxConcurrentRequests"/> to 0 to disable concurrency limit.
    /// </remarks>
    public HttpRequestHandler(HttpClient client, ushort maxConcurrentRequests = 0)
    {
        this._client = client;
        if (maxConcurrentRequests > 0)
        {
            this._semaphore = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
        }
    }

    public Task<HttpResponseMessage> GetAsync(string url, CancellationToken cancellationToken)
    {
        return _responseCache.GetOrAdd(
            url,
            () => this._GetAsync(url, cancellationToken),
            TimeSpan.FromMinutes(60));
    }
    
    private async Task<HttpResponseMessage> _GetAsync(string url, CancellationToken cancellationToken)
    {
        byte maxRetries = 10;
        byte attempts = 0;
        if (this._semaphore is not null)
        {
            await this._semaphore.WaitAsync(cancellationToken);
        }

        try
        {
            TimeSpan timeToWait;
            lock (_lock)
            {
                timeToWait = this._retryAfterTime - DateTime.UtcNow;
            }
            if (timeToWait > TimeSpan.Zero)
            {
                await Task.Delay(timeToWait, cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();

            var response = await this._client.GetAsync(new Uri(url, UriKind.RelativeOrAbsolute), cancellationToken);
            while (response.StatusCode is HttpStatusCode.TooManyRequests && attempts < maxRetries)
            {
                    attempts++;
                    if (attempts == maxRetries) throw new HttpRequestException("Too many attempts.");
                    var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(60);
                    lock (_lock)
                    {
                        this._retryAfterTime = DateTime.UtcNow.Add(retryAfter);
                    }
                    // new request will wait for the specified time before retrying
                    await Task.Delay(retryAfter, cancellationToken);
                    response = await this._client.GetAsync(new Uri(url, UriKind.RelativeOrAbsolute), cancellationToken);
            }
            response.EnsureSuccessStatusCode();
            return response;
        }
        finally
        {
            this._semaphore?.Release();
        }
    }
}