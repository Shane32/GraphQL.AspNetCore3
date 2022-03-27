using Microsoft.AspNetCore.TestHost;

namespace Tests;

internal static class TestServerExtensions
{
    public static async Task<string> ExecuteGet(this TestServer server, string url)
    {
        var client = server.CreateClient();
        using var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var str = await response.Content.ReadAsStringAsync();
        return str;
    }

    public static async Task<string> ExecutePost(this TestServer server, string url, string query, object? variables = null)
    {
        var client = server.CreateClient();
        var data = System.Text.Json.JsonSerializer.Serialize(new { query = query, variables = variables });
        var content = new StringContent(data, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(url, content);
        response.EnsureSuccessStatusCode();
        var str = await response.Content.ReadAsStringAsync();
        return str;
    }
}
