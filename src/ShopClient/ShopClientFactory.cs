using ShopClient.OpenCart;

namespace ShopClient;

/// <summary>Creates the client for a shop type. Consumers never name a shop's client class, so a new shop needs no change in them.</summary>
public static class ShopClientFactory
{
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is null.</exception>
    /// <exception cref="UnsupportedShopException"><paramref name="type"/> is not a supported shop type.</exception>
    public static IShopClient CreateShopClient(ShopType type, ShopConnection connection) => Create(type, connection);

    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is null.</exception>
    /// <exception cref="UnsupportedShopException"><paramref name="type"/> is not supported or its client cannot load a catalog.</exception>
    public static ICatalogLoadingClient CreateCatalogLoadingClient(ShopType type, ShopConnection connection)
    {
        var client = Create(type, connection);
        if (client is ICatalogLoadingClient loading)
        {
            return loading;
        }

        client.Dispose();
        throw new UnsupportedShopException($"The {type} shop does not support loading a catalog.");
    }

    private static IShopClient Create(ShopType type, ShopConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        return type == ShopType.OpenCart
            ? new OpenCartClient(connection)
            : throw new UnsupportedShopException(
                $"Unsupported shop type \"{type}\". Supported types: {string.Join(", ", ShopType.SupportedNames)}.");
    }
}
