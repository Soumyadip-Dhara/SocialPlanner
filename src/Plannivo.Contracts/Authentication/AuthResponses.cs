namespace Plannivo.Contracts.Authentication;

public class AuthResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public DateTime AccessTokenExpiry { get; init; }
    public UserDto User { get; init; } = new();
}

public class UserDto
{
    public long Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public IEnumerable<string> Roles { get; init; } = Enumerable.Empty<string>();
    public DateTime CreatedAt { get; init; }
}
