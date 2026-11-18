using ShopLoader.Models;
using ShopLoader.Validation;

namespace ShopLoader;

/// <summary>Loads a validated product file into a shop. Re-running it applies changes and creates no duplicates.</summary>
internal sealed class Loader(ICatalogClient shop, TextWriter output)
{
    public async Task<LoadReport> LoadAsync(
        ValidationResult validation, string imagesDirectory, CancellationToken cancellationToken)
    {
        var report = new LoadReport();
        foreach (var error in validation.Errors)
        {
            report.AddFailure(error);
            output.WriteLine($"SKIPPED {error.Kind} {error.Key}: {error.Message}");
        }

        var failedCategories = new HashSet<string>(StringComparer.Ordinal);
        foreach (var category in validation.File.Categories)
        {
            if (category.Parent is not null && failedCategories.Contains(category.Parent))
            {
                failedCategories.Add(category.Slug);
                Fail(report, "category", category.Slug, $"parent category {category.Parent} could not be loaded");
                continue;
            }

            try
            {
                var result = await shop.UpsertCategoryAsync(category, cancellationToken);
                report.Category(result.Created);
                output.WriteLine($"{(result.Created ? "created" : "updated")} category {category.Slug} (id {result.Id})");
            }
            catch (CatalogException e)
            {
                failedCategories.Add(category.Slug);
                Fail(report, "category", category.Slug, e.Message);
            }
        }

        foreach (var product in validation.File.Products)
        {
            if (failedCategories.Contains(product.Category))
            {
                Fail(report, "product", product.Sku, $"category {product.Category} could not be loaded");
                continue;
            }

            try
            {
                var result = await shop.UpsertProductAsync(product, cancellationToken);
                await shop.UploadImageAsync(product.Sku, Path.Combine(imagesDirectory, product.Image), cancellationToken);
                report.Product(result.Created);
                output.WriteLine($"{(result.Created ? "created" : "updated")} product {product.Sku} (id {result.Id})");
            }
            catch (CatalogException e)
            {
                Fail(report, "product", product.Sku, e.Message);
            }
        }

        return report;
    }

    private void Fail(LoadReport report, string kind, string key, string message)
    {
        report.AddFailure(new RecordError(kind, key, message));
        output.WriteLine($"FAILED {kind} {key}: {message}");
    }
}
