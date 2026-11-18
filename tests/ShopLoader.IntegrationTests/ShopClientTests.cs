using ShopClient;

namespace ShopLoader.IntegrationTests;

/// <summary>
/// The shared shop client used on its own, through the factory and the contracts, against the real shop. This is how
/// a consumer other than the loader uses the library; the loader's reporting is covered by <see cref="LoaderTests"/>.
/// </summary>
public sealed class ShopClientTests(ShopStack shop) : ShopTest(shop)
{
    [Fact]
    public async Task CategoriesProductsAndPhotosAreUpsertedThroughTheContract()
    {
        using var client = NewClient();
        var slug = NewSlug();
        var sku = NewSku();
        await client.WaitUntilReadyAsync(TimeSpan.FromSeconds(60));

        var category = await client.UpsertCategoryAsync(new CatalogCategory(slug, "Для проверок", null));
        var created = await client.UpsertProductAsync(Product(sku, slug, price: 100m));
        var updated = await client.UpsertProductAsync(Product(sku, slug, price: 250m));
        await client.UploadImageAsync(sku, SampleImage);

        Assert.True(category.Created);
        Assert.True(created.Created);
        Assert.False(updated.Created);
        Assert.Equal(created.Id, updated.Id);
        var stored = await Api.GetProductAsync(sku);
        Assert.Equal(250m, stored.Price);
        Assert.NotNull(stored.ImageUrl);
    }

    [Fact]
    public async Task ARecordTheShopRejectsIsReportedAsACatalogException()
    {
        using var client = NewClient();

        await Assert.ThrowsAsync<CatalogException>(() => client.UpsertProductAsync(Product(NewSku(), "no-such-category", price: 100m)));
    }

    private static CatalogProduct Product(string sku, string category, decimal price) =>
        new(sku, "Товар", category, "Бренд", "Описание", new Dictionary<string, string>(), price, 1);
}
