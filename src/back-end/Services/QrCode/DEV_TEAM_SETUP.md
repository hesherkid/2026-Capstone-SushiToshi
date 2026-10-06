# Team Setup: Testing QR Codes on Your Phone

This guide gets the table QR code flow (scan → login → menu) working on **your own machine and your own phone**.
Each teammate runs their own tunnel, so you can test your own local changes from anywhere.

---

## How it works

```
Phone ──scan QR──▶ https://<your-name>.ngrok-free.dev
                         │  (ngrok tunnel)
                         ▼
               Next.js dev server  :3000
                         │  /api/* is forwarded by the rewrite in next.config.mjs
                         ▼
               .NET backend        :5264  (http://127.0.0.1:5264)
```

- The phone only ever talks to **one address** (your ngrok URL).
- The browser calls the API with relative paths (`/api/...`), and Next.js forwards them to .NET.
  - This means no CORS errors and no "access other apps / local network" prompt on the phone.
- The QR code contains `{SessionPageUrl}/auth/login?locationId={id}&tableNumber={n}`.
  - Source: `QrGeneratorService.GetSessionUrl()`.

---

## Prerequisites

- .NET SDK and Node.js installed. The project already builds and runs locally for you.
- MySQL/MariaDB running on the port in your connection string (default `3307`).
- You can log in at `http://localhost:3000` on your computer.
- An admin account, needed to generate QR codes.

---

## One-time setup

### 1. Pull the latest code

The changes that make phone testing work are already in the repo:

| File                              | What it does                                                                                |
| --------------------------------- | ------------------------------------------------------------------------------------------- |
| `src/front-end/src/config/api.js` | In the browser, API calls use relative `/api/...` paths unless `NEXT_PUBLIC_API_URL` is set |
| `src/front-end/next.config.mjs`   | Rewrites `/api/*` → `http://127.0.0.1:5264/api/*` and allows `*.ngrok-free.dev` in dev      |

### 2. Clean up frontend env files

From `src/front-end`, check that no env file forces the old backend address:

```bash
grep -n "NEXT_PUBLIC_API_URL" .env* 2>/dev/null
```

- If a line sets it to `http://localhost:5264`, delete it or comment it out.
- If nothing prints, you're fine.

Then clear the Next.js cache:

```bash
rm -rf .next
```

### 3. Create your own ngrok account

- Sign up at <https://ngrok.com/signup>. The free plan is fine.
- **Do not share authtokens.** The free plan allows one user per account, and two people on the same account will collide.

### 4. Install ngrok

| OS                                               | Command                                                                                    |
| ------------------------------------------------ | ------------------------------------------------------------------------------------------ |
| Windows (Chocolatey, **Administrator** terminal) | `choco install ngrok`                                                                      |
| Windows (no Chocolatey)                          | Download from <https://ngrok.com/download>, extract `ngrok.exe` into a folder on your PATH |
| macOS                                            | `brew install ngrok`                                                                       |
| Linux                                            | See <https://ngrok.com/docs/getting-started>                                               |

Close and reopen your terminal, then confirm it installed:

```bash
ngrok version
```

> **Antivirus warning:** Malwarebytes and some other antivirus tools flag ngrok as "RiskWare" because tunnels can be misused.
> This is expected for a genuine install. Before you restore it from quarantine, verify the signature in PowerShell:
>
> ```powershell
> Get-AuthenticodeSignature "C:\ProgramData\chocolatey\lib\ngrok\tools\ngrok.exe" | Format-List Status, SignerCertificate
> ```
>
> - You should see `Status : Valid` and a signer of `CN="ngrok, Inc."`.
> - If so, restore it and allow-list **only that one file**.
> - If it's on a work-managed machine, ask IT first.

### 5. Connect ngrok to your account

- Copy your token from <https://dashboard.ngrok.com/get-started/your-authtoken>, then run:

  ```bash
  ngrok config add-authtoken <YOUR_TOKEN>
  ```

- Your free account comes with **one permanent dev domain**, like `https://something-something.ngrok-free.dev`.
  - You'll see it when you start ngrok (step 7).
  - It's also listed at <https://dashboard.ngrok.com/domains>.

### 6. Point QR codes at your ngrok URL (user secrets, never committed)

- `appsettings.json` keeps `"SessionPageUrl": "http://localhost:3000"` for everyone.
- You override it **on your machine only** with .NET user secrets. From `src/back-end`, run:

  ```bash
  dotnet user-secrets init          # first time only; safe if already done
  dotnet user-secrets set "QRCodeSettings:SessionPageUrl" "https://<your-name>.ngrok-free.dev"
  dotnet user-secrets list          # confirm it's there
  ```

