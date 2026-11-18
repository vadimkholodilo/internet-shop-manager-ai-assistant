namespace ShopClient;

/// <summary>What any consumer needs from a shop. Shop specifics stay behind this interface.</summary>
public interface IShopClient : IDisposable
{
    /// <summary>Returns when the shop answers; throws <see cref="ShopUnavailableException"/> after <paramref name="timeout"/> or when the credentials are rejected.</summary>
    Task WaitUntilReadyAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}
