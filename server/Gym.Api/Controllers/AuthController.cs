using System.Security.Claims;
using Gym.Api.Contracts;
using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    /// <summary>
    /// The refresh token never reaches JavaScript. The client is served through a dev proxy
    /// and from the same origin in production, so SameSite=Lax is both sufficient and safer
    /// than the None+Secure combination cross-origin cookies would need.
    /// </summary>
    private const string RefreshCookieName = "forge_rt";

    private readonly GymDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly ITokenService _tokens;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AuthController> _log;

    public AuthController(
        GymDbContext db,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        ITokenService tokens,
        IWebHostEnvironment env,
        ILogger<AuthController> log)
    {
        _db = db;
        _users = users;
        _signIn = signIn;
        _tokens = tokens;
        _env = env;
        _log = log;
    }

    /// <summary>
    /// Always Secure outside development. In development the API is commonly reached over
    /// plain http through the Vite proxy, where a Secure cookie is silently dropped by
    /// non-browser clients — which would break the silent-refresh flow during local work.
    /// </summary>
    private bool RequireSecureCookie => !_env.IsDevelopment() || Request.IsHttps;

    /// <summary>Open member registration — creates the login, the Member row and its QR token.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var phone = NormalisePhone(request.Phone);

        if (await _users.FindByEmailAsync(email) is not null)
            return Problem("An account already exists for that email address.", statusCode: StatusCodes.Status409Conflict);

        if (await _db.Members.AnyAsync(m => m.Phone == phone, ct))
            return Problem("An account already exists for that mobile number.", statusCode: StatusCodes.Status409Conflict);

        var branch = request.HomeBranchId is int branchId
            ? await _db.Branches.FirstOrDefaultAsync(b => b.Id == branchId && b.IsActive, ct)
            : await _db.Branches.Where(b => b.IsActive).OrderBy(b => b.DisplayOrder).FirstOrDefaultAsync(ct);

        if (branch is null)
            return Problem("That branch is not available. Pick another.", statusCode: StatusCodes.Status400BadRequest);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            PhoneNumber = phone,
            FullName = request.FullName.Trim(),
            HomeBranchId = branch.Id,
            EmailConfirmed = false,
            IsActive = true
        };

        var created = await _users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            return ValidationProblem(ToModelState(created));

        await _users.AddToRoleAsync(user, RoleNames.Member);

        var member = new Member
        {
            UserId = user.Id,
            MemberCode = await NextMemberCodeAsync(branch, ct),
            HomeBranchId = branch.Id,
            FullName = user.FullName,
            Phone = phone,
            Email = email,
            DateOfBirth = request.DateOfBirth,
            // Self-registered accounts start as leads-in-waiting until a plan is bought.
            Status = MemberStatus.Lead,
            JoinedOn = DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(330)),
            PrimaryGoal = request.PrimaryGoal,
            QrToken = Guid.NewGuid().ToString("N"),
            ReferralCode = BuildReferralCode(user.FullName, user.Id),
            ConsentMarketing = request.ConsentMarketing
        };

        _db.Members.Add(member);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Registered member {MemberCode} ({Email}) at {Branch}", member.MemberCode, email, branch.Name);

        var pair = await _tokens.IssueAsync(user, ClientIp(), ct);
        SetRefreshCookie(pair.RefreshToken, pair.RefreshTokenExpiresAtUtc);
        return Ok(new AuthResponse
        {
            AccessToken = pair.AccessToken,
            AccessTokenExpiresAtUtc = pair.AccessTokenExpiresAtUtc,
            User = await DescribeAsync(user, ct)
        });
    }

    /// <summary>Password login by email or mobile number.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var identifier = request.Identifier.Trim();
        var user = identifier.Contains('@')
            ? await _users.FindByEmailAsync(identifier.ToLowerInvariant())
            : await ByPhoneAsync(NormalisePhone(identifier), ct);

        // Same message and roughly the same work either way — do not leak which half was wrong.
        if (user is null)
            return Problem("Those credentials do not match an account.", statusCode: StatusCodes.Status401Unauthorized);

        if (!user.IsActive)
            return Problem("That account is inactive. Contact the front desk.", statusCode: StatusCodes.Status403Forbidden);

        var result = await _signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
            return Problem("Too many attempts. Try again in fifteen minutes.", statusCode: StatusCodes.Status423Locked);
        if (!result.Succeeded)
            return Problem("Those credentials do not match an account.", statusCode: StatusCodes.Status401Unauthorized);

        user.LastLoginAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var pair = await _tokens.IssueAsync(user, ClientIp(), ct);
        SetRefreshCookie(pair.RefreshToken, pair.RefreshTokenExpiresAtUtc);
        return Ok(new AuthResponse
        {
            AccessToken = pair.AccessToken,
            AccessTokenExpiresAtUtc = pair.AccessTokenExpiresAtUtc,
            User = await DescribeAsync(user, ct)
        });
    }

    /// <summary>Exchanges the httpOnly refresh cookie for a new access token, rotating the cookie.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var presented) || string.IsNullOrWhiteSpace(presented))
            return Problem("No refresh token.", statusCode: StatusCodes.Status401Unauthorized);

        var rotated = await _tokens.RotateAsync(presented, ClientIp(), ct);
        if (rotated is null)
        {
            ClearRefreshCookie();
            return Problem("That session has expired. Sign in again.", statusCode: StatusCodes.Status401Unauthorized);
        }

        SetRefreshCookie(rotated.Tokens.RefreshToken, rotated.Tokens.RefreshTokenExpiresAtUtc);
        return Ok(new AuthResponse
        {
            AccessToken = rotated.Tokens.AccessToken,
            AccessTokenExpiresAtUtc = rotated.Tokens.AccessTokenExpiresAtUtc,
            User = await DescribeAsync(rotated.User, ct)
        });
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies.TryGetValue(RefreshCookieName, out var presented) && !string.IsNullOrWhiteSpace(presented))
            await _tokens.RevokeAsync(presented, ClientIp(), "Signed out", ct);

        ClearRefreshCookie();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);
        if (user is null) return Unauthorized();
        return Ok(await DescribeAsync(user, ct));
    }

    /// <summary>Clears MustChangePassword and revokes every other session for the account.</summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);
        if (user is null) return Unauthorized();

        var result = await _users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded) return ValidationProblem(ToModelState(result));

        user.MustChangePassword = false;
        await _db.SaveChangesAsync(ct);
        await _tokens.RevokeAllForUserAsync(user.Id, ClientIp(), "Password changed", ct);
        ClearRefreshCookie();
        return NoContent();
    }

    // ------------------------------------------------------------------ helpers

    private async Task<CurrentUserResponse> DescribeAsync(ApplicationUser user, CancellationToken ct)
    {
        var roles = await _users.GetRolesAsync(user);
        var member = await _db.Members
            .AsNoTracking()
            .Where(m => m.UserId == user.Id)
            .Select(m => new { m.Id, m.MemberCode })
            .FirstOrDefaultAsync(ct);
        var branchName = user.HomeBranchId is null
            ? null
            : await _db.Branches.AsNoTracking()
                .Where(b => b.Id == user.HomeBranchId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync(ct);

        return new CurrentUserResponse
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.PhoneNumber,
            Roles = roles.ToArray(),
            MemberId = member?.Id,
            MemberCode = member?.MemberCode,
            HomeBranchId = user.HomeBranchId,
            HomeBranchName = branchName,
            AvatarUrl = user.AvatarUrl,
            MustChangePassword = user.MustChangePassword
        };
    }

    private Task<ApplicationUser?> ByPhoneAsync(string phone, CancellationToken ct) =>
        _users.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone, ct);

    /// <summary>
    /// Continues the branch's existing member-number series. The prefix comes from the
    /// branch row, not from its slug, so seeded and self-registered members share one series.
    /// </summary>
    private async Task<string> NextMemberCodeAsync(Branch branch, CancellationToken ct)
    {
        var prefix = $"FRG-{branch.MemberCodePrefix}-";
        var highest = await _db.Members
            .Where(m => m.MemberCode.StartsWith(prefix))
            .Select(m => m.MemberCode)
            .OrderByDescending(code => code)
            .FirstOrDefaultAsync(ct);

        var next = 1;
        if (highest is not null && int.TryParse(highest[prefix.Length..], out var parsed))
            next = parsed + 1;

        return $"{prefix}{next:D5}";
    }

    private static string BuildReferralCode(string fullName, int userId)
    {
        var initials = new string(fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]))
            .Take(3)
            .ToArray());
        return $"{(initials.Length == 0 ? "FRG" : initials)}{1000 + userId}";
    }

    /// <summary>Stores Indian mobiles as +91XXXXXXXXXX so login by phone matches the seeder.</summary>
    private static string NormalisePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 10) return $"+91{digits}";
        if (digits.Length == 12 && digits.StartsWith("91")) return $"+{digits}";
        if (digits.Length == 11 && digits.StartsWith('0')) return $"+91{digits[1..]}";
        return phone.StartsWith('+') ? phone : $"+{digits}";
    }

    private string? ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private void SetRefreshCookie(string token, DateTime expiresAtUtc) =>
        Response.Cookies.Append(RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = RequireSecureCookie,
            SameSite = SameSiteMode.Lax,
            Expires = expiresAtUtc,
            Path = "/api/auth",
            IsEssential = true
        });

    private void ClearRefreshCookie() =>
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true, Secure = RequireSecureCookie, SameSite = SameSiteMode.Lax, Path = "/api/auth"
        });

    private ModelStateDictionary ToModelState(IdentityResult result)
    {
        var state = new ModelStateDictionary();
        foreach (var error in result.Errors)
            state.AddModelError(error.Code.Contains("Password") ? "password" : string.Empty, error.Description);
        return state;
    }
}
