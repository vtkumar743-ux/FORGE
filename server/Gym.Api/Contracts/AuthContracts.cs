using System.ComponentModel.DataAnnotations;

namespace Gym.Api.Contracts;

/// <summary>
/// Member self-registration. Phone is captured in an OTP-ready shape (E.164) even though v1
/// verifies with a password, so switching to OTP login later is an endpoint change only.
/// </summary>
public record RegisterRequest
{
    [Required, StringLength(160, MinimumLength = 2)]
    public string FullName { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, RegularExpression(@"^\+?[0-9]{10,15}$", ErrorMessage = "Enter a valid mobile number.")]
    public string Phone { get; init; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 10)]
    public string Password { get; init; } = string.Empty;

    /// <summary>Home branch. Falls back to the first active branch when omitted.</summary>
    public int? HomeBranchId { get; init; }

    public string? PrimaryGoal { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public bool ConsentMarketing { get; init; } = true;
}

public record LoginRequest
{
    /// <summary>Email or registered mobile number.</summary>
    [Required, StringLength(256)]
    public string Identifier { get; init; } = string.Empty;

    [Required, StringLength(128)]
    public string Password { get; init; } = string.Empty;
}

public record ChangePasswordRequest
{
    [Required] public string CurrentPassword { get; init; } = string.Empty;
    [Required, StringLength(128, MinimumLength = 10)] public string NewPassword { get; init; } = string.Empty;
}

public record AuthResponse
{
    public required string AccessToken { get; init; }
    public required DateTime AccessTokenExpiresAtUtc { get; init; }
    public required CurrentUserResponse User { get; init; }
}

public record CurrentUserResponse
{
    public required int Id { get; init; }
    public required string FullName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public required string[] Roles { get; init; }
    public int? MemberId { get; init; }
    public string? MemberCode { get; init; }
    public int? HomeBranchId { get; init; }
    public string? HomeBranchName { get; init; }
    public string? AvatarUrl { get; init; }
    /// <summary>True for the seeded owner until the first password change (04 §4).</summary>
    public bool MustChangePassword { get; init; }
}
