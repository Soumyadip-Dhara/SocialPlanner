using Gatherly.Contracts.Authentication;
using Gatherly.Repositories.Interfaces;
using Gatherly.Repositories.Persistence.Entities;
using Gatherly.Services.Interfaces;
using Gatherly.Services.Interfaces.Authentication;
using Microsoft.Extensions.Logging;

namespace Gatherly.Services.Implementations.Authentication;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthService> _logger;

    private const string DefaultUserRole = "User";

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (await _userRepository.EmailExistsAsync(request.Email, cancellationToken))
            throw new ArgumentException("An account with this email address already exists.");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var user = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            PhoneNumber = request.PhoneNumber?.Trim(),
            IsActive = true,
            IsEmailVerified = false
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        var defaultRole = await _userRepository.GetRoleByNameAsync(DefaultUserRole, cancellationToken);
        if (defaultRole is not null)
        {
            await _userRepository.AddRoleAsync(user.Id, defaultRole.Id, cancellationToken);
            await _userRepository.SaveChangesAsync(cancellationToken);
        }

        var roles = await _userRepository.GetRolesAsync(user.Id, cancellationToken);
        var accessToken = _tokenService.GenerateAccessToken(user.Id, user.Email, roles);
        var rawRefreshToken = _tokenService.GenerateRefreshToken();
        var tokenHash = _tokenService.HashToken(rawRefreshToken);

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = _tokenService.GetRefreshTokenExpiry(),
            CreatedByIp = ipAddress
        };

        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} registered successfully", user.Id);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            AccessTokenExpiry = _tokenService.GetAccessTokenExpiry(),
            User = MapToUserDto(user, roles)
        };
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailWithRolesAsync(request.Email, cancellationToken);

        var success = user is not null
            && user.IsActive
            && BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

        await _userRepository.AddLoginActivityAsync(new LoginActivity
        {
            UserId = user?.Id ?? 0,
            IpAddress = ipAddress,
            Success = success,
            FailureReason = success ? null : "Invalid credentials"
        }, cancellationToken);

        if (!success)
        {
            if (user is not null)
                await _userRepository.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("Failed login attempt for email {Email} from {IpAddress}", request.Email, ipAddress);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        await _userRepository.SaveChangesAsync(cancellationToken);

        var roles = user!.UserRoles.Select(ur => ur.Role.Name).ToList();
        var accessToken = _tokenService.GenerateAccessToken(user.Id, user.Email, roles);
        var rawRefreshToken = _tokenService.GenerateRefreshToken();
        var tokenHash = _tokenService.HashToken(rawRefreshToken);

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = _tokenService.GetRefreshTokenExpiry(),
            CreatedByIp = ipAddress
        };

        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} logged in successfully", user.Id);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            AccessTokenExpiry = _tokenService.GetAccessTokenExpiry(),
            User = MapToUserDto(user, roles)
        };
    }

    public async Task<AuthResponse> RefreshTokenAsync(
        string refreshToken,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = _tokenService.HashToken(refreshToken);
        var storedToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (storedToken is null || storedToken.IsRevoked || storedToken.ExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        var user = storedToken.User;
        if (!user.IsActive)
            throw new UnauthorizedAccessException("User account is inactive.");

        // Rotate: revoke old token, issue new tokens
        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow;

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var newAccessToken = _tokenService.GenerateAccessToken(user.Id, user.Email, roles);
        var newRawRefreshToken = _tokenService.GenerateRefreshToken();
        var newTokenHash = _tokenService.HashToken(newRawRefreshToken);

        storedToken.ReplacedByToken = newTokenHash;

        var newRefreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = newTokenHash,
            ExpiresAt = _tokenService.GetRefreshTokenExpiry(),
            CreatedByIp = ipAddress
        };

        await _refreshTokenRepository.UpdateAsync(storedToken, cancellationToken);
        await _refreshTokenRepository.AddAsync(newRefreshToken, cancellationToken);
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return new AuthResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRawRefreshToken,
            AccessTokenExpiry = _tokenService.GetAccessTokenExpiry(),
            User = MapToUserDto(user, roles)
        };
    }

    public async Task LogoutAsync(
        string refreshToken,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = _tokenService.HashToken(refreshToken);
        var storedToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (storedToken is not null && !storedToken.IsRevoked)
        {
            storedToken.IsRevoked = true;
            storedToken.RevokedAt = DateTime.UtcNow;
            await _refreshTokenRepository.UpdateAsync(storedToken, cancellationToken);
            await _refreshTokenRepository.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        // Always return success to prevent email enumeration
        if (user is null || !user.IsActive)
        {
            _logger.LogInformation("Password reset requested for unknown email: {Email}", request.Email);
            return;
        }

        var token = _tokenService.GenerateRefreshToken(); // reuse cryptographic random generation
        user.PasswordResetToken = token;
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        // TODO: send email with reset link containing token
        _logger.LogInformation("Password reset token generated for user {UserId}", user.Id);
    }

    public async Task ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByResetTokenAsync(request.Token, cancellationToken);

        if (user is null || user.Email != request.Email.ToLowerInvariant())
            throw new ArgumentException("Invalid or expired password reset token.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _refreshTokenRepository.RevokeAllUserTokensAsync(user.Id, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password reset completed for user {UserId}", user.Id);
    }

    public async Task ChangePasswordAsync(
        long userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new KeyNotFoundException("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            throw new ArgumentException("Current password is incorrect.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _userRepository.UpdateAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password changed for user {UserId}", userId);
    }

    public async Task<UserDto> GetProfileAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new KeyNotFoundException("User not found.");
        var roles = await _userRepository.GetRolesAsync(userId, cancellationToken);
        return MapToUserDto(user, roles);
    }

    private static UserDto MapToUserDto(User user, IEnumerable<string> roles) => new()
    {
        Id = user.Id,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Email = user.Email,
        PhoneNumber = user.PhoneNumber,
        Roles = roles,
        CreatedAt = user.CreatedAt
    };
}
