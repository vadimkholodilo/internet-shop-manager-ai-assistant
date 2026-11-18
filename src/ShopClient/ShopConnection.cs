namespace ShopClient;

/// <summary>How to reach a shop's API.</summary>
/// <param name="ApiKey">Credential for the shop's API; never logged.</param>
/// <param name="RequestTimeout">How long one request may take (default 60 seconds).</param>
public sealed record ShopConnection(Uri Url, string ApiKey, TimeSpan? RequestTimeout = null);
