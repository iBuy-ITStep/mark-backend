using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MarkBackend.Tests.Helpers;
using MarkBackend.Tests.Infrastructure;

namespace MarkBackend.Tests.Controllers;

public class AccountControllerTests : IntegrationTestBase
{
    public AccountControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokens()
    {
        // Arrange
        var loginRequest = new
        {
            email = TestDataSeeder.UserEmail,
            password = TestDataSeeder.TestPassword
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/account/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        result.Should().NotBeNull();
        result!.Token.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new
        {
            email = TestDataSeeder.UserEmail,
            password = "WrongPassword123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/account/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(Skip = "Test isolation issue - EmailHelper requires real SMTP server. Passes individually but fails in suite.")]
    public async Task Register_WithValidData_ReturnsSuccess()
    {
        // Arrange - Use unique email to avoid conflicts
        var password = "NewUser123!";
        var registerRequest = new
        {
            email = $"newuser{Guid.NewGuid().ToString().Substring(0, 8)}@test.com",
            password = password,
            passwordConfirm = password,
            clientUri = "http://localhost/confirm"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/account/register", registerRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange
        var password = "Test123!";
        var registerRequest = new
        {
            email = TestDataSeeder.UserEmail, // Already exists
            password = password,
            passwordConfirm = password,
            clientUri = "http://localhost/confirm"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/account/register", registerRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Logout_WhenAuthenticated_ReturnsSuccess()
    {
        // Arrange
        await Client.AuthenticateAsAsync(TestDataSeeder.UserEmail, TestDataSeeder.TestPassword);

        // Act
        var response = await Client.PostAsync("/api/account/logout", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.PostAsync("/api/account/logout", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private class LoginResponse
    {
        public string Token { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public DateTime Expiration { get; set; }
    }
}
