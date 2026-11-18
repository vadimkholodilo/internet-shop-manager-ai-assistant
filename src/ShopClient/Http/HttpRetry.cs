using System.Net;

namespace ShopClient.Http;

/// <summary>
/// Sends a request again when it failed in a temporary way. Only for requests that are safe to repeat; every request
/// the library makes is (upserts by key, image upload, reads).
/// </summary>
internal static class HttpRetry
{
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(10);

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

            var wait = Wait(connection.RetryDelay, attempt, response);
            response?.Dispose();
            await Task.Delay(wait, cancellationToken);
        }
    }

    /// <summary>Appended to messages of failures that came after repeated attempts.</summary>
    public static string Attempts(int attempts) => attempts > 1 ? $" (after {attempts} attempts)" : "";

    public static bool IsTemporary(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private static TimeSpan Wait(TimeSpan baseDelay, int attempt, HttpResponseMessage? response)
    {
        // Doubles each time; half of it is random so that many clients do not come back together.
        var growth = Math.Min(baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1), MaxDelay.TotalMilliseconds);
        var wait = TimeSpan.FromMilliseconds(growth / 2 + Random.Shared.NextDouble() * growth / 2);

        var retryAfter = response?.Headers.RetryAfter;
        var requested = retryAfter?.Delta ?? (retryAfter?.Date - DateTimeOffset.UtcNow);
        return requested is { } asked && asked > wait ? (asked < MaxDelay ? asked : MaxDelay) : wait;
    }
}
