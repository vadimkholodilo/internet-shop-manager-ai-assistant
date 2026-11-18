namespace ShopClient.UnitTests;

public sealed class ShopClientFactoryTests
{
    private static readonly ShopConnection Connection = new(new Uri("http://127.0.0.1:9"), "key");

    [Fact]
    public void EachSupportedShopTypeYieldsAClientForBothContracts()
    {
        foreach (var name in ShopType.SupportedNames)
        {
            Assert.True(ShopType.TryParse(name, out var type));

            using var general = ShopClientFactory.CreateShopClient(type, Connection);
            using var loading = ShopClientFactory.CreateCatalogLoadingClient(type, Connection);

            Assert.NotNull(general);
            Assert.NotNull(loading);
        }
    }

    [Fact]
    public void AShopTypeWithoutANameIsRejectedWithTheSupportedOnes()
    {
        var error = Assert.Throws<UnsupportedShopException>(() => ShopClientFactory.CreateShopClient(default, Connection));

        Assert.Contains("opencart", error.Message);
    }

    [Theory]
    [InlineData("opencart")]
    [InlineData("OpenCart")]
    public void ShopTypeNamesAreCaseInsensitive(string text) => Assert.True(ShopType.TryParse(text, out _));

    [Theory]
    [InlineData("nope")]
    [InlineData("")]
    [InlineData(null)]
    public void UnknownShopTypeNamesAreRejected(string? text) => Assert.False(ShopType.TryParse(text, out _));

    [Fact]
    public void ANullConnectionIsRejected() =>
        Assert.Throws<ArgumentNullException>(() => ShopClientFactory.CreateShopClient(ShopType.OpenCart, null!));

    [Fact]
    public void ABadConnectionIsRejectedWhenItIsBuilt()
    {
        Assert.Throws<ArgumentNullException>(() => new ShopConnection(null!, "key"));
        Assert.Throws<ArgumentException>(() => new ShopConnection(new Uri("/relative", UriKind.Relative), "key"));
        Assert.Throws<ArgumentException>(() => new ShopConnection(new Uri("ftp://shop.example"), "key"));
        Assert.Throws<ArgumentException>(() => new ShopConnection(new Uri("http://shop.example"), " "));
        Assert.Throws<ArgumentException>(() => new ShopConnection(new Uri("http://shop.example?x=1"), "key"));
        Assert.Throws<ArgumentException>(() => new ShopConnection(new Uri("http://user:pass@shop.example"), "key"));
        Assert.Throws<ArgumentException>(() => new ShopConnection(new Uri("http://shop.example"), "key\n"));
        Assert.Throws<ArgumentException>(() => new ShopConnection(new Uri("http://shop.example"), "ключ"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShopConnection(new Uri("http://shop.example"), "key") { RequestTimeout = TimeSpan.Zero });
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShopConnection(new Uri("http://shop.example"), "key") { MaxRetries = -1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShopConnection(new Uri("http://shop.example"), "key") { RetryDelay = TimeSpan.FromSeconds(-1) });
    }

    [Fact]
    public void TheApiKeyIsNeverPrinted() =>
        Assert.DoesNotContain("secret-key", new ShopConnection(new Uri("http://shop.example"), "secret-key").ToString());
}
