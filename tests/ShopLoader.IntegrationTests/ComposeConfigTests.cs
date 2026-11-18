using System.Diagnostics;
using System.Text.Json.Nodes;

namespace ShopLoader.IntegrationTests;

/// <summary>User Story 4: the development override publishes the ports, the default configuration does not publish the database.</summary>
public sealed class ComposeConfigTests
{
    private static JsonNode Config(params string[] files)
    {
        var arguments = string.Join(' ', files.Select(f => $"-f {f}")) + " config --format json";
        var start = new ProcessStartInfo("docker", "compose " + arguments)
        {
            WorkingDirectory = Repository.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment["SHOP_PORT"] = "18080";
        start.Environment["SHOP_DB_PORT"] = "13306";

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error);
        return JsonNode.Parse(output)!;
    }

    private static IEnumerable<JsonNode> Ports(JsonNode config, string service) =>
        config["services"]![service]!["ports"]?.AsArray().Select(p => p!) ?? [];

    private static IEnumerable<string> PublishedPorts(JsonNode config, string service) =>
        Ports(config, service).Select(p => p["published"]!.GetValue<string>());

    [Fact]
    public void DefaultConfigurationPublishesTheShopButNotTheDatabase()
    {
        var config = Config("docker-compose.yml");

        Assert.Equal(["18080"], PublishedPorts(config, "shop"));
        Assert.Empty(PublishedPorts(config, "db"));
    }

    [Fact]
    public void DevelopmentOverridePublishesTheShopAndTheDatabase()
    {
        var config = Config("docker-compose.yml", "docker-compose.dev.yml");

        Assert.Equal(["18080"], PublishedPorts(config, "shop"));
        Assert.Equal(["13306"], PublishedPorts(config, "db"));
    }

    [Fact]
    public void PublishedPortsAreReachableFromThisMachineOnly()
    {
        // The demo ships default credentials, so it must not be offered to the network.
        var config = Config("docker-compose.yml", "docker-compose.dev.yml");

        var ports = new[] { "shop", "db" }.SelectMany(service => Ports(config, service)).ToList();
        Assert.NotEmpty(ports);
        Assert.All(ports, port => Assert.Equal("127.0.0.1", port["host_ip"]!.GetValue<string>()));
    }

    [Fact]
    public void DefaultConfigurationRunsTheLoaderAfterTheShopIsHealthy()
    {
        var config = Config("docker-compose.yml");

        var loader = config["services"]!["loader"]!;
        Assert.Equal("service_healthy", loader["depends_on"]!["shop"]!["condition"]!.GetValue<string>());
        Assert.Equal("no", loader["restart"]!.GetValue<string>());
    }
}
