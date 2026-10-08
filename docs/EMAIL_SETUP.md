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

## 4. Configure `appsettings.json`

Add these sections to your existing file (do not replace the rest of its configuration):

```json
{
  "SendGrid": {
    "ApiKey": "",
    "FromEmail": "noreply@example.com",
    "FromName": "Sushi Toshi"
  },
  "AuthEmail": {
    "FrontendBaseUrl": "http://localhost:3000",
    "VerificationExpiryMinutes": 1440,
    "PasswordResetExpiryMinutes": 30
  }
}
```

| Setting | Meaning |
|---|---|
| `SendGrid:ApiKey` | Secret used to authenticate requests to SendGrid |
| `SendGrid:FromEmail` | Verified sender address |
| `SendGrid:FromName` | Sender display name |
| `AuthEmail:FrontendBaseUrl` | Next.js origin, without `/auth` |
| `AuthEmail:VerificationExpiryMinutes` | Verification link lifetime |
| `AuthEmail:PasswordResetExpiryMinutes` | Password reset link lifetime |

### Keep secrets outside source control

For local development:

```bash
dotnet user-secrets init
dotnet user-secrets set "SendGrid:ApiKey" "YOUR_SENDGRID_API_KEY"
```

For deployment, use a secret manager or set the environment variable `SendGrid__ApiKey`.

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

`Services/Email/IEmailService.cs`:

```csharp
public interface IEmailService
{
    Task SendEmailVerificationAsync(
        string email,
        string verificationUrl,
        CancellationToken ct = default);

    Task SendPasswordResetAsync(
        string email,
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
3. For eligible accounts, it creates a random, short-lived, single-use token and sends the email.
4. The user opens `/auth/reset-password` and submits a new password.
5. The backend validates and consumes the token, saves the new **BCrypt hash**, and invalidates existing sessions as supported by the authentication architecture.

Reset link:

```text
http://localhost:3000/auth/reset-password?token=RESET_TOKEN
```

Reset API:

```http
POST /api/auth/reset-password
Content-Type: application/json

{"token":"RESET_TOKEN","newPassword":"ExampleNewPassword123!"}
```

Match JSON property names to the actual `[JsonPropertyName]` attributes on your project's DTOs. Token consumption and user updates should occur within one database transaction.

## 8. API endpoints and frontend pages

| Method | Backend route | Frontend page |
|---|---|---|
| POST | `/api/auth/register` | `/auth/register` |
| POST | `/api/auth/login` | `/auth/login` |
| POST | `/api/auth/verify-email` | `/auth/verify-email?token=...` |
| POST | `/api/auth/resend-verification` | `/auth/resend-verification` |
| POST | `/api/auth/forgot-password` | `/auth/forgot-password` |
| POST | `/api/auth/reset-password` | `/auth/reset-password?token=...` |

The Next.js rewrite used for local development is:

```text
Browser: http://localhost:3000/api/*
   → ASP.NET Core: http://127.0.0.1:5264/api/*
```

The frontend auth helpers live in `src/utils/auth.js` and should centralize registration, verification, reset requests, and login.

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
| SendGrid returns 401 | API key validity |
| SendGrid returns 403 | Key permissions or account limitations |
| Sender rejected | Sender identity / authenticated domain |
| Email not received | Spam, suppression, bounce, Email Activity |
| Link gives 404 | Use `/auth/verify-email` or `/auth/reset-password` |
| Token rejected | Expiry, incorrect purpose, already consumed |
| Verified user cannot log in | `Status` / admin approval as well as `Is_email_confirmed` |
| API returns 400 / null DTO fields | Check frontend JSON names against `[JsonPropertyName]` |

## 11. Production security checklist

- [ ] Authenticate the production domain and configure email authentication records.
- [ ] Use HTTPS for all public links.
- [ ] Use a dedicated, restricted SendGrid API key stored in secret management.
- [ ] Never log passwords, raw tokens, API keys, or sensitive URLs.
- [ ] Hash tokens at rest and require expiry and single-use.
- [ ] Consume tokens and update users atomically.
- [ ] Rate-limit registration, login, resend verification, and forgot password.
- [ ] Avoid account enumeration through password-reset responses.
- [ ] Set `Referrer-Policy: no-referrer` on token-bearing frontend pages.
- [ ] Revoke existing sessions following a password change, where supported.
- [ ] Test email/password, Google, and Guest authentication policies.

## References

- [SendGrid](https://sendgrid.com/)
- [SendGrid Dashboard](https://app.sendgrid.com/)
- [API Keys](https://www.twilio.com/docs/sendgrid/ui/account-and-settings/api-keys)
- [C# Email API Quickstart](https://www.twilio.com/docs/sendgrid/for-developers/sending-email/email-api-quickstart-for-c)
- [Domain Authentication](https://www.twilio.com/docs/sendgrid/ui/account-and-settings/how-to-set-up-domain-authentication)
- [SendGrid C# SDK](https://github.com/sendgrid/sendgrid-csharp)
