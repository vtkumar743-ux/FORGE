using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Gym.Core.Entities;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace Gym.Infrastructure.Services;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "forge-api";
    public string Audience { get; set; } = "forge-client";
    /// <summary>At least 32 bytes; validated at startup so a weak key cannot ship.</summary>
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}

/// <summary>Custom claims the client and the branch-scope policies read.</summary>
public static class GymClaims
{
    public const string MemberId = "member_id";
    public const string BranchId = "branch_id";
    public const string MustChangePassword = "must_change_password";
    public const string FullName = "full_name";
}

/// <summary>Rotation result — carries the owning user so the caller need not re-derive it.</summary>
public record RotatedTokens(TokenPair Tokens, ApplicationUser User);

public interface ITokenService
{
    Task<TokenPair> IssueAsync(ApplicationUser user, string? ip, CancellationToken ct = default);
    /// <summary>Validates the presented refresh token, revokes it and issues a replacement.</summary>
    Task<RotatedTokens?> RotateAsync(string refreshToken, string? ip, CancellationToken ct = default);
    Task<bool> RevokeAsync(string refreshToken, string? ip, string reason, CancellationToken ct = default);
    Task RevokeAllForUserAsync(int userId, string? ip, string reason, CancellationToken ct = default);
}

public class TokenService : ITokenService
{
    private readonly GymDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly JwtOptions _options;
    private readonly IClock _clock;

    public TokenService(GymDbContext db, UserManager<ApplicationUser> users, IOptions<JwtOptions> options, IClock clock)
    {
        _db = db;
        _users = users;
        _options = options.Value;
        _clock = clock;
    }

    public async Task<TokenPair> IssueAsync(ApplicationUser user, string? ip, CancellationToken ct = default)
    {
        var roles = await _users.GetRolesAsync(user);
        var memberId = await _db.Members
            .Where(m => m.UserId == user.Id)
            .Select(m => (int?)m.Id)
            .FirstOrDefaultAsync(ct);

        var now = _clock.UtcNow;
        var accessExpires = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(GymClaims.FullName, user.FullName)
        };
        if (!string.IsNullOrEmpty(user.Email)) claims.Add(new Claim(ClaimTypes.Email, user.Email));
        if (memberId is not null) claims.Add(new Claim(GymClaims.MemberId, memberId.Value.ToString()));
        if (user.HomeBranchId is not null) claims.Add(new Claim(GymClaims.BranchId, user.HomeBranchId.Value.ToString()));
        if (user.MustChangePassword) claims.Add(new Claim(GymClaims.MustChangePassword, "true"));
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: accessExpires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        var (refreshToken, refreshExpires) = await CreateRefreshTokenAsync(user.Id, ip, null, ct);
        return new TokenPair(accessToken, accessExpires, refreshToken, refreshExpires);
    }

    public async Task<RotatedTokens?> RotateAsync(string refreshToken, string? ip, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken);
        var stored = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null) return null;

        // Replay of an already-rotated token means the cookie leaked: kill the whole family.
        if (stored.RevokedAtUtc is not null)
        {
            await RevokeAllForUserAsync(stored.UserId, ip, "Reuse of a rotated refresh token", ct);
            return null;
        }
        if (stored.IsExpired || !stored.User.IsActive) return null;

        var (newToken, newExpires) = await CreateRefreshTokenAsync(stored.UserId, ip, null, ct);
        stored.RevokedAtUtc = _clock.UtcNow;
        stored.RevokedByIp = ip;
        stored.ReplacedByTokenHash = Hash(newToken);
        stored.ReasonRevoked = "Rotated";
        await _db.SaveChangesAsync(ct);

        // Re-issue the access token against the (possibly changed) user and roles.
        var roles = await _users.GetRolesAsync(stored.User);
        var access = await IssueAccessOnlyAsync(stored.User, roles, ct);
        return new RotatedTokens(
            new TokenPair(access.Token, access.ExpiresAtUtc, newToken, newExpires),
            stored.User);
    }

    public async Task<bool> RevokeAsync(string refreshToken, string? ip, string reason, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null || stored.RevokedAtUtc is not null) return false;

        stored.RevokedAtUtc = _clock.UtcNow;
        stored.RevokedByIp = ip;
        stored.ReasonRevoked = reason;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task RevokeAllForUserAsync(int userId, string? ip, string reason, CancellationToken ct = default)
    {
        var live = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(ct);

        foreach (var token in live)
        {
            token.RevokedAtUtc = _clock.UtcNow;
            token.RevokedByIp = ip;
            token.ReasonRevoked = reason;
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task<(string Token, DateTime ExpiresAtUtc)> IssueAccessOnlyAsync(
        ApplicationUser user, IList<string> roles, CancellationToken ct)
    {
        var memberId = await _db.Members
            .Where(m => m.UserId == user.Id)
            .Select(m => (int?)m.Id)
            .FirstOrDefaultAsync(ct);

        var now = _clock.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(GymClaims.FullName, user.FullName)
        };
        if (!string.IsNullOrEmpty(user.Email)) claims.Add(new Claim(ClaimTypes.Email, user.Email));
        if (memberId is not null) claims.Add(new Claim(GymClaims.MemberId, memberId.Value.ToString()));
        if (user.HomeBranchId is not null) claims.Add(new Claim(GymClaims.BranchId, user.HomeBranchId.Value.ToString()));
        if (user.MustChangePassword) claims.Add(new Claim(GymClaims.MustChangePassword, "true"));
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var token = new JwtSecurityToken(
            _options.Issuer, _options.Audience, claims, now, expires,
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    private async Task<(string Token, DateTime ExpiresAtUtc)> CreateRefreshTokenAsync(
        int userId, string? ip, string? replaces, CancellationToken ct)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var expires = _clock.UtcNow.AddDays(_options.RefreshTokenDays);

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(raw),
            ExpiresAtUtc = expires,
            CreatedAtUtc = _clock.UtcNow,
            CreatedByIp = ip,
            ReplacedByTokenHash = replaces
        });
        await _db.SaveChangesAsync(ct);
        return (raw, expires);
    }

    /// <summary>Only the hash is persisted, so a database leak cannot mint sessions.</summary>
    private static string Hash(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

/// <summary>
/// JwtRegisteredClaimNames from the JWT handler uses "sub"/"jti"; naming them here keeps
/// the dependency on the constant names in one place.
/// </summary>
internal static class JwtRegisteredClaimNames
{
    public const string Sub = "sub";
    public const string Jti = "jti";
}
