using System.Text.Json;
using System.Text.RegularExpressions;
using ShopLoader.Models;

namespace ShopLoader.Validation;

/// <summary>A record that could not be loaded, with the reason.</summary>
internal sealed record RecordError(string Kind, string Key, string Message);

internal sealed record ValidationResult(ProductFile File, IReadOnlyList<RecordError> Errors);

/// <summary>Raised for problems that make the whole file unusable (nothing is changed in the shop).</summary>
internal sealed class ProductFileException(string message) : Exception(message);

/// <summary>
/// Hand-written validation of the product file (contracts/product-file.schema.json). It avoids reflection
/// and schema libraries so the loader stays Native AOT compatible.
/// </summary>
internal static partial class ProductFileValidator
{
    [GeneratedRegex("^[a-z0-9-]+$")]
    private static partial Regex SlugPattern();

    private static readonly HashSet<string> CategoryFields = ["slug", "name", "parent"];

    private static readonly HashSet<string> ProductFields =
        ["sku", "name", "category", "brand", "description", "attributes", "price", "quantity", "image"];

    /// <summary>Reads and validates a product file. Throws <see cref="ProductFileException"/> if it is unusable.</summary>
    public static ValidationResult ValidateFile(string path, string imagesDirectory)
    {
        if (!File.Exists(path))
        {
            throw new ProductFileException($"Product file not found: {path}");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllBytes(path));
        }
        catch (JsonException e)
        {
            throw new ProductFileException($"Product file is not valid JSON: {e.Message}");
        }

