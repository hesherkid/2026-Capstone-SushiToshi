namespace back_end.Helpers;

// Per-IP limits on the sign-in and account endpoints in AuthController (guest sign-in has its
// own, see GuestAccounts). Diners on the restaurant Wi-Fi share one public IP, so the limits
// allow a busy room while slowing a script down. Each IP only uses up its own allowance.
public static class AuthRateLimits
{
    // login, google, verify-email, reset-password: slows password and token guessing
    public const string SignInPolicy = "auth-sign-in";
    public const int SignInPerMinute = 20;

    // register, forgot-password, resend-verification: each call can send an email
    public const string EmailPolicy = "auth-email";
    public const int EmailPerMinute = 5;

}
