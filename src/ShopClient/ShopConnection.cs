namespace ShopClient;

/// <summary>How to reach a shop's API.</summary>
/// <param name="ApiKey">Credential for the shop's API; never logged.</param>
public sealed record ShopConnection(Uri Url, string ApiKey)
{
    /// <summary>How long one request may take. Raise it for a slow shop.</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(60);
}
