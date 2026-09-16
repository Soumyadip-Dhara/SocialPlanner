namespace Gatherly.Services.Interfaces;

public interface ITokenService
{
    /// <summary>
    /// Generates a JWT access token. The caller must supply a pre-computed <paramref name="expiry"/>
    /// obtained from <see cref="GetAccessTokenExpiry"/> so the expiry embedded in the JWT claim
    /// and the expiry returned to the client are identical.
    /// </summary>
    string GenerateAccessToken(long userId, string email, IEnumerable<string> roles, DateTime expiry);
    string GenerateRefreshToken();
    string HashToken(string token);
    DateTime GetAccessTokenExpiry();
    DateTime GetRefreshTokenExpiry();
}
