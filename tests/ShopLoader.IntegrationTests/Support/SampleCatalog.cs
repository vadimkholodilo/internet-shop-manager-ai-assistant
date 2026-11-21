using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;

namespace ShopLoader.IntegrationTests;

/// <summary>A product of the committed demo catalog, as the storefront should show it.</summary>
public sealed record SampleProduct(string Sku, string Category, string Name, string Brand, string Price, string Attribute, string Image);

/// <summary>
/// Reads expectations from data/sample-catalog.json, so tests follow the catalog instead of naming its products.
/// This is the only place that knows the product file's field names on the integration side.
/// </summary>
internal static class SampleCatalog
{
    private static readonly JsonObject File =
        JsonNode.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(Repository.Root, "data", "sample-catalog.json")))!.AsObject();

    public static int ProductCount => File["products"]!.AsArray().Count;

    /// <summary>Names of the top categories (those without a parent).</summary>
    public static IReadOnlyList<string> TopCategoryNames =>
        [.. File["categories"]!.AsArray().Where(c => c!["parent"] is null).Select(c => (string)c!["name"]!)];

    /// <summary>
    /// The first in-stock product whose name, brand and first attribute need no HTML escaping, so they can be found
    /// in a page as they are.
    /// </summary>
    public static SampleProduct Product { get; } = File["products"]!.AsArray()
        .Select(p => p!.AsObject())
        .Where(p => (int)p["quantity"]! > 0)
        .Select(p => new SampleProduct(
            (string)p["sku"]!,
            (string)p["category"]!,
            (string)p["name"]!,
            (string)p["brand"]!,
            ((decimal)p["price"]!).ToString("#,0", new NumberFormatInfo { NumberGroupSeparator = " " }) + " ₽",
            p["attributes"]!.AsObject().Select(a => a.Key).FirstOrDefault() ?? "",
            System.IO.Path.GetFileNameWithoutExtension((string)p["image"]!)))
        .First(p => p.Attribute.Length > 0 && new[] { p.Name, p.Brand, p.Attribute }.All(t => WebUtility.HtmlEncode(t) == t));
}
