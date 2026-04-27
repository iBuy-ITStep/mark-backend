using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using FluentAssertions;
using MarkBackend.Tests.Infrastructure;

namespace MarkBackend.Tests.Diagnostics;

[Collection("Database collection")]
public class JwtDiagnosticTests
{
    private readonly HttpClient _client;

    public JwtDiagnosticTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DiagnoseJwtAuthentication()
    {
        // Step 1: Login
        var loginRequest = new { email = TestDataSeeder.UserEmail, password = TestDataSeeder.TestPassword };
        var loginResponse = await _client.PostAsJsonAsync("/api/account/login", loginRequest);

        Console.WriteLine($"Login Status: {loginResponse.StatusCode}");
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK, "login should succeed");

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Console.WriteLine($"Token received: {loginResult!.Token.Substring(0, Math.Min(50, loginResult.Token.Length))}...");

        // Step 2: Try to access authenticated endpoint
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult.Token);
        Console.WriteLine($"Authorization header set: {_client.DefaultRequestHeaders.Authorization}");

        var cartResponse = await _client.GetAsync("/api/cart");
        Console.WriteLine($"Cart endpoint status: {cartResponse.StatusCode}");

        if (cartResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            var content = await cartResponse.Content.ReadAsStringAsync();
            Console.WriteLine($"Unauthorized response body: {content}");
        }

        cartResponse.StatusCode.Should().Be(HttpStatusCode.OK, "authenticated request should succeed");
    }

    private class LoginResponse
    {
        public string Token { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
    }
}
