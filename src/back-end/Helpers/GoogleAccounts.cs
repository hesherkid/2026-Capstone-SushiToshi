using back_end.domain.Entities;

namespace back_end.Helpers
{
    // Google sign-in proves who owns an email, which settles who owns an account nobody verified yet
    public static class GoogleAccounts
    {
        // Confirms an unverified account and removes its password, since whoever set the password
        // never proved they own the email. Returns true when it changed the account, so the caller
        // can revoke any links already sent. A confirmed account is left alone.
        public static bool ClaimUnverified(User user)
        {
            if (user.Is_email_confirmed) return false;

            user.Is_email_confirmed = true;
            user.Password_hash = null;
            return true;
        }
    }
}
