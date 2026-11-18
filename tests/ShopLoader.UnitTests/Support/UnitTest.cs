using ShopLoader.Validation;

namespace ShopLoader.UnitTests;

/// <summary>What the loader's command line printed and returned.</summary>
public sealed record CliRun(int Exit, string Output, string Error);

/// <summary>
/// Base class for unit tests that need a product file or the command line. Everything that knows the format of the
/// product file or the names of the command-line options lives here, so a change to either is made once.
/// Unit tests cover logic that needs no shop; anything that involves the shop belongs in the integration tests.
/// </summary>
public abstract class UnitTest : IDisposable
{
    protected const string PhonesCategory = """[{"slug":"phones","name":"Телефоны"}]""";

    /// <summary>A temporary directory with an images folder, removed after the test.</summary>
    internal TestDirectory Workspace { get; } = new();

    public void Dispose() => Workspace.Dispose();

    /// <summary>One product as JSON. Numbers are passed as JSON text, so a test can pass a wrong type such as "\"100\"".</summary>
    protected static string Product(
        string sku = "A-1", string price = "100", string quantity = "1", string category = "phones",
        string image = "a.png", string extra = "") =>
        $$"""
        {"sku":"{{sku}}","name":"Смартфон","category":"{{category}}","brand":"Acme","description":"Описание",
         "attributes":{"Память":"128 ГБ"},"price":{{price}},"quantity":{{quantity}},"image":"{{image}}"{{extra}}}
        """;

    /// <summary>Writes a product file (with the image "a.png" next to it) and returns its path.</summary>
    protected string WriteCatalog(string products = "", string categories = PhonesCategory)
    {
        Workspace.AddImage("a.png");
        return Workspace.WriteCatalog($$"""{"categories":{{categories}},"products":[{{products}}]}""");
    }

    /// <summary>Writes a file that is not a valid product file, to test how the loader refuses it.</summary>
    protected string WriteRawCatalog(string text) => Workspace.WriteCatalog(text);

    internal ValidationResult Validate(string products, string categories = PhonesCategory) =>
        ProductFileValidator.ValidateFile(WriteCatalog(products, categories), Workspace.Images);

    protected static async Task<CliRun> RunCliAsync(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = await Cli.RunAsync(args, output, error);
        return new CliRun(exit, output.ToString(), error.ToString());
    }

    /// <summary>Runs the loader on a file against an address where no shop listens (port 9), without waiting for it.</summary>
    protected static Task<CliRun> LoadWithoutAShopAsync(string file) =>
        RunCliAsync("--file", file, "--url", "http://127.0.0.1:9", "--api-key", "k", "--wait-seconds", "0");
}
