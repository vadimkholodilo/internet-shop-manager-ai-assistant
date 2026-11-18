using System.CommandLine;
using ShopClient;
using ShopLoader.Validation;

namespace ShopLoader;

/// <summary>Command line entry point. Exit codes: 0 loaded, 1 some records failed, 2 file or usage problem or shop unavailable.</summary>
internal static class Cli
{
    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken = default)
    {
        var file = new Option<string>("--file") { Description = "product file in the documented JSON format", Required = true };
        var url = new Option<Uri>("--url")
        {
            Description = "address of the shop, e.g. http://localhost:8080",
            Required = true,
            CustomParser = ParseHttpUrl,
        };
        var apiKey = new Option<string?>("--api-key")
        {
            Description = "API key of the shop (default: SHOP_API_KEY environment variable)",
            DefaultValueFactory = _ => Environment.GetEnvironmentVariable("SHOP_API_KEY"),
        };
        var shop = new Option<ShopType>("--shop")
        {
            Description = $"kind of shop to load into: {string.Join(", ", ShopType.SupportedNames)}",
            DefaultValueFactory = _ => ShopType.Default,
            CustomParser = ParseShopType,
        };
        var imagesDirectory = new Option<string?>("--images-dir")
        {
            Description = "directory the product image paths are relative to (default: \"images\" next to the file)",
        };
        var waitSeconds = new Option<int>("--wait-seconds")
        {
            Description = "how long to wait for the shop to become ready",
            DefaultValueFactory = _ => 300,
            CustomParser = ParseSeconds,
        };

        var root = new RootCommand("Loads a product file into the shop through its catalog API.")
        {
            file, url, apiKey, shop, imagesDirectory, waitSeconds,
        };
        const string MissingApiKey = "--api-key or SHOP_API_KEY is required";
        root.SetAction((parsed, token) =>
        {
            var key = parsed.GetValue(apiKey);
            if (string.IsNullOrEmpty(key))
            {
                error.WriteLine(MissingApiKey);
                return Task.FromResult(2);
            }

            return LoadAsync(
                new Options(parsed.GetRequiredValue(file), parsed.GetRequiredValue(url), key, parsed.GetRequiredValue(shop), parsed.GetValue(imagesDirectory), parsed.GetValue(waitSeconds)),
                output, error, token);
        });

        var parseResult = root.Parse(args);
        if (parseResult.Errors.Count > 0)
        {
            foreach (var problem in parseResult.Errors)
            {
                error.WriteLine(problem.Message);
            }
            if (string.IsNullOrEmpty(parseResult.GetValue(apiKey)))
            {
                error.WriteLine(MissingApiKey);
            }
            error.WriteLine("Run with --help for usage.");
            return 2;
        }

        return await parseResult.InvokeAsync(new InvocationConfiguration { Output = output, Error = error }, cancellationToken);
    }

    private sealed record Options(string File, Uri Url, string ApiKey, ShopType Shop, string? ImagesDirectory, int WaitSeconds);

    private static Uri? ParseHttpUrl(System.CommandLine.Parsing.ArgumentResult result)
    {
        var text = result.Tokens.Count == 1 ? result.Tokens[0].Value : null;
        if (text is not null && Uri.TryCreate(text, UriKind.Absolute, out var url) &&
            (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps))
        {
            return url;
        }

        result.AddError("--url is required and must be an http(s) address");
        return null;
    }

    private static ShopType ParseShopType(System.CommandLine.Parsing.ArgumentResult result)
    {
        var text = result.Tokens.Count == 1 ? result.Tokens[0].Value : null;
        if (ShopType.TryParse(text, out var type))
        {
            return type;
        }

        result.AddError($"--shop must be one of: {string.Join(", ", ShopType.SupportedNames)}");
        return ShopType.Default;
    }

    private static int ParseSeconds(System.CommandLine.Parsing.ArgumentResult result)
    {
        if (result.Tokens.Count == 1 && int.TryParse(result.Tokens[0].Value, out var seconds) && seconds >= 0)
        {
            return seconds;
        }

        result.AddError("--wait-seconds must be a number of seconds, 0 or more");
        return 0;
    }

    private static async Task<int> LoadAsync(Options options, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
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
            using var shop = ShopClientFactory.CreateCatalogLoadingClient(options.Shop, new ShopConnection(options.Url, options.ApiKey));
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
}
