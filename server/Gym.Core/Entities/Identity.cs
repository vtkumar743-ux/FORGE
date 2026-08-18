using Microsoft.AspNetCore.Identity;

namespace Gym.Core.Entities;

/// <summary>
/// Single identity across every branch (multi-branch essential: one member, many branches).
/// Role is carried by Identity roles; branch scope by <see cref="HomeBranchId"/> plus a
/// branch claim, so BranchManager/Trainer roles drop in later without a schema change.
/// </summary>
public class ApplicationUser : IdentityUser<int>
{
    public string FullName { get; set; } = string.Empty;
    public int? HomeBranchId { get; set; }
    public Branch? HomeBranch { get; set; }
    public string? AvatarUrl { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAtUtc { get; set; }
    public bool IsActive { get; set; } = true;

    public Member? Member { get; set; }
    public Trainer? Trainer { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

public class ApplicationRole : IdentityRole<int>
{
    public string? Description { get; set; }
}

/// <summary>Rotating refresh token — one row per issued token, revoked on use.</summary>
public class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    /// <summary>SHA-256 of the token; the raw value never touches the database.</summary>
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? CreatedByIp { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedByIp { get; set; }
    /// <summary>Token that superseded this one — gives a full rotation audit chain.</summary>
    public string? ReplacedByTokenHash { get; set; }
    public string? ReasonRevoked { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
    public bool IsActive => RevokedAtUtc is null && !IsExpired;
}
