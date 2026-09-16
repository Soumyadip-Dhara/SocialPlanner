using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Gatherly.Contracts.Authentication;
using Gatherly.Contracts.Common;

namespace Gatherly.IntegrationTests.Auth;

public class AuthControllerTests : IClassFixture<GatherlyWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthControllerTests(GatherlyWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidData_Returns201AndTokens()
    {
        var request = new RegisterRequest
        {
            FirstName = "Integration",
            LastName = "User",
            Email = $"integration_{Guid.NewGuid()}@test.com",
            Password = "Password1!@"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data!.AccessToken.Should().NotBeNullOrEmpty();
        body.Data.RefreshToken.Should().NotBeNullOrEmpty();
        body.Data.User.Email.Should().Be(request.Email.ToLowerInvariant());
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409()
    {
        var email = $"dup_{Guid.NewGuid()}@test.com";
        var request = new RegisterRequest
        {
            FirstName = "Dup",
            LastName = "User",
            Email = email,
            Password = "Password1!@"
        };

        await _client.PostAsJsonAsync("/api/v1/auth/register", request);
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200AndTokens()
    {
        var email = $"login_{Guid.NewGuid()}@test.com";
        var password = "Password1!@";

        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
        {
            FirstName = "Login",
            LastName = "User",
            Email = email,
            Password = password
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest { Email = email, Password = password });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        body!.Success.Should().BeTrue();
        body.Data!.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest { Email = "nobody@test.com", Password = "WrongPass!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_WithValidToken_Returns200AndProfile()
    {
        var email = $"me_{Guid.NewGuid()}@test.com";

        var regResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
        {
            FirstName = "Profile",
            LastName = "User",
            Email = email,
            Password = "Password1!@"
        });

        var regBody = await regResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        var accessToken = regBody!.Data!.AccessToken;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<UserDto>>();
        body!.Success.Should().BeTrue();
        body.Data!.Email.Should().Be(email.ToLowerInvariant());
    }

    [Fact]
    public async Task HealthCheck_Returns200()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
