using back_end.domain.Entities;

namespace back_end.Services.Auth;

public interface IAuthTokenService
{
    Task<string> CreateAsync(
        int userId,
        AuthTokenPurpose purpose,
        CancellationToken ct = default);

    Task<AuthActionToken?> FindValidAsync(
        string token,
        AuthTokenPurpose purpose,
        CancellationToken ct = default);

    // False when another request already used the token or it expired
    Task<bool> TryConsumeAsync(
        AuthActionToken token,
        CancellationToken ct = default);

    // Makes every unused link already sent to the user stop working
    Task RevokeAllAsync(
        int userId,
        CancellationToken ct = default);
}