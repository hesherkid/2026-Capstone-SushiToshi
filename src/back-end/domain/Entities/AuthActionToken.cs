using System.ComponentModel.DataAnnotations.Schema;

namespace back_end.domain.Entities;

[Table("auth_action_tokens")]
public class AuthActionToken
{
    public long Id { get; set; }

    public int UserId { get; set; }

    public string TokenHash { get; set; } = "";

    public AuthTokenPurpose Purpose { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }
        = DateTime.UtcNow;

    public User User { get; set; } = null!;
}

public enum AuthTokenPurpose
{
    EmailVerification = 1,
    PasswordReset = 2
}