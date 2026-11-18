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
}
