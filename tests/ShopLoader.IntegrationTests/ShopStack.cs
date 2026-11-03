using System.Net.Http.Headers;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;

namespace ShopLoader.IntegrationTests;

/// <summary>
/// Real OpenCart and MariaDB containers started with Testcontainers, built from the same Dockerfiles and
/// started with the same settings as the demo in docker-compose.yml. Nothing is mocked. The stack is shared
/// by all tests in the "shop" collection, so tests use their own unique SKUs and category slugs and never
/// depend on each other. The loader image (built with Native AOT) is built once and can be run as a container.
/// </summary>
public sealed class ShopStack : IAsyncLifetime
{
    public const string ApiKey = "integration-test-api-key";
    private const string DbPassword = "integration-test-db-password";

    private INetwork? network;
    private IContainer? db;
    private IContainer? shop;
    private IFutureDockerImage? shopImage;

    public IFutureDockerImage LoaderImage { get; private set; } = null!;

    public INetwork Network => network!;

    public Uri ShopUrl { get; private set; } = null!;

    public HttpClient Http { get; private set; } = null!;

    public string RepositoryRoot { get; } = FindRepositoryRoot();

    public async Task InitializeAsync()
    {
        shopImage = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(Path.Combine(RepositoryRoot, "shop"))
            .WithDockerfile("Dockerfile")
            .WithName("shop-it/opencart:test")
            .WithDeleteIfExists(false)
            .Build();

        LoaderImage = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(RepositoryRoot)
            .WithDockerfile("src/ShopLoader/Dockerfile")
            .WithName("shop-it/loader:test")
            .WithDeleteIfExists(false)
            .Build();

        await Task.WhenAll(shopImage.CreateAsync(), LoaderImage.CreateAsync());

        network = new NetworkBuilder().Build();
        await network.CreateAsync();

        db = new ContainerBuilder("mariadb:11.4")
            .WithNetwork(network)
            .WithNetworkAliases("db")
            .WithEnvironment("MARIADB_DATABASE", "opencart")
            .WithEnvironment("MARIADB_USER", "opencart")
            .WithEnvironment("MARIADB_PASSWORD", DbPassword)
            .WithEnvironment("MARIADB_ROOT_PASSWORD", DbPassword)
            .WithCommand("--character-set-server=utf8mb4", "--collation-server=utf8mb4_general_ci")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("healthcheck.sh", "--connect", "--innodb_initialized"))
            .Build();
        await db.StartAsync();

        shop = new ContainerBuilder(shopImage)
            .WithNetwork(network)
            .WithNetworkAliases("shop")
            .WithEnvironment("SHOP_PORT", "80")
            .WithEnvironment("SHOP_DB_PASSWORD", DbPassword)
            .WithEnvironment("SHOP_API_KEY", ApiKey)
            .WithPortBinding(80, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("resuming normal operations"))
            .Build();
        await shop.StartAsync();

        ShopUrl = new Uri($"http://{shop.Hostname}:{shop.GetMappedPublicPort(80)}/");
        Http = new HttpClient { BaseAddress = ShopUrl, Timeout = TimeSpan.FromSeconds(60) };
        Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

        await WaitForStorefrontAsync();
    }

    private async Task WaitForStorefrontAsync()
    {
        var deadline = DateTime.UtcNow.AddMinutes(3);
        while (true)
        {
            try
            {
                using var response = await Http.GetAsync("index.php?route=common/home");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Not accepting connections yet.
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"The shop did not answer at {ShopUrl} within 3 minutes.");
            }
            await Task.Delay(1000);
        }
    }

    public async Task DisposeAsync()
    {
        Http?.Dispose();
        if (shop is not null) { await shop.DisposeAsync(); }
        if (db is not null) { await db.DisposeAsync(); }
        if (network is not null) { await network.DisposeAsync(); }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "InternetShop.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found");
    }
}

[CollectionDefinition("shop")]
public sealed class ShopCollection : ICollectionFixture<ShopStack>;
