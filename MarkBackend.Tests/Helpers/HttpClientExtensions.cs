using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MarkBackend.Tests.Helpers;

public static class HttpClientExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Authenticates the client by logging in and setting the Bearer token
    /// </summary>
    public static async Task<string> AuthenticateAsAsync(this HttpClient client, string email, string password)
    {
        var loginRequest = new
        {
            email = email,
            password = password
        };

        var response = await client.PostAsJsonAsync("/api/account/login", loginRequest);
        response.EnsureSuccessStatusCode();

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        var token = loginResponse!.Token;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return token;
    }

    /// <summary>
    /// POST request with JSON body
    /// </summary>
    public static async Task<HttpResponseMessage> PostJsonAsync<T>(
        this HttpClient client,
        string url,
        T content)
    {
        var json = JsonSerializer.Serialize(content);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PostAsync(url, httpContent);
    }

    /// <summary>
    /// PUT request with JSON body
    /// </summary>
    public static async Task<HttpResponseMessage> PutJsonAsync<T>(
        this HttpClient client,
        string url,
        T content)
    {
        var json = JsonSerializer.Serialize(content);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PutAsync(url, httpContent);
    }

    /// <summary>
    /// GET request and deserialize JSON response
    /// </summary>
    public static async Task<T?> GetJsonAsync<T>(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    private class LoginResponse
    {
        public string Token { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public DateTime Expiration { get; set; }
    }
}
