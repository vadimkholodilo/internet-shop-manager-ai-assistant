using System.Net;

namespace ShopClient;

/// <summary>
/// Base of every failure the library reports about a shop or its configuration, so a consumer can catch them all in
/// one place. Bad arguments are not part of it: they raise the usual <see cref="ArgumentException"/> family.
/// </summary>
public abstract class ShopClientException(string message, Exception? innerException = null) : Exception(message, innerException);

/// <summary>Raised when the shop rejects a single record; the caller may report it and continue.</summary>
public sealed class CatalogException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
    : ShopClientException(message, innerException)
{
    /// <summary>The HTTP status the shop answered with, or null when the failure did not come from an answer (for example an unreadable image file).</summary>
    public HttpStatusCode? StatusCode { get; } = statusCode;
}

/// <summary>Raised when the shop cannot be used at all (unreachable, timed out, wrong credentials); the caller should stop.</summary>
public sealed class ShopUnavailableException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
    : ShopClientException(message, innerException)
{
    /// <summary>The HTTP status the shop answered with, or null when it did not answer (connection lost, timeout).</summary>
    public HttpStatusCode? StatusCode { get; } = statusCode;
}

/// <summary>Raised when a shop type is unknown or its client lacks a requested capability.</summary>
public sealed class UnsupportedShopException(string message) : ShopClientException(message);
