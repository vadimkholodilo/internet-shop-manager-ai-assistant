using ShopLoader.OpenCart;
using ShopLoader.Validation;

namespace ShopLoader;

/// <summary>Command line entry point. Exit codes: 0 loaded, 1 some records failed, 2 file or usage problem or shop unavailable.</summary>
internal static class Cli
{
    private const string Usage = """
        Usage: shop-loader --file <catalog.json> --url <shop url> [--api-key <key>] [--images-dir <dir>] [--wait-seconds <n>]

          --file          product file in the documented JSON format
          --url           address of the shop, e.g. http://localhost:8080
          --api-key       API key of the shop (default: SHOP_API_KEY environment variable)
          --images-dir    directory the product image paths are relative to (default: "images" next to the file)
          --wait-seconds  how long to wait for the shop to become ready (default: 300)
        """;

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken = default)
    {
        if (!TryParse(args, out var options, out var problem))
        {
            error.WriteLine(problem);
            error.WriteLine(Usage);
            return 2;
        }

        var imagesDirectory = options.ImagesDirectory ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(options.File))!, "images");

        ValidationResult validation;
        try
        {
            validation = ProductFileValidator.ValidateFile(options.File, imagesDirectory);
        }
        catch (ProductFileException e)
        {
            error.WriteLine(e.Message);
            return 2;
        }

        try
        {
            using var shop = new OpenCartCatalogClient(options.Url, options.ApiKey);
            await shop.WaitUntilReadyAsync(TimeSpan.FromSeconds(options.WaitSeconds), cancellationToken);

            var report = await new Loader(shop, output).LoadAsync(validation, imagesDirectory, cancellationToken);
            output.WriteLine(report.Summary());
            return report.Succeeded ? 0 : 1;
        }
        catch (ShopUnavailableException e)
        {
            error.WriteLine(e.Message);
            return 2;
        }
    }

    private sealed record Options(string File, Uri Url, string ApiKey, string? ImagesDirectory, int WaitSeconds);

    private static bool TryParse(string[] args, out Options options, out string problem)
    {
        options = null!;
        problem = "";
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
            {
                problem = $"Unexpected argument: {args[i]}";
                return false;
            }
            values[args[i][2..]] = args[++i];
        }

        foreach (var name in values.Keys)
        {
            if (name is not ("file" or "url" or "api-key" or "images-dir" or "wait-seconds"))
            {
                problem = $"Unknown option: --{name}";
                return false;
            }
        }

        if (!values.TryGetValue("file", out var file)) { problem = "--file is required"; return false; }
        if (!values.TryGetValue("url", out var url) || !Uri.TryCreate(url, UriKind.Absolute, out var shopUrl) ||
            (shopUrl.Scheme != Uri.UriSchemeHttp && shopUrl.Scheme != Uri.UriSchemeHttps))
        {
            problem = "--url is required and must be an http(s) address";
            return false;
        }

        var apiKey = values.GetValueOrDefault("api-key") ?? Environment.GetEnvironmentVariable("SHOP_API_KEY");
        if (string.IsNullOrEmpty(apiKey)) { problem = "--api-key or SHOP_API_KEY is required"; return false; }

        var wait = 300;
        if (values.TryGetValue("wait-seconds", out var waitText) && (!int.TryParse(waitText, out wait) || wait < 0))
        {
            problem = "--wait-seconds must be a number of seconds, 0 or more";
            return false;
        }

        options = new Options(file, shopUrl, apiKey, values.GetValueOrDefault("images-dir"), wait);
        return true;
    }
}
