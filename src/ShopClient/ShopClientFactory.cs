using ShopClient.OpenCart;

namespace ShopClient;

/// <summary>Creates the client for a shop type. Consumers never name a shop's client class, so a new shop needs no change in them.</summary>
public static class ShopClientFactory
{
    public static IShopClient CreateShopClient(ShopType type, ShopConnection connection) => Create(type, connection);

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

    private static IShopClient Create(ShopType type, ShopConnection connection) =>
        type == ShopType.OpenCart
            ? new OpenCartClient(connection)
            : throw new UnsupportedShopException(
                $"Unsupported shop type \"{type}\". Supported types: {string.Join(", ", ShopType.SupportedNames)}.");
}
