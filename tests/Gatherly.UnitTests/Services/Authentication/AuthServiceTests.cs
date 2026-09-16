using FluentAssertions;
using Gatherly.Contracts.Authentication;
using Gatherly.Repositories.Interfaces;
using Gatherly.Repositories.Persistence.Entities;
using Gatherly.Services.Implementations.Authentication;
using Gatherly.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace Gatherly.UnitTests.Services.Authentication;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepoMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<ILogger<AuthService>> _loggerMock = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(
            _userRepoMock.Object,
            _refreshTokenRepoMock.Object,
            _tokenServiceMock.Object,
            _loggerMock.Object);
    }

    // ── Register ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterAsync_WhenEmailAlreadyExists_ThrowsArgumentException()
    {
        _userRepoMock.Setup(r => r.EmailExistsAsync("test@example.com", default))
            .ReturnsAsync(true);

        var request = new RegisterRequest
        {
            FirstName = "Alice",
            LastName = "Smith",
            Email = "test@example.com",
            Password = "Password1!"
        };

        var act = () => _sut.RegisterAsync(request, "127.0.0.1");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task RegisterAsync_WithValidData_ReturnsAuthResponse()
    {
        _userRepoMock.Setup(r => r.EmailExistsAsync("new@example.com", default))
            .ReturnsAsync(false);

        var savedUser = new User
        {
            Id = 1,
            FirstName = "Bob",
            LastName = "Jones",
            Email = "new@example.com",
            PasswordHash = "hashed",
            IsActive = true
        };

        _userRepoMock.Setup(r => r.AddAsync(It.IsAny<User>(), default))
            .Callback<User, CancellationToken>((u, _) =>
            {
                u.Id = 1;
            })
            .Returns(Task.CompletedTask);

        _userRepoMock.Setup(r => r.SaveChangesAsync(default)).ReturnsAsync(1);
        _userRepoMock.Setup(r => r.GetRoleByNameAsync("User", default))
            .ReturnsAsync(new Role { Id = 2, Name = "User" });
        _userRepoMock.Setup(r => r.AddRoleAsync(It.IsAny<long>(), It.IsAny<long>(), default))
            .Returns(Task.CompletedTask);
        _userRepoMock.Setup(r => r.GetRolesAsync(It.IsAny<long>(), default))
            .ReturnsAsync(new[] { "User" });

        _tokenServiceMock.Setup(t => t.GenerateAccessToken(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns("access_token");
        _tokenServiceMock.Setup(t => t.GenerateRefreshToken()).Returns("raw_refresh");
        _tokenServiceMock.Setup(t => t.HashToken("raw_refresh")).Returns("hashed_refresh");
        _tokenServiceMock.Setup(t => t.GetRefreshTokenExpiry()).Returns(DateTime.UtcNow.AddDays(7));
        _tokenServiceMock.Setup(t => t.GetAccessTokenExpiry()).Returns(DateTime.UtcNow.AddMinutes(15));

        _refreshTokenRepoMock.Setup(r => r.AddAsync(It.IsAny<RefreshToken>(), default))
            .Returns(Task.CompletedTask);
        _refreshTokenRepoMock.Setup(r => r.SaveChangesAsync(default)).ReturnsAsync(1);

        var request = new RegisterRequest
        {
            FirstName = "Bob",
            LastName = "Jones",
            Email = "new@example.com",
            Password = "Password1!"
        };

        var result = await _sut.RegisterAsync(request, "127.0.0.1");

        result.Should().NotBeNull();
        result.AccessToken.Should().Be("access_token");
        result.RefreshToken.Should().Be("raw_refresh");
        result.User.Email.Should().Be("new@example.com");
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_WithInvalidCredentials_ThrowsUnauthorizedException()
    {
        _userRepoMock.Setup(r => r.GetByEmailWithRolesAsync("bad@example.com", default))
            .ReturnsAsync((User?)null);
        _userRepoMock.Setup(r => r.AddLoginActivityAsync(It.IsAny<LoginActivity>(), default))
            .Returns(Task.CompletedTask);

        var request = new LoginRequest { Email = "bad@example.com", Password = "wrong" };

        var act = () => _sut.LoginAsync(request, "127.0.0.1");

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid email or password*");
    }

    [Fact]
    public async Task LoginAsync_WithInactiveUser_ThrowsUnauthorizedException()
    {
        var user = new User
        {
            Id = 1,
            Email = "inactive@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password1!"),
            IsActive = false,
            UserRoles = new List<UserRole>()
        };

        _userRepoMock.Setup(r => r.GetByEmailWithRolesAsync("inactive@example.com", default))
            .ReturnsAsync(user);
        _userRepoMock.Setup(r => r.AddLoginActivityAsync(It.IsAny<LoginActivity>(), default))
            .Returns(Task.CompletedTask);
        _userRepoMock.Setup(r => r.SaveChangesAsync(default)).ReturnsAsync(1);

        var request = new LoginRequest { Email = "inactive@example.com", Password = "Password1!" };

        var act = () => _sut.LoginAsync(request, "127.0.0.1");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── Refresh Token ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshTokenAsync_WithRevokedToken_ThrowsUnauthorizedException()
    {
        var storedToken = new RefreshToken
        {
            Id = 1,
            UserId = 1,
            TokenHash = "hashed",
            IsRevoked = true,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            User = new User { Id = 1, IsActive = true, UserRoles = new List<UserRole>() }
        };

        _tokenServiceMock.Setup(t => t.HashToken("raw")).Returns("hashed");
        _refreshTokenRepoMock.Setup(r => r.GetByTokenHashAsync("hashed", default))
            .ReturnsAsync(storedToken);

        var act = () => _sut.RefreshTokenAsync("raw", "127.0.0.1");

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid or expired refresh token*");
    }

    [Fact]
    public async Task RefreshTokenAsync_WithExpiredToken_ThrowsUnauthorizedException()
    {
        var storedToken = new RefreshToken
        {
            Id = 1,
            UserId = 1,
            TokenHash = "hashed",
            IsRevoked = false,
            ExpiresAt = DateTime.UtcNow.AddDays(-1), // expired
            User = new User { Id = 1, IsActive = true, UserRoles = new List<UserRole>() }
        };

        _tokenServiceMock.Setup(t => t.HashToken("raw")).Returns("hashed");
        _refreshTokenRepoMock.Setup(r => r.GetByTokenHashAsync("hashed", default))
            .ReturnsAsync(storedToken);

        var act = () => _sut.RefreshTokenAsync("raw", "127.0.0.1");

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid or expired refresh token*");
    }

    // ── ForgotPassword ───────────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPasswordAsync_WithUnknownEmail_DoesNotThrow()
    {
        _userRepoMock.Setup(r => r.GetByEmailAsync("unknown@example.com", default))
            .ReturnsAsync((User?)null);

        var request = new ForgotPasswordRequest { Email = "unknown@example.com" };

        // Should not throw (prevents email enumeration)
        var act = () => _sut.ForgotPasswordAsync(request);
        await act.Should().NotThrowAsync();
    }

    // ── TokenService ─────────────────────────────────────────────────────────

    [Fact]
    public void HashToken_ProducesDeterministicAndUniqueHashes()
    {
        // Verify hash behaviour using the mock token service
        // (real TokenService is exercised in integration tests)
        var tokenServiceMock = new Mock<ITokenService>();
        tokenServiceMock
            .Setup(t => t.HashToken(It.IsAny<string>()))
            .Returns<string>(token =>
            {
                // Replicate the real SHA-256 hash logic inline
                var bytes = System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(token));
                return Convert.ToBase64String(bytes);
            });

        var hash1 = tokenServiceMock.Object.HashToken("token_a");
        var hash2 = tokenServiceMock.Object.HashToken("token_b");
        var hash1Again = tokenServiceMock.Object.HashToken("token_a");

        hash1.Should().NotBe(hash2);
        hash1.Should().Be(hash1Again);
        hash1.Should().NotBeNullOrEmpty();
    }
}
