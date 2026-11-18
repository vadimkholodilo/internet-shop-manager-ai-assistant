using System.Text;

namespace ShopClient;

/// <summary>How to reach a shop's API. Invalid values are rejected when the connection is built, not on the first request.</summary>
public sealed record ShopConnection
{
    private readonly TimeSpan requestTimeout = TimeSpan.FromSeconds(60);
    private readonly int maxRetries = 3;
    private readonly TimeSpan retryDelay = TimeSpan.FromMilliseconds(500);

    /// <param name="url">Absolute http or https address of the shop.</param>
    /// <param name="apiKey">Credential for the shop's API; never logged.</param>
    /// <exception cref="ArgumentNullException"><paramref name="url"/> or <paramref name="apiKey"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="url"/> is not an absolute http(s) address or has a query, fragment or credentials, or <paramref name="apiKey"/> is blank or has characters other than visible ASCII.</exception>
    public ShopConnection(Uri url, string apiKey)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        if (!url.IsAbsoluteUri || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException($"The shop URL must be an absolute http or https address, got \"{url}\".", nameof(url));
        }

        if (url.Query.Length > 0 || url.Fragment.Length > 0 || url.UserInfo.Length > 0)
        {
            // A query or fragment would be lost when request paths are added to the address, and credentials in the
            // address would end up in messages and logs.
            throw new ArgumentException("The shop URL must not contain a query, a fragment or credentials.", nameof(url));
        }
        if (apiKey.Any(c => c is <= ' ' or > '~'))
        {
            // Typically a newline or space copied along with the key; it would break the Authorization header.
            throw new ArgumentException("The API key must consist of visible ASCII characters only (no spaces, line breaks or non-ASCII).", nameof(apiKey));
        }

        Url = url;
        ApiKey = apiKey;
    }

    public Uri Url { get; }

    public string ApiKey { get; }

    /// <summary>How long one request may take. Raise it for a slow shop.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public TimeSpan RequestTimeout
    {
        get => requestTimeout;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            requestTimeout = value;
        }
    }

    /// <summary>
    /// How many times a request that failed in a temporary way (lost connection, timeout, 408, 429, 500, 502, 503, 504) is
    /// repeated after the first attempt. 0 turns retries off.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int MaxRetries
    {
        get => maxRetries;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            maxRetries = value;
        }
    }

    /// <summary>The wait before the first retry; it doubles for each further retry (with jitter) up to 10 seconds.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan RetryDelay
    {
        get => retryDelay;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
            retryDelay = value;
        }
    }

    // The generated ToString would print every property, including the key.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Url = {Url}, ApiKey = ***, RequestTimeout = {RequestTimeout}");
        return true;
    }
}