        using (document)
        {
            return Validate(document.RootElement, imagesDirectory);
        }
    }

    public static ValidationResult Validate(JsonElement root, string imagesDirectory)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("categories", out var categoriesElement) || categoriesElement.ValueKind != JsonValueKind.Array ||
            !root.TryGetProperty("products", out var productsElement) || productsElement.ValueKind != JsonValueKind.Array)
        {
            throw new ProductFileException("Product file must be an object with \"categories\" and \"products\" arrays.");
        }

        var errors = new List<RecordError>();
        var categories = ValidateCategories(categoriesElement, errors);
        var products = ValidateProducts(productsElement, categories, imagesDirectory, errors);

        return new ValidationResult(new ProductFile(categories, products), errors);
    }

    private static List<CategoryRecord> ValidateCategories(JsonElement array, List<RecordError> errors)
    {
        var parsed = new List<CategoryRecord>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;

        foreach (var element in array.EnumerateArray())
        {
            var label = $"#{index++}";
            if (element.ValueKind != JsonValueKind.Object)
            {
                errors.Add(new RecordError("category", label, "category must be an object"));
                continue;
            }

            var slug = GetString(element, "slug");
            var key = slug ?? label;

            var problem = UnknownField(element, CategoryFields);
            if (problem is null && (slug is null || !SlugPattern().IsMatch(slug)))
            {
                problem = "slug is required and must match ^[a-z0-9-]+$";
            }
            var name = GetString(element, "name");
            if (problem is null && name is null)
            {
                problem = "name is required";
            }
            var parent = GetString(element, "parent");
            if (problem is null && element.TryGetProperty("parent", out var parentElement) &&
                parentElement.ValueKind != JsonValueKind.Null && parent is null)
            {
                problem = "parent must be a non-empty slug";
            }
            if (problem is null && !seen.Add(slug!))
            {
                problem = "duplicate slug, the first one is kept";
            }

            if (problem is not null)
            {
                errors.Add(new RecordError("category", key, problem));
                continue;
            }

            parsed.Add(new CategoryRecord(slug!, name!, parent));
        }

        return OrderParentsFirst(parsed, errors);
    }

    /// <summary>Puts parents before their children and drops categories with an unknown or circular parent.</summary>
    private static List<CategoryRecord> OrderParentsFirst(List<CategoryRecord> categories, List<RecordError> errors)
    {
        var bySlug = categories.ToDictionary(c => c.Slug, StringComparer.Ordinal);
        var ordered = new List<CategoryRecord>();
        var done = new HashSet<string>(StringComparer.Ordinal);
        var failed = new HashSet<string>(StringComparer.Ordinal);

        bool Visit(CategoryRecord category, HashSet<string> path)
        {
            if (done.Contains(category.Slug))
            {
                return true;
            }
            if (failed.Contains(category.Slug))
            {
                return false;
            }
            if (!path.Add(category.Slug))
            {
                errors.Add(new RecordError("category", category.Slug, "parent chain is circular"));
                failed.Add(category.Slug);
                return false;
            }

            var ok = true;
            if (category.Parent is not null)
            {
                if (!bySlug.TryGetValue(category.Parent, out var parent))
                {
                    errors.Add(new RecordError("category", category.Slug, $"unknown parent category: {category.Parent}"));
                    ok = false;
                }
                else if (!Visit(parent, path))
                {
                    if (!failed.Contains(category.Slug))
                    {
                        errors.Add(new RecordError("category", category.Slug, $"parent category {category.Parent} could not be loaded"));
                    }
                    ok = false;
                }
            }

            path.Remove(category.Slug);
            if (ok)
            {
                done.Add(category.Slug);
                ordered.Add(category);
            }
            else
            {
                failed.Add(category.Slug);
            }
            return ok;
        }

        foreach (var category in categories)
        {
            Visit(category, new HashSet<string>(StringComparer.Ordinal));
        }

        return ordered;
    }

    private static List<ProductRecord> ValidateProducts(
        JsonElement array, IReadOnlyList<CategoryRecord> categories, string imagesDirectory, List<RecordError> errors)
    {
        var known = categories.Select(c => c.Slug).ToHashSet(StringComparer.Ordinal);
        var parsed = new List<ProductRecord>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;

        foreach (var element in array.EnumerateArray())
        {
            var label = $"#{index++}";
            if (element.ValueKind != JsonValueKind.Object)
            {
                errors.Add(new RecordError("product", label, "product must be an object"));
                continue;
            }

            var sku = GetString(element, "sku");
            var key = sku ?? label;
            var problem = ValidateProduct(element, known, imagesDirectory, out var product);

            if (problem is null && !seen.Add(sku!))
            {
                problem = "duplicate sku, the first one is kept";
            }

            if (problem is not null)
            {
                errors.Add(new RecordError("product", key, problem));
                continue;
            }

            parsed.Add(product!);
        }

        return parsed;
    }

    private static string? ValidateProduct(
        JsonElement element, HashSet<string> knownCategories, string imagesDirectory, out ProductRecord? product)
    {
        product = null;

        var unknown = UnknownField(element, ProductFields);
        if (unknown is not null)
        {
            return unknown;
        }

        var sku = GetString(element, "sku");
        if (sku is null)
        {
            return "sku is required";
        }
        if (sku.Any(c => c is '/' or '\\' or '%' or '?' or '#' || char.IsControl(c)))
        {
            return "sku must not contain / \\ % ? # or control characters (it is used in the shop's URLs)";
        }

        string? Required(string field) => GetString(element, field);
        var name = Required("name");
        var category = Required("category");
        var brand = Required("brand");
        var description = Required("description");
        var image = Required("image");

        if (name is null) { return "name is required"; }
        if (category is null) { return "category is required"; }
        if (brand is null) { return "brand is required"; }
        if (description is null) { return "description is required"; }
        if (image is null) { return "image is required"; }

        if (!knownCategories.Contains(category))
        {
            return $"unknown category: {category}";
        }

        if (!element.TryGetProperty("price", out var priceElement) || priceElement.ValueKind != JsonValueKind.Number ||
            !priceElement.TryGetDecimal(out var price) || price <= 0)
        {
            return "price is required and must be a number greater than 0";
        }

        if (!element.TryGetProperty("quantity", out var quantityElement) || quantityElement.ValueKind != JsonValueKind.Number ||
            !quantityElement.TryGetInt32(out var quantity) || quantity < 0)
        {
            return "quantity is required and must be an integer of 0 or more";
        }

        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        if (element.TryGetProperty("attributes", out var attributesElement))
        {
            if (attributesElement.ValueKind != JsonValueKind.Object)
            {
                return "attributes must be an object of text values";
            }
            foreach (var attribute in attributesElement.EnumerateObject())
            {
                if (attribute.Value.ValueKind != JsonValueKind.String || attribute.Name.Length == 0)
                {
                    return "attributes must be an object of text values";
                }
                attributes[attribute.Name] = attribute.Value.GetString()!;
            }
        }

        var imagePath = Path.GetFullPath(Path.Combine(imagesDirectory, image));
        if (!imagePath.StartsWith(Path.GetFullPath(imagesDirectory), StringComparison.Ordinal) || !File.Exists(imagePath))
        {
            return $"image file not found: {image}";
        }

        product = new ProductRecord(sku, name, category, brand, description, attributes, price, quantity, image);
        return null;
    }

    private static string? GetString(JsonElement element, string field) =>
        element.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;

    private static string? UnknownField(JsonElement element, HashSet<string> allowed)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                return $"unknown field: {property.Name}";
            }
        }
        return null;
    }
}
