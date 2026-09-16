namespace Gatherly.Services.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(long userId, string email, IEnumerable<string> roles);
    string GenerateRefreshToken();
    string HashToken(string token);
    DateTime GetAccessTokenExpiry();
    DateTime GetRefreshTokenExpiry();
}
