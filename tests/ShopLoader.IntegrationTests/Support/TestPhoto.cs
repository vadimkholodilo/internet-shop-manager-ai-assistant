namespace ShopLoader.IntegrationTests;

internal static class TestPhoto
{
    /// <summary>A small JPEG kept with the tests, so loader tests do not depend on the demo catalog's photos.</summary>
    public static string Path { get; } = System.IO.Path.Combine(Repository.Root, "tests", "ShopLoader.IntegrationTests", "Support", "photo.jpg");
}
