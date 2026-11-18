namespace ShopClient;

/// <summary>A category of a shop's catalog. <see cref="Slug"/> is its stable key.</summary>
/// <param name="Parent">Slug of the parent category, or null for a top-level category.</param>
public sealed record CatalogCategory(string Slug, string Name, string? Parent);
