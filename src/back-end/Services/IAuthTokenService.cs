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

    Task ConsumeAsync(
        AuthActionToken token,
        CancellationToken ct = default);
}