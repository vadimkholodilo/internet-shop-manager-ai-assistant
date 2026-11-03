using System.Text.Json.Serialization;

namespace ShopLoader.Json;

internal sealed record CategoryRequest(string Name, string? Parent);

internal sealed record ProductRequest(
    string Name,
    string Category,
    string Brand,
    string Description,
    IReadOnlyDictionary<string, string> Attributes,
    decimal Price,
    int Quantity);

/// <summary>The fields of an item representation the loader needs; ids are the shop's numeric ids.</summary>
internal sealed record ItemResponse(long Id, bool? Created);

internal sealed record ErrorResponse(string? Error);

internal sealed record ListResponse(int Total);

// Source-generated serializer: no reflection, so the loader stays Native AOT compatible.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(CategoryRequest))]
[JsonSerializable(typeof(ProductRequest))]
[JsonSerializable(typeof(ItemResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(ListResponse))]
internal sealed partial class LoaderJsonContext : JsonSerializerContext;
