namespace ShopLoader.UnitTests;

public sealed class CliTests : IDisposable
{
    private readonly TestDirectory directory = new();

    public void Dispose() => directory.Dispose();

    private static async Task<(int Exit, string Error)> Run(params string[] args)
    {
        var error = new StringWriter();
        var exit = await Cli.RunAsync(args, new StringWriter(), error);
        return (exit, error.ToString());
    }

    [Fact]
    public async Task NoArgumentsReportsWhatIsMissing()
    {
        var (exit, error) = await Run();

        Assert.Equal(2, exit);
        Assert.Contains("--file", error);
        Assert.Contains("--url", error);
        Assert.Contains("--help", error);
    }

    [Fact]
    public async Task HelpPrintsUsageAndSucceeds()
    {
        var output = new StringWriter();

        var exit = await Cli.RunAsync(["--help"], output, new StringWriter());

        Assert.Equal(0, exit);
        Assert.Contains("Usage:", output.ToString());
        Assert.Contains("--wait-seconds", output.ToString());
    }

    [Theory]
    [InlineData("--file", "x.json", "--url", "not-a-url", "--api-key", "k")]
    [InlineData("--file", "x.json", "--url", "ftp://shop", "--api-key", "k")]
    public async Task UrlMustBeHttp(params string[] args)
    {
        var (exit, error) = await Run(args);

        Assert.Equal(2, exit);
        Assert.Contains("--url", error);
    }

    [Fact]
    public async Task UnknownOptionIsRejected()
    {
        var (exit, error) = await Run("--file", "x.json", "--url", "http://shop", "--api-key", "k", "--colour", "red");

        Assert.Equal(2, exit);
        Assert.Contains("--colour", error);
    }

    [Fact]
    public async Task ApiKeyIsRequiredWhenNeitherOptionNorEnvironmentProvidesIt()
    {
        var saved = Environment.GetEnvironmentVariable("SHOP_API_KEY");
        Environment.SetEnvironmentVariable("SHOP_API_KEY", null);
        try
        {
            var (exit, error) = await Run("--file", "x.json", "--url", "http://shop");

            Assert.Equal(2, exit);
            Assert.Contains("SHOP_API_KEY", error);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHOP_API_KEY", saved);
        }
    }

    [Fact]
    public async Task MissingFileFailsBeforeContactingTheShop()
    {
        var (exit, error) = await Run("--file", Path.Combine(directory.Path, "none.json"), "--url", "http://127.0.0.1:9", "--api-key", "k", "--wait-seconds", "0");

        Assert.Equal(2, exit);
        Assert.Contains("not found", error);
    }

    [Fact]
    public async Task InvalidJsonFailsBeforeContactingTheShop()
    {
        var file = directory.WriteCatalog("{ nope");

        var (exit, error) = await Run("--file", file, "--url", "http://127.0.0.1:9", "--api-key", "k", "--wait-seconds", "0");

        Assert.Equal(2, exit);
        Assert.Contains("not valid JSON", error);
    }

    [Fact]
    public async Task UnreachableShopFailsClearly()
    {
        directory.AddImage("a.png");
        var file = directory.WriteCatalog("""{"categories":[{"slug":"c","name":"К"}],"products":[]}""");

        var (exit, error) = await Run("--file", file, "--url", "http://127.0.0.1:9", "--api-key", "k", "--wait-seconds", "0");

        Assert.Equal(2, exit);
        Assert.Contains("did not become ready", error);
    }

    [Theory]
    [InlineData("soon")]
    [InlineData("-1")]
    public async Task WaitSecondsMustBeANonNegativeNumber(string value)
    {
        var (exit, error) = await Run("--file", "x.json", "--url", "http://shop", "--api-key", "k", "--wait-seconds", value);

        Assert.Equal(2, exit);
        Assert.Contains("--wait-seconds", error);
    }
}
