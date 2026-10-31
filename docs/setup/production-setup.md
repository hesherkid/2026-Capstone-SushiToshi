# Production Setup

Everything that must change before Sushi Toshi runs anywhere other than a developer's machine. The values in `src/back-end/appsettings.json` are development defaults, and several of them are not safe to deploy.

Work through each section in order. The checklist at the end is for a final pass before going live.

## How settings are applied

The backend reads settings from `appsettings.json`, then lets **environment variables** override them. In production, set every secret and every environment-specific value as an environment variable. Never edit `appsettings.json` with real values; it's committed to git.

Environment variable names use double underscores where the setting uses a colon:

| Setting | Environment variable |
| --- | --- |
| `Jwt:Key` | `Jwt__Key` |
| `ConnectionStrings:MySqlConnection` | `ConnectionStrings__MySqlConnection` |
| `Cors:AllowedOrigins` (a list) | `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, ... |

User secrets (`dotnet user-secrets`) only load in Development, so they don't apply here.

### Run in the Production environment

Set `ASPNETCORE_ENVIRONMENT=Production` on the server. `dotnet run --launch-profile http` sets Development, so don't start production that way.

In Production the backend:

- turns **off** Swagger (`/swagger`)
- turns **on** HTTPS redirection
- only accepts requests from the origins in `Cors:AllowedOrigins` (see below)

## 1. Backend: required settings

These must be set, or the app either won't work or won't be secure.

### Database connection

```
ConnectionStrings__MySqlConnection=Server=<host>;Port=3306;Database=sushi_toshi_db;User=<user>;Password=<password>;
```

- The committed value points at the local Docker database (`localhost:3307`, `app_user` / `app_password`). `docker-compose.yml` is for development only.
- Use a database user with a strong password, and only the permissions the app needs.
- **Migrations run automatically** when the backend starts, so you don't need `dotnet ef database update` on the server. Back up the database before deploying a version with new migrations.

### JWT signing key

```
Jwt__Key=<new random secret>
```

`appsettings.json` has no signing key, so **the backend won't start outside Development until `Jwt__Key` is set**. The development key in `appsettings.Development.json` is committed to git, and anyone who has seen the repo could use it to create valid login tokens, including admin ones. Production must use its own new key:

```bash
openssl rand -base64 48
```

- It must be at least 32 bytes, or the backend refuses to start. Tokens are signed with HMAC-SHA256.
- Never reuse the old key that used to be in `appsettings.json` (`ThisIsAVerySecure...`). It is still in the git history.
- Changing the key signs everyone out, because existing tokens stop being valid.
- Optional: `Jwt__ExpiresInMinutes` (default `120`) and `Jwt__GuestExpiresInMinutes` (default `360`) control how long logins last.

### Allowed origins (CORS)

```
Cors__AllowedOrigins__0=https://<your-frontend-domain>
```

- Outside Development, the backend **only** accepts browser requests from origins in this list. If it's empty, the frontend can't call the API at all.
- Use the full origin: scheme and host, with no trailing slash or path. Add one entry per domain (e.g. `www` and the bare domain).

### Public URLs

These must be the real frontend address, or emails and printed QR codes will link to `localhost`:

| Setting | Used for |
| --- | --- |
| `AuthEmail__FrontendBaseUrl` | Links in verification and password reset emails |
| `QRCodeSettings__SessionPageUrl` | The table Session QR codes |
| `Restaurant__BaseUrl` | Fallback for the QR session URL |

All three take the frontend origin, e.g. `https://sushitoshi.example.com`, with no trailing slash.

**After setting `SessionPageUrl`, regenerate and reprint every location's QR codes.** Saved codes keep the old URL. See [Table QR codes](#5-table-qr-codes) for the full steps.

## 2. Backend: integrations

| Integration | What to do |
| --- | --- |
| **Email (SendGrid)** | Set `SendGrid__ApiKey` and `SendGrid__FromEmail`, and authenticate your domain. See [email-sendgrid-setup.md](email-sendgrid-setup.md). |
| **Google Sign-In** | Add the production frontend origin under **Authorized JavaScript origins** on the Google OAuth client. `Google:ClientId` can stay as committed if you're using the same OAuth client. See [google-signin-setup.md](../dev/google-signin-setup.md). |
| **WiFi QR codes** | Set each location's guest network credentials. See [wifi-configuration.md](wifi-configuration.md). |
| **Holidays** | `HolidayApi__DefaultProvince` is `AB` (Alberta). Change it if the restaurant is in another province, since it decides which statutory holidays are loaded. |

## 3. Frontend

Next.js reads `NEXT_PUBLIC_*` variables **when you build**, not when it starts. Set them before running `npm run build`, and rebuild after changing them.

| Variable | Value |
| --- | --- |
| `NEXT_PUBLIC_GOOGLE_CLIENT_ID` | The same client ID as the backend's `Google:ClientId`. Without it, the Google button doesn't appear. |
| `NEXT_PUBLIC_API_URL` | The backend's origin, e.g. `https://api.sushitoshi.example.com`. See below. |

