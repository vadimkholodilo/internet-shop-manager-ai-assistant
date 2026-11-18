namespace ShopClient;

/// <summary>
/// What any consumer needs from a shop. Shop specifics stay behind this interface. Everything a client reports about
/// a shop derives from <see cref="ShopClientException"/>; a bad argument raises <see cref="ArgumentException"/> instead.
/// </summary>
public interface IShopClient : IDisposable
{
    /// <summary>Returns when the shop answers; tries at least once and gives up after <paramref name="timeout"/> in total, however slow each attempt is.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
    /// <exception cref="ShopUnavailableException">The shop did not answer in time, or the credentials were rejected.</exception>
    Task WaitUntilReadyAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}