- No trailing slash on the URL.
- User secrets apply only in the Development environment, which is what the `http` launch profile uses.
- **Restart the backend after setting or changing a secret.** Settings are read when the backend starts. If it was already running, new QR codes will still use the old value (`http://localhost:3000`).
- **WiFi QR credentials:** `appsettings.json` has placeholders (`Location-Test` / `Location-Password`), which are enough to generate and scan WiFi QR codes.
  - To test actually **joining** a network, set your own SSID and password in user secrets (never in `appsettings.json`), restart the backend, then regenerate (bulk, or `&refresh=true`):

    ```bash
        dotnet user-secrets set "QRCodeSettings:Locations:1:WiFi:SSID" "<your network name>"
        dotnet user-secrets set "QRCodeSettings:Locations:1:WiFi:Password" "<your password>"
    ```

  - A location's own entry (`Locations:{id}:WiFi`) overrides the default (`WiFi`).
  - **Forget** the network on your phone first, or scanning appears to do nothing.
  - Production credentials are set as server environment variables (e.g. `QRCodeSettings__WiFi__Password`), never committed.

---

## Every testing session

### 7. Start everything in this order

Use three terminals.

**Backend**, from `src/back-end`:

```bash
dotnet run --launch-profile http
```

Wait for `Now listening on: http://0.0.0.0:5264`.
Also check that the console shows `Hosting environment: Development`. If it doesn't, your user secrets won't load.

**Frontend**, from `src/front-end`:

```bash
npm run dev
```

**Tunnel:**

```bash
ngrok http 3000
```

Keep all three running while you test. If ngrok stops, the phone will show `ERR_NGROK_3200`.

### 8. Regenerate the QR codes

Do this the first time, and any time `SessionPageUrl` or WiFi settings change. The values are baked into the image, and old images are saved on disk (`src/back-end/storage/qrcodes/`).
**Make sure the backend was restarted after your last user-secrets change** before you regenerate.

1. Open Swagger at `http://localhost:5264/swagger`.
2. Call `POST /api/Auth/login` with an admin account and copy the token.
3. Click **Authorize** and paste the token.
4. Call `POST /api/admin/qr/bulk?locationId=1`. This regenerates and overwrites every table's QR code.
5. Optionally, get printable copies:
   - `GET /api/admin/qr/bulk/download-pdf?locationId=1`
   - `GET /api/admin/qr/session?locationId=1&table=1` or `GET /api/admin/qr/wifi?locationId=1&table=1` for a single code

**Single-code endpoints and `refresh`:** `GET /qr/wifi` and `GET /qr/session` return the **saved** image if one exists.

- Add `&refresh=true` to regenerate it from your current settings.
- Bulk (step 4) always regenerates.

> **Browser cache gotcha:** before the `refresh` change, these endpoints told browsers to cache images for 24 hours. A browser may still show an **old** image without contacting the backend at all. You can tell because the DevTools Network **Size** column shows `(disk cache)` and no new file appears in `storage/qrcodes/`.
> Fix: DevTools → Network → right-click → **Clear browser cache**, and tick **Disable cache** while testing. New responses send `Cache-Control: private, no-cache`, so this won't recur once old copies are gone.

### 9. Test on your phone

1. Open `https://<your-name>.ngrok-free.dev` in your phone's browser.
2. Tap **Visit Site** on ngrok's free-plan warning page. It's remembered for a while.
3. Scan the QR code and log in.

---

## Verification checklist

On your computer, open your **ngrok URL** (not localhost). Then open DevTools → **Network** → **Fetch/XHR**, and press **Sign in**.

- [ ] The POST `login` request URL is `https://<your-name>.ngrok-free.dev/api/auth/login`. It should **not** be `localhost:5264`.
- [ ] Status is `200`.
- [ ] Scanning the QR code on your phone opens your ngrok URL at `/auth/login?locationId=...&tableNumber=...`, **not** `localhost`.
- [ ] Optional: `GET /api/admin/qr/bulk/download-pdf?locationId=1` shows each QR with one centered "Table N" line and one "WiFi"/"Session" line.
- [ ] Logging in on the phone works.

---

## Troubleshooting