**How the frontend reaches the API:** if `NEXT_PUBLIC_API_URL` isn't set, the browser calls `/api/...` on the frontend's own domain. `next.config.mjs` then forwards those calls to `http://127.0.0.1:5264`. That only works when the backend runs on the same server on port 5264. If the backend is anywhere else, set `NEXT_PUBLIC_API_URL`, and add the frontend's origin to the backend's `Cors:AllowedOrigins`.

Build and start from `src/front-end`:

```bash
npm ci
npm run build
npm run start
```

Use `npm run start`, not `npm run dev`, in production.

## 4. Default accounts (security)

Outside Development, the backend doesn't create any of the sample accounts (admin, staff or customer), because their passwords are in the source code.

On an empty database, the first start still loads the **menu and restaurant setup**: the two sample locations, categories, tags, menus, menu items, tables and table groups. It doesn't load the sample dining sessions, orders, bills, service requests or menu views. Review the menu and setup data and change anything that isn't real before opening to customers. To start with none of it, set `Seeding:Enabled` to `false` (for example, the environment variable `Seeding__Enabled=false`) before the first start.

### First admin account

No admin is seeded in production, so the backend creates the first one from these settings when it starts and finds no admin in the database:

```bash
Bootstrap__AdminEmail=owner@your-restaurant.example
Bootstrap__AdminPassword=<at least 12 characters>
# Optional, default to "Admin" and "User"
Bootstrap__AdminFirstName=...
Bootstrap__AdminLastName=...
```

- Set the password in an environment variable or secret store, never in a committed file.
- Use an email that isn't registered yet. The backend won't turn an existing account into an admin.
- Once any admin exists, these settings are ignored, so they can't overwrite or take over an account. Still, **remove `Bootstrap__AdminPassword` after the first sign-in**, then change the password from your own row in **Manage Staff**.
- If a setting is missing or invalid (bad email, password too short or long, email already in use), the backend logs an error under `An error occurred during database initialization` and creates no admin. Fix it and restart.
- Add more admins and staff from **Manage Staff** once you're signed in.

## 5. Table QR codes

Each table's Session QR code has the site's address saved inside the image when it's generated. If codes are generated before `SessionPageUrl` is set, they open `http://localhost:3000/...`, which won't load on a customer's phone.

1. **Choose a domain you'll keep.** Printed codes can't be changed, so moving to a new domain later means reprinting every table.
2. **Set the site URL** on the production backend, with no trailing slash:

   ```
   QRCodeSettings__SessionPageUrl=https://<your-frontend-domain>
   ```

   Don't put it in `appsettings.json`. Local setups rely on the `http://localhost:3000` default.
3. **Set WiFi credentials** for each location's guest network. See [wifi-configuration.md](wifi-configuration.md).
4. **Restart the backend** so it reads the new values.
5. **Bulk generate before downloading anything.** In the admin app, go to **QR Codes**, pick a location, and click **Generate QR Codes for All N Tables**. Repeat for every location.
   - Bulk generation overwrites every table's saved image. Downloading a single code first would return the old saved image, with the old address.
   - Optional extra precaution: delete everything in `storage/qrcodes/` (under the folder the backend runs from) before generating.
6. **Test before printing.** Ask staff to start a dining session for one table. Then, with the phone's WiFi **turned off** so it can't reach anything on the local network:
   - Scan that table's Session code. It should open the real site's login page, not `localhost`.
   - Sign in. The page should show "Joining Table N…" and take you into that table's session.
   - Scan the table's WiFi code. The phone should offer to join the guest network.
7. **Print.** Click **Download as PDF** (16 codes per page, sized for letter paper) and print.

### Keep the legacy QR routes

Codes printed by earlier versions of the app use older links:

- `/join?table=4&location=1`
- `/start-session?tableNumber=4&locationId=1`

Both pages still exist in the frontend (`src/front-end/src/app/join/` and `src/front-end/src/app/start-session/`) and send customers on to the login page. **Don't remove them** while any of those older codes are still on tables.

## 6. Pre-launch checklist

**Backend**

- [ ] `ASPNETCORE_ENVIRONMENT=Production`
- [ ] `ConnectionStrings__MySqlConnection` points at the production database, with a strong password
- [ ] `Jwt__Key` is a new random secret, not the development key or the old key from git history
- [ ] `Cors__AllowedOrigins__0` (and more if needed) lists the frontend's origin
- [ ] `AuthEmail__FrontendBaseUrl`, `QRCodeSettings__SessionPageUrl` and `Restaurant__BaseUrl` are the real frontend URL
- [ ] SendGrid API key and sender are set, and the domain is authenticated
- [ ] WiFi credentials are set for every location
- [ ] `HolidayApi__DefaultProvince` matches the restaurant's province
- [ ] The backend is served over HTTPS

**Frontend**

- [ ] `NEXT_PUBLIC_GOOGLE_CLIENT_ID` and, if needed, `NEXT_PUBLIC_API_URL` were set **before** `npm run build`
- [ ] The production origin is authorized on the Google OAuth client
- [ ] Served over HTTPS

**After the first start**

- [ ] Every seeded account's password is changed, or the account is deactivated
- [ ] A real admin account exists
- [ ] Sample data is reviewed and cleaned up
- [ ] QR codes are bulk generated for every location, a test scan works with the phone's WiFi off, and the codes are printed
- [ ] Test: register, verify email, reset password, Google sign-in, scan a Session QR code and a WiFi QR code
