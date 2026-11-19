# SendGrid Email Setup Guide — Sushi Toshi

## Overview

Sushi Toshi uses **Twilio SendGrid** for transactional authentication emails:

- **Email verification** after user registration
- **Resend verification** links
- **Forgot password** requests
- **Password reset** links

Emails are sent by the **ASP.NET Core backend**. The Next.js frontend never sends email directly to SendGrid and must never contain the SendGrid API key.

## 1. Create a SendGrid account

1. Visit [Twilio SendGrid](https://sendgrid.com/) and create an account.
2. Complete account verification and enable two-factor authentication.
3. Sign in to the [SendGrid dashboard](https://app.sendgrid.com/).

> Availability, plans, and sending limits may change. Review your account's current allowances.

## 2. Authenticate a sender

For development, open **Settings → Sender Authentication** and choose **Single Sender Verification**. Enter your sender name, email address, reply-to address, and required contact information, then confirm the verification email.

For production, use **Domain Authentication** instead. Configure the DNS records SendGrid provides and check your domain's SPF, DKIM, and DMARC setup.

Example verified sender:

- **From Name:** Sushi Toshi
- **From Email:** `noreply@example.com` (replace with your verified sender)

## 3. Create a SendGrid API key

1. Open **Settings → API Keys**.
2. Select **Create API Key**.
3. Name it `SushiToshi-Backend`.
4. Select restricted/custom permissions with **Mail Send** access.
5. Generate the key and store it securely. It may only be displayed once.

## 4. Configure SendGrid outside source control

> **The backend won't start without `SendGrid:ApiKey` and `SendGrid:FromEmail`**, in Development too. Both are checked when it starts (`SendGridSettings` in `src/back-end/Configurations/`), and neither is in the committed settings, so a fresh clone stops with `OptionsValidationException: DataAnnotation validation failed for 'SendGridSettings'`.
>
> If you don't need real emails locally, set placeholders instead (from `src/back-end`):
>
> ```bash
> dotnet user-secrets set "SendGrid:ApiKey" "local-placeholder"
> dotnet user-secrets set "SendGrid:FromEmail" "noreply@example.com"
> ```
>
> The backend then starts and everything except email works. Registering still saves the account, but the request returns a 500 because SendGrid rejects the key, and no verification email arrives. Use the [seeded accounts](../dev/database.md#seeded-accounts), which are already verified. CI uses placeholders the same way (`.github/workflows/automated-tests.yml`).

Do not put `SendGrid:ApiKey` or `SendGrid:FromEmail` in tracked settings such as `appsettings.json`. The sender address is included in outgoing emails, and keeping it out of the repository prevents it from being exposed through the source. Recipients will still see the sender address, so use a verified role address such as `noreply@yourdomain.com` rather than a personal address.

The project already has a `UserSecretsId`. From `src/back-end`, configure local development values with User Secrets:

```bash
dotnet user-secrets set "SendGrid:ApiKey" "YOUR_SENDGRID_API_KEY"
dotnet user-secrets set "SendGrid:FromEmail" "verified-sender@yourdomain.com"
```

Replace the sender example with an address verified in SendGrid. User Secrets are loaded for local Development and are not committed to the repository. `SendGrid:FromName` can remain in `appsettings.json` because it is a display name, not a credential.

For deployment, use a secret manager or environment variables:

```text
SendGrid__ApiKey=YOUR_SENDGRID_API_KEY
SendGrid__FromEmail=verified-sender@yourdomain.com
```

`AuthEmail` settings such as `FrontendBaseUrl`, `VerificationExpiryMinutes`, and `PasswordResetExpiryMinutes` can remain in `appsettings.json`; they are not SendGrid credentials.

| Setting | Meaning |
|---|---|
| `SendGrid:ApiKey` | Secret used to authenticate requests to SendGrid |
| `SendGrid:FromEmail` | Verified sender address shown to email recipients |
| `SendGrid:FromName` | Sender display name |
| `AuthEmail:FrontendBaseUrl` | Next.js origin, without `/auth` |
| `AuthEmail:VerificationExpiryMinutes` | Verification link lifetime |
| `AuthEmail:PasswordResetExpiryMinutes` | Password reset link lifetime |

## 5. Email service architecture

```text
AuthController
  ├── IEmailService
  │     └── SendGridEmailService
  │           └── SendGrid Mail Send API
  └── IAuthTokenService
        └── Database (hashed one-time tokens)
```

The email service handles sending. The token service handles generating, storing, validating, and consuming authentication tokens.

### Interface

`src/back-end/Services/IEmailService.cs` (namespace `back_end.Services.Email`):

```csharp
public interface IEmailService
{
    Task SendEmailVerificationAsync(
        string toEmail,
        string verificationUrl,
        CancellationToken ct = default);

    Task SendPasswordResetAsync(
        string toEmail,
        string resetUrl,
        CancellationToken ct = default);
}
```

## 6. Email verification workflow

1. A new user registers.
2. The backend stores the user with `Is_email_confirmed = false`.
3. It generates a cryptographically random verification token and stores **only its hash**.
4. SendGrid emails a link to the Next.js verification page.
5. The user opens the page and explicitly submits the token to the backend.
6. The backend validates the token, marks the email verified, and consumes the token **atomically**.
7. Login becomes available if the user also satisfies the account-status/approval policy.

Verification link:

```text
http://localhost:3000/auth/verify-email?token=VERIFICATION_TOKEN
```

Verification API:

```http
POST /api/auth/verify-email
Content-Type: application/json

{"token":"VERIFICATION_TOKEN"}
```

**Registration must not return a login JWT for an unverified user.**

## 7. Password reset workflow

1. The user enters an email at `/auth/forgot-password`.
2. The backend returns a generic response regardless of whether the account exists.
3. Only for accounts that have a password, it creates a random, short-lived, single-use token and sends the email. Accounts with no password (Google sign-in accounts whose password was removed, and guest accounts) get no email.
4. The user opens `/auth/reset-password` and submits a new password.
5. The backend validates and consumes the token and saves the new **BCrypt hash**. Devices that are already signed in stay signed in: logins are JWTs, which stay valid until they expire.

Reset link:

```text
http://localhost:3000/auth/reset-password?token=RESET_TOKEN
```

Reset API:

```http
POST /api/auth/reset-password
Content-Type: application/json

{"token":"RESET_TOKEN","new_password":"ExampleNewPassword123!"}
```

The request bodies bind to `VerifyEmailDto` and `ResetPasswordDto` in `src/back-end/Dtos/Auth/`. Their JSON keys are snake_case (see [naming conventions](../dev/naming-conventions.md)). New passwords must be 8 to 200 characters and not only spaces. If the same link is submitted twice at once, only the request that consumes the token changes anything.

## 8. API endpoints and frontend pages

| Method | Backend route | Frontend page |
|---|---|---|
| POST | `/api/auth/register` | `/auth/register` |
| POST | `/api/auth/login` | `/auth/login` |
| POST | `/api/auth/verify-email` | `/auth/verify-email?token=...` |
| POST | `/api/auth/resend-verification` | `/auth/resend-verification`, linked from **Resend Verification Email** on the login page (after a sign-in fails because the email isn't verified, with the email filled in) and the verify-email page. `/auth/register` also has a resend button after signing up |
| POST | `/api/auth/forgot-password` | `/auth/forgot-password` |
| POST | `/api/auth/reset-password` | `/auth/reset-password?token=...` |

The Next.js rewrite used for local development is:

```text
Browser: http://localhost:3000/api/*
   → ASP.NET Core: http://127.0.0.1:5264/api/*
```

The frontend auth helpers live in `src/front-end/src/utils/auth.js` and should centralize registration, verification, reset requests, and login.

## 9. Test email delivery

### Registration and verification

- [ ] Run the backend and frontend.
- [ ] Register with an email address you control.
- [ ] Confirm the app shows a verification message, without logging in.
- [ ] Check inbox and spam folder.
- [ ] Open `/auth/verify-email?token=...` from the email.
- [ ] Click Verify Email and confirm success.
- [ ] Sign in with the verified account.
- [ ] Confirm an expired or already-used token is rejected.

### Password reset

- [ ] Request a reset from `/auth/forgot-password`.
- [ ] Confirm a generic acknowledgment is shown.
- [ ] Open the email link to `/auth/reset-password?token=...`.
- [ ] Submit a new password and confirm success.
- [ ] Confirm the old password fails and the new one works.
- [ ] Confirm the reset link cannot be reused.

### Delivery status

A response such as `202 Accepted` from SendGrid means the message was **accepted for processing**, not necessarily delivered. Check the SendGrid Email Activity/event information available on your account for bounces, suppressions, and other delivery issues.

## 10. Troubleshooting

| Symptom | Check |
|---|---|
| Backend stops at startup with `OptionsValidationException ... 'SendGridSettings'` | `SendGrid:ApiKey` or `SendGrid:FromEmail` isn't set (see [step 4](#4-configure-sendgrid-outside-source-control)) |
| Register returns 500, but the account was created | The email send failed. The backend log has `SendGrid rejected email. Status: ...`. Check the API key and sender, then use **Resend Verification Email** |
| Forgot password shows success, but no email arrives for one account | That account has no password (Google sign-in or guest), so no reset email is sent |
| SendGrid returns 401 | API key validity |
| SendGrid returns 403 | Key permissions or account limitations |
| Sender rejected | Sender identity / authenticated domain |
| Email not received | Spam, suppression, bounce, Email Activity |
| Link gives 404 | Use `/auth/verify-email` or `/auth/reset-password` |
| Token rejected | Expiry, incorrect purpose, already consumed |
| Verified user cannot log in | `Status` as well as `Is_email_confirmed`: a deactivated account gets "Account is not active" |
| API returns 400 / null DTO fields | Check the frontend's JSON keys against the DTOs in `src/back-end/Dtos/Auth/` (snake_case, e.g. `new_password`). In Development, an unknown key gets a 400 that names it |

## 11. Production security checklist

- [ ] Authenticate the production domain and configure email authentication records.
- [ ] Use HTTPS for all public links (`AuthEmail__FrontendBaseUrl` starts with `https://`).
- [ ] Use a dedicated, restricted SendGrid API key stored in secret management.
- [ ] Never log passwords, raw tokens, API keys, or token-bearing URLs if you add logging.
- [ ] Test email/password, Google, and Guest sign-in on the production site.

Already handled by the code, so there's nothing to set up: tokens are hashed at rest, expire and work once; registration, login, resend verification and forgot password are rate-limited per IP address; and forgot password gives the same answer whether or not the account exists.

Not built yet, worth considering before launch:

- [ ] Send `Referrer-Policy: no-referrer` on `/auth/verify-email` and `/auth/reset-password`, so the token in the URL can't leak to other sites through the `Referer` header.
- [ ] Sign out other devices after a password reset or change. Logins are JWTs, so this needs a token version or revocation list.

## References

- [SendGrid](https://sendgrid.com/)
- [SendGrid Dashboard](https://app.sendgrid.com/)
- [API Keys](https://www.twilio.com/docs/sendgrid/ui/account-and-settings/api-keys)
- [C# Email API Quickstart](https://www.twilio.com/docs/sendgrid/for-developers/sending-email/email-api-quickstart-for-c)
- [Domain Authentication](https://www.twilio.com/docs/sendgrid/ui/account-and-settings/how-to-set-up-domain-authentication)
- [SendGrid C# SDK](https://github.com/sendgrid/sendgrid-csharp)