| Symptom                                                                                                                          | Cause                                                                                                                  | Fix                                                                                                                                                                                                                                                |
| -------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ngrok: command not found`                                                                                                       | ngrok not installed, or terminal not restarted                                                                         | Step 4, then open a new terminal                                                                                                                                                                                                                   |
| `ERR_NGROK_3200 – endpoint is offline`                                                                                           | ngrok isn't running, or the QR code points to someone else's ngrok URL                                                 | Run `ngrok http 3000`. Check your user secret, then regenerate (step 8)                                                                                                                                                                            |
| QR code opens `http://localhost:3000/...`                                                                                        | Your user secret isn't being applied                                                                                   | Run `dotnet user-secrets list` in `src/back-end` to confirm `QRCodeSettings:SessionPageUrl`. Check the console shows `Hosting environment: Development`. **Restart the backend** with `dotnet run --launch-profile http`, then regenerate (step 8) |
| QR code opens an old or wrong URL                                                                                                | Saved PNG in `storage/qrcodes/`                                                                                        | Regenerate with `POST /api/admin/qr/bulk`, or add `&refresh=true` to the single-code endpoint                                                                                                                                                      |
| Single downloaded QR shows old values (e.g. `SushiToshi-Downtown-WiFi`), but bulk/PDF is correct                                 | Browser cache is serving an old image. DevTools Size shows `(disk cache)` and no file is written to `storage/qrcodes/` | DevTools → Network → right-click → **Clear browser cache**, tick **Disable cache**, download again                                                                                                                                                 |
| WiFi QR shows the wrong network name                                                                                             | A location's own entry (`Locations:{id}:WiFi`) overrides the default `WiFi`                                            | Edit or override the `Locations:{id}` keys (step 6), then regenerate                                                                                                                                                                               |
| WiFi QR shows the right name but won't connect                                                                                   | Phone already on that network; prompt not tapped; router set to WPA3-only or hidden network                            | Forget the network on the phone; tap **Join** (iPhone) / **Connect** (Android); use WPA2 or WPA2/WPA3 mixed                                                                                                                                        |
| Can't find generated QR files                                                                                                    | Files are saved under the folder the backend was **started from**                                                      | Start the backend from `src/back-end`; files go to `src/back-end/storage/qrcodes/`                                                                                                                                                                 |
| `dotnet user-secrets list` says "No secrets configured" or mentions `UserSecretsId`                                              | Secrets were saved for a different project, or `init` was never run                                                    | Run `dotnet user-secrets init` and `set` again from `src/back-end`                                                                                                                                                                                 |
| Phone: "Network Error" on login, or "Login failed, check your credentials"                                                       | Frontend is still calling `http://localhost:5264`                                                                      | Step 2, then `rm -rf .next` and restart `npm run dev`                                                                                                                                                                                              |
| Phone asks to "access other apps / devices on your local network"                                                                | Same as above: the page is calling a local address                                                                     | Fix the cause above, then reset the site's permissions in Chrome (tap the icon left of the URL → Permissions → Reset)                                                                                                                              |
| `/api/...` returns a Next.js 404 page                                                                                            | Rewrite not loaded                                                                                                     | Check `next.config.mjs` is saved and restart Next                                                                                                                                                                                                  |
| `/api/...` returns 500, Next terminal shows `ECONNREFUSED`                                                                       | Backend not running, or not on port 5264                                                                               | Start the backend with `--launch-profile http`                                                                                                                                                                                                     |
| Pages show `AxiosError: Request failed with status code 500` right after a backend rebuild, with nothing in the backend terminal | Next's proxy couldn't reach .NET while it was restarting                                                               | Wait for `Now listening on`, then hard-refresh (Ctrl+Shift+R). Confirm the backend directly at `http://localhost:5264/api/Location`                                                                                                                |
| Google "Sign in with Google" button returns 403                                                                                  | Your ngrok URL isn't an authorized origin on the Google OAuth client                                                   | Use email/password for now                                                                                                                                                                                                                         |
| Antivirus quarantines ngrok                                                                                                      | Expected "RiskWare" detection                                                                                          | See the warning box in step 4                                                                                                                                                                                                                      |

**Quick proxy test:** open `https://<your-name>.ngrok-free.dev/api/Auth/login` in a browser.

- A **405** or blank page means the proxy reaches .NET. This is good, because that route only accepts POST.
- A **Next.js 404** means the rewrite isn't active.

---

## Changelog

- Phone testing via ngrok + Next.js `/api` proxy (`api.js`, `next.config.mjs`).
- `SessionPageUrl` moved to per-developer user secrets; `appsettings.json` defaults to `http://localhost:3000`.
- WiFi QR credentials in `appsettings.json` replaced with placeholders (`DefaufltWifi` | `Location-Test` / `DefaultPass` | `Location-Password`).
- `AdminQrCodeController.GetWifiQr` / `GetSessionQr`: added an optional `refresh` query parameter. Caching is now handled by the service, and `Cache-Control` changed from `public, max-age=86400` to `private, no-cache` (the WiFi images contain the network password).
- `AdminQrCodeController.DownloadBulkPdf` updated to the current SkiaSharp text API (`SKFont` + `SKTextAlign.Center`). This removed 24 `CS0618` warnings and a duplicated label.

---

## Rules of the road

- **Never commit** your ngrok URL, authtoken, or user secrets. Keep `appsettings.json` at `http://localhost:3000`.
- **Never commit** generated QR images. `storage/qrcodes/` is in `.gitignore`.
- **Stop ngrok (Ctrl+C) when you're not testing.** While it runs, your dev server is reachable from the internet.
- The free ngrok plan has monthly request and bandwidth limits. That's plenty for testing, but don't leave it running for days.

---

## References

- ngrok getting started: <https://ngrok.com/docs/getting-started>
- ngrok `ERR_NGROK_3200`: <https://ngrok.com/docs/errors/err_ngrok_3200>
- ngrok free plan limits and interstitial: <https://ngrok.com/docs/pricing-limits/free-plan-limits>
- .NET user secrets: <https://learn.microsoft.com/aspnet/core/security/app-secrets>
- ASP.NET Core configuration: <https://learn.microsoft.com/aspnet/core/fundamentals/configuration>
- Next.js rewrites: <https://nextjs.org/docs/app/api-reference/config/next-config-js/rewrites>
- Chrome Local Network Access: <https://developer.chrome.com/blog/local-network-access>
