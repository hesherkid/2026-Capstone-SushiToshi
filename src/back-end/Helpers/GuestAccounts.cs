using System;
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

        // No 0/O or 1/I so staff can read the code off a screen
        private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public static string NewEmail() => $"guest-{Guid.NewGuid():N}@{EmailDomain}";

        // Short code shown after "Guest" so staff can tell guests at a table apart, e.g. "Guest K7P2"
        public static string NewCode() => RandomNumberGenerator.GetString(CodeAlphabet, 4);

        public static bool IsGuest(string? email) =>
            email != null && email.EndsWith("@" + EmailDomain, StringComparison.OrdinalIgnoreCase);
    }
}
