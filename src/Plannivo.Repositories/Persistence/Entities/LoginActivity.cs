namespace Plannivo.Repositories.Persistence.Entities;

public class LoginActivity : BaseEntity
{
    /// <summary>
    /// Nullable — login attempts for email addresses that don't correspond to any user
    /// must still be recorded for audit purposes without violating the FK constraint.
    /// </summary>
    public long? UserId { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public bool Success { get; set; }
    public string? FailureReason { get; set; }

    public User? User { get; set; }
}
