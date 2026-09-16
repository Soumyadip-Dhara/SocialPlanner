namespace Gatherly.Repositories.Persistence.Entities;

public class LoginActivity : BaseEntity
{
    public long UserId { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public bool Success { get; set; }
    public string? FailureReason { get; set; }

    public User User { get; set; } = null!;
}
