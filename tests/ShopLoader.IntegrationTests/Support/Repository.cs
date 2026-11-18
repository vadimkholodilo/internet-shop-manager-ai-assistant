namespace ShopLoader.IntegrationTests;

internal static class Repository
{
    /// <summary>The directory that holds InternetShop.slnx, found by walking up from the test binaries.</summary>
    public static string Root { get; } = Find();

    private static string Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "InternetShop.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found");
    }
}
