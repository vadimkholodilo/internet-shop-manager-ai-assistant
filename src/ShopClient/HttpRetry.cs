using System.Net;

namespace ShopClient;

/// <summary>
/// Sends a request again when it failed in a temporary way. Only for requests that are safe to repeat; every request
/// the library makes is (upserts by key, image upload, reads).
/// </summary>
internal static class HttpRetry
{
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(10);

    /// <summary>The longest wait a shop may ask for with Retry-After; asking for more ends the retrying.</summary>
    private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Returns the last response, which may still carry a temporary status once the retries are used up, with the number of
    /// attempts made. Throws <see cref="ShopUnavailableException"/> when the last attempt got no answer at all.
    /// </summary>
    /// <param name="createRequest">Builds a fresh request, content included: a sent request cannot be sent again.</param>
    public static async Task<(HttpResponseMessage Response, int Attempts)> SendAsync(
        HttpClient http, ShopConnection connection, Func<HttpRequestMessage> createRequest, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            HttpResponseMessage? response = null;
            string? failure = null;
            HttpStatusCode? failureStatus = null;
            Exception? failureCause = null;
            try
            {
                using var request = createRequest();
                response = await http.SendAsync(request, cancellationToken);
                if (!IsTemporary(response.StatusCode) || attempt > connection.MaxRetries)
                {
                    return (response, attempt);
                }
            }
            catch (HttpRequestException e) when (e.HttpRequestError is HttpRequestError.NameResolutionError or HttpRequestError.SecureConnectionError)
            {
                // An unknown host or a bad certificate does not fix itself between attempts; stop at once.
                throw new ShopUnavailableException($"Cannot reach the shop: {e.Message}", e.StatusCode, e);
            }
            catch (HttpRequestException e)
            {
                (failure, failureStatus, failureCause) = ($"Lost connection to the shop: {e.Message}", e.StatusCode, e);
            }
            catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested)
            {
                // HttpClient reports its own timeout this way. A shop that stops answering ends the run like a lost
                // connection does, instead of making every remaining record wait for the timeout.
                (failure, failureCause) = ($"The shop did not answer within {connection.RequestTimeout.TotalSeconds:0.##} seconds.", e);
            }

            if (failure is not null && attempt > connection.MaxRetries)
            {
                throw new ShopUnavailableException(failure + Attempts(attempt), failureStatus, failureCause);
            }

            var requested = RetryAfter(response);
            if (response is not null && requested > MaxRetryAfter)
            {
                // The shop asks for a pause longer than a load should sit through: stop here with what it said.
                return (response, attempt);
            }

            var wait = Wait(connection.RetryDelay, attempt, requested);
            response?.Dispose();
            await Task.Delay(wait, cancellationToken);
        }
    }

    /// <summary>Appended to messages of failures that came after repeated attempts.</summary>
    public static string Attempts(int attempts) => attempts > 1 ? $" (after {attempts} attempts)" : "";

    /// <summary>Statuses that say the shop cannot serve anyone right now, as opposed to a problem with one record.</summary>
    public static bool IsShopUnavailable(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    /// <summary>The unavailable statuses plus 500 and 423 (the item is busy), which stay a failure of the record once the retries are used up.</summary>
    public static bool IsTemporary(HttpStatusCode status) =>
        IsShopUnavailable(status) || status is HttpStatusCode.InternalServerError or HttpStatusCode.Locked;

    private static TimeSpan? RetryAfter(HttpResponseMessage? response)
    {
        var header = response?.Headers.RetryAfter;
        return header?.Delta ?? (header?.Date - DateTimeOffset.UtcNow);
    }

    private static TimeSpan Wait(TimeSpan baseDelay, int attempt, TimeSpan? requested)
    {
        // Doubles each time; half of it is random so that many clients do not come back together.
        var growth = Math.Min(baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1), MaxDelay.TotalMilliseconds);
        var wait = TimeSpan.FromMilliseconds(growth / 2 + Random.Shared.NextDouble() * growth / 2);

        // What the shop asked for is honored exactly, never shortened.
        return requested is { } asked && asked > wait ? asked : wait;
    }
}
