namespace ShopLoader.UnitTests;

public sealed class CliTests : UnitTest
{
    [Fact]
    public async Task NoArgumentsReportsWhatIsMissing()
    {
        var run = await RunCliAsync();

        Assert.Equal(2, run.Exit);
        Assert.Contains("--file", run.Error);
        Assert.Contains("--url", run.Error);
        Assert.Contains("--help", run.Error);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://shop")]
    public async Task UrlMustBeHttp(string url)
    {
        var run = await RunCliAsync("--file", "x.json", "--url", url, "--api-key", "k");

        Assert.Equal(2, run.Exit);
        Assert.Contains("--url", run.Error);
    }

    [Fact]
    public async Task ApiKeyIsRequiredWhenNeitherOptionNorEnvironmentProvidesIt()
    {
        var saved = Environment.GetEnvironmentVariable("SHOP_API_KEY");
        Environment.SetEnvironmentVariable("SHOP_API_KEY", null);
        try
        {
            var run = await RunCliAsync("--file", "x.json", "--url", "http://shop");

            Assert.Equal(2, run.Exit);
            Assert.Contains("SHOP_API_KEY", run.Error);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHOP_API_KEY", saved);
        }
    }

    [Fact]
    public async Task MissingFileFailsBeforeContactingTheShop()
    {
        var run = await LoadWithoutAShopAsync(Path.Combine(Workspace.Path, "none.json"));

        Assert.Equal(2, run.Exit);
        Assert.Contains("not found", run.Error);
    }

    [Fact]
    public async Task InvalidJsonFailsBeforeContactingTheShop()
    {
        var run = await LoadWithoutAShopAsync(WriteRawCatalog("{ nope"));

        Assert.Equal(2, run.Exit);
        Assert.Contains("not valid JSON", run.Error);
    }

    [Fact]
    public async Task UnreachableShopFailsClearly()
    {
        var run = await LoadWithoutAShopAsync(WriteCatalog());

        Assert.Equal(2, run.Exit);
        Assert.Contains("did not become ready", run.Error);
    }

    [Theory]
    [InlineData("soon")]
    [InlineData("-1")]
    public async Task WaitSecondsMustBeANonNegativeNumber(string value)
    {
        var run = await RunCliAsync("--file", "x.json", "--url", "http://shop", "--api-key", "k", "--wait-seconds", value);

        Assert.Equal(2, run.Exit);
        Assert.Contains("--wait-seconds", run.Error);
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("")]
    public async Task ShopMustBeASupportedKind(string value)
    {
        var run = await RunCliAsync("--file", "x.json", "--url", "http://shop", "--api-key", "k", "--shop", value);

        Assert.Equal(2, run.Exit);
        Assert.Contains("--shop", run.Error);
        Assert.Contains("opencart", run.Error);
    }

    [Fact]
    public async Task ShopKindIsCaseInsensitive()
    {
        var run = await RunCliAsync("--file", Path.Combine(Workspace.Path, "none.json"), "--url", "http://shop", "--api-key", "k", "--shop", "OpenCart");

        Assert.Contains("not found", run.Error);
    }
}
