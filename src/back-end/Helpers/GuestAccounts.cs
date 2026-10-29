using System;
using System.Security.Claims;
using System.Security.Cryptography;

namespace back_end.Helpers
{
    // "Continue as Guest" creates a throwaway customer account per guest so each guest is
    // their own session participant. They have no password and a reserved email domain.
    public static class GuestAccounts
    {
        // .invalid is reserved, so these addresses can never belong to a real person
        public const string EmailDomain = "guest.sushitoshi.invalid";

        // Long enough for a whole visit; regular logins use Jwt:ExpiresInMinutes
        public const int DefaultTokenMinutes = 6 * 60;

        // POST /api/auth/guest creates a database row per call, so it's rate limited per IP address.
        // Diners on the restaurant Wi-Fi share one public IP, so the limit allows a full room
        // arriving at once while stopping a script from creating thousands of accounts.
        public const string RateLimitPolicy = "guest-login";
        public const int RateLimitPerMinute = 30;

        // No 0/O or 1/I so staff can read the code off a screen
        private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public static string NewEmail() => $"guest-{Guid.NewGuid():N}@{EmailDomain}";

        // Short code shown after "Guest" so staff can tell guests at a table apart, e.g. "Guest K7P2"
        public static string NewCode() => RandomNumberGenerator.GetString(CodeAlphabet, 4);

        // JWT claim marking guest tokens, so the front end can reuse one instead of making a new guest
        public const string TokenClaim = "guest";

        public static bool IsGuest(string? email) =>
            email != null && email.EndsWith("@" + EmailDomain, StringComparison.OrdinalIgnoreCase);

        // Tokens from before the guest claim was added are recognised by their email
        public static bool IsGuest(ClaimsPrincipal user) =>
            user.HasClaim(TokenClaim, "true") || IsGuest(user.FindFirst(ClaimTypes.Email)?.Value);
    }
}
