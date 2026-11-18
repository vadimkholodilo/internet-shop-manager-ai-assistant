using System.Text.Json.Serialization;

namespace ShopClient.OpenCart;

internal sealed record CategoryRequest(string Name, string? Parent);

internal sealed record ProductRequest(
    string Name,
    string Category,
    string Brand,
    string Description,
    IReadOnlyDictionary<string, string> Attributes,
    decimal Price,
    int Quantity);

/// <summary>The fields of an item representation the client needs; ids are the shop's numeric ids.</summary>
internal sealed record ItemResponse(long Id, bool? Created);

internal sealed record ErrorResponse(string? Error);

// Source-generated serializer: no reflection, so consumers stay Native AOT compatible.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(CategoryRequest))]
[JsonSerializable(typeof(ProductRequest))]
[JsonSerializable(typeof(ItemResponse))]
[JsonSerializable(typeof(ErrorResponse))]
internal sealed partial class OpenCartJsonContext : JsonSerializerContext;
