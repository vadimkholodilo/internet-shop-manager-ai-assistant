using System.Text;

namespace ShopClient;

/// <summary>How to reach a shop's API. Invalid values are rejected when the connection is built, not on the first request.</summary>
public sealed record ShopConnection
{
    private readonly TimeSpan requestTimeout = TimeSpan.FromSeconds(60);

    /// <param name="url">Absolute http or https address of the shop.</param>
    /// <param name="apiKey">Credential for the shop's API; never logged.</param>
    /// <exception cref="ArgumentNullException"><paramref name="url"/> or <paramref name="apiKey"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="url"/> is not an absolute http(s) address, or <paramref name="apiKey"/> is blank.</exception>
    public ShopConnection(Uri url, string apiKey)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        if (!url.IsAbsoluteUri || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException($"The shop URL must be an absolute http or https address, got \"{url}\".", nameof(url));
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

    // The generated ToString would print every property, including the key.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Url = {Url}, ApiKey = ***, RequestTimeout = {RequestTimeout}");
        return true;
    }
}
