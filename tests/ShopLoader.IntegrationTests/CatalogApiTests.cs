using System.Net;

namespace ShopLoader.IntegrationTests;

/// <summary>The catalog API extension: authentication, ids in responses, validation and keyed upserts.</summary>
public sealed class CatalogApiTests(ShopStack shop) : ShopTest(shop)
{
    [Theory]
    [InlineData("")]
    [InlineData("wrong-key")]
    public async Task RequestsWithoutAValidKeyAreRejectedAndChangeNothing(string key)
    {
        using var unauthorized = Api.WithKey(key);
        var slug = NewSlug();
        var before = await Api.CategoryCountAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await unauthorized.TryListProductsAsync()).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await unauthorized.TryListCategoriesAsync()).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await unauthorized.TryUpsertCategoryAsync(slug, "Не должна появиться")).Status);

        Assert.Equal(before, await Api.CategoryCountAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await Api.TryGetCategoryAsync(slug)).Status);
    }

    [Fact]
    public async Task CategoryResponsesCarryIdsAndKeys()
    {
        var parent = await Api.UpsertCategoryAsync(NewSlug(), "Родитель");
        var child = await Api.UpsertCategoryAsync(NewSlug(), "Ребёнок", parent.Slug);

        Assert.True(parent.Created);
        Assert.Equal(parent.Id, child.ParentId);
        Assert.Equal(parent.Slug, child.ParentSlug);
        Assert.Equal(await Api.GetCategoryByIdAsync(child.Id), await Api.GetCategoryAsync(child.Slug));
    }

    [Fact]
    public async Task UpsertKeepsTheSameIdAndUpdatesValues()
    {
        var slug = NewSlug();
        var first = await Api.UpsertCategoryAsync(slug, "Было");
        var second = await Api.UpsertCategoryAsync(slug, "Стало");

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal("Стало", second.Name);
    }

    [Fact]
    public async Task ProductResponsesCarryIdsAndAreListed()
    {
        var category = await NewCategoryAsync();
        var product = await Api.UpsertProductAsync(NewSku(), new ProductSpec(category.Slug));

        Assert.Equal(category.Id, product.CategoryId);
        Assert.Equal(product.Sku, (await Api.GetProductByIdAsync(product.Id)).Sku);

        var list = await Api.ListProductsAsync();
        Assert.True(list.Total >= 1);
        Assert.All(list.Items, item => Assert.True(item.Id > 0));
    }

    [Fact]
    public async Task InvalidRequestsAreRejectedWithClearStatusCodes()
    {
        var category = await NewCategoryAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await Api.TryUpsertProductAsync("BAD-1", new ProductSpec(category.Slug, Price: 0))).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await Api.TryUpsertProductAsync("BAD-2", new ProductSpec(category.Slug, Quantity: -1))).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Api.TryUpsertProductAsync("BAD-3", new ProductSpec("no-such-category"))).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Api.TryUpsertCategoryAsync(NewSlug(), "X", parent: "no-such-parent")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await Api.TryUpsertCategoryAsync("Bad_Slug", "X")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Api.TryGetProductAsync("BAD-1")).Status);
    }

    [Fact]
    public async Task ImageUploadRejectsUnsupportedTypesBadImagesAndUnknownProducts()
    {
        var product = await NewProductAsync();

        Assert.Equal(HttpStatusCode.NotFound, await Api.UploadImageAsync("NO-SUCH-SKU", "image/png", [1, 2, 3]));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, await Api.UploadImageAsync(product.Sku, "text/plain", [1, 2, 3]));
        Assert.Equal(HttpStatusCode.BadRequest, await Api.UploadImageAsync(product.Sku, "image/png", [1, 2, 3]));
    }

    [Fact]
    public async Task SkusThatMapToTheSameUrlKeywordAreRejected()
    {
        // AB_1 and AB-1 are different SKUs but would share a URL keyword and a photo file name.
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var spec = new ProductSpec((await NewCategoryAsync()).Slug);

        var first = await Api.TryUpsertProductAsync($"KW_{suffix}", spec);
        var second = await Api.TryUpsertProductAsync($"KW-{suffix}", spec);

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(HttpStatusCode.Conflict, second.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Api.TryGetProductAsync($"KW-{suffix}")).Status);
    }
}
