using System.Security.Cryptography;
using System.Text;
using back_end.Configurations;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace back_end.Services.Auth;

public sealed class AuthTokenService : IAuthTokenService
{
    private readonly ApplicationDbContext _db;
    private readonly AuthEmailSettings _settings;

    public AuthTokenService(
        ApplicationDbContext db,
        IOptions<AuthEmailSettings> settings)
    {
        _db = db;
        _settings = settings.Value;
    }

    public async Task<string> CreateAsync(
        int userId,
        AuthTokenPurpose purpose,
        CancellationToken ct = default)
    {
        var rawToken = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(32));

        var tokenHash = Hash(rawToken);

        var now = DateTime.UtcNow;

        // Invalidate previous unused tokens of this purpose.
        await _db.AuthActionTokens
            .Where(x =>
                x.UserId == userId &&
                x.Purpose == purpose &&
                x.UsedAt == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(
                    x => x.UsedAt, now),
                ct);

        var lifetime = purpose switch
        {
            AuthTokenPurpose.EmailVerification =>
                _settings.VerificationExpiryMinutes,

            AuthTokenPurpose.PasswordReset =>
                _settings.PasswordResetExpiryMinutes,

            _ => throw new ArgumentOutOfRangeException(
                nameof(purpose))
        };

        _db.AuthActionTokens.Add(new AuthActionToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            Purpose = purpose,
            ExpiresAt = now.AddMinutes(lifetime)
        });

        await _db.SaveChangesAsync(ct);

        return rawToken;
    }

    public async Task<AuthActionToken?> FindValidAsync(
        string token,
        AuthTokenPurpose purpose,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) ||
            token.Length != 64 ||
            !token.All(Uri.IsHexDigit))
        {
            return null;
        }

        var hash = Hash(token.ToUpperInvariant());

        return await _db.AuthActionTokens
            .FirstOrDefaultAsync(x =>
                x.TokenHash == hash &&
                x.Purpose == purpose &&
                x.UsedAt == null &&
                x.ExpiresAt > DateTime.UtcNow,
                ct);
    }

    public async Task ConsumeAsync(
        AuthActionToken token,
        CancellationToken ct = default)
    {
        var updated = await _db.AuthActionTokens
            .Where(x =>
                x.Id == token.Id &&
                x.UsedAt == null &&
                x.ExpiresAt > DateTime.UtcNow)
            .ExecuteUpdateAsync(
                s => s.SetProperty(
                    x => x.UsedAt, DateTime.UtcNow),
                ct);

        if (updated != 1)
        {
            throw new InvalidOperationException(
                "Token already used or expired.");
        }
    }

    private static string Hash(string token)
    {
        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(token)));
    }
}