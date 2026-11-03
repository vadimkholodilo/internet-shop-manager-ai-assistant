namespace ShopLoader.UnitTests;

/// <summary>A temporary directory with an images folder, removed when disposed.</summary>
internal sealed class TestDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "shop-loader-tests-" + Guid.NewGuid().ToString("N"));

    public string Images => System.IO.Path.Combine(Path, "images");

    public TestDirectory()
    {
        Directory.CreateDirectory(Images);
    }

    public string AddImage(string name)
    {
        File.WriteAllBytes(System.IO.Path.Combine(Images, name), [0x89, 0x50, 0x4E, 0x47]);
        return name;
    }

    public string WriteCatalog(string json)
    {
        var file = System.IO.Path.Combine(Path, "catalog.json");
        File.WriteAllText(file, json);
        return file;
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}

internal static class RepositoryRoot
{
    public static string Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "InternetShop.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found");
    }
}
