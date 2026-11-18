namespace ShopClient;

/// <summary>Raised when the shop rejects a single record; the caller may report it and continue.</summary>
public sealed class CatalogException(string message) : Exception(message);

/// <summary>Raised when the shop cannot be used at all (unreachable, timed out, wrong credentials); the caller should stop.</summary>
public sealed class ShopUnavailableException(string message) : Exception(message);

/// <summary>Raised when a shop type is unknown or its client lacks a requested capability.</summary>
public sealed class UnsupportedShopException(string message) : Exception(message);
