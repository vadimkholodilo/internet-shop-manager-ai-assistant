using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ShopLoader.IntegrationTests;

/// <summary>Reads the shop through the catalog API extension, so tests check results without the database.</summary>
public static class ShopClient
{
    public static async Task<JsonNode?> GetJsonAsync(this HttpClient http, string path)
    {
        using var response = await http.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"GET {path} -> {(int)response.StatusCode}: {body}");
        return JsonNode.Parse(body);
    }

    public static async Task<HttpStatusCode> StatusAsync(this HttpClient http, string path)
    {
        using var response = await http.GetAsync(path);
        return response.StatusCode;
    }

    public static async Task<JsonNode?> ProductAsync(this HttpClient http, string sku) =>
        await http.GetJsonAsync($"catalog-api/products/{sku}");

    public static async Task<int> ProductCountAsync(this HttpClient http) =>
        (await http.GetJsonAsync("catalog-api/products"))!["total"]!.GetValue<int>();

    public static async Task<int> CategoryCountAsync(this HttpClient http) =>
        (await http.GetJsonAsync("catalog-api/categories"))!["total"]!.GetValue<int>();

    public static async Task<(HttpStatusCode Status, JsonNode? Body)> SendJsonAsync(
        this HttpClient http, HttpMethod method, string path, object body)
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json"),
        };
        using var response = await http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, text.Length == 0 ? null : JsonNode.Parse(text));
    }
}
