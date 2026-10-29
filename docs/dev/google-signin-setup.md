# Setting Up Google Sign-In

## Overview

The "Sign in with Google" button only shows on the login page when your frontend has `NEXT_PUBLIC_GOOGLE_CLIENT_ID` set. That variable lives in `src/front-end/.env.local`, which is gitignored, so a fresh clone has no button and shows no error.

You need three things:

1. The frontend client ID in `src/front-end/.env.local`.
2. The same client ID in the backend's `Google:ClientId` (already committed in `appsettings.json`).
3. The URL you open the app from listed as an authorized origin on the Google OAuth client.

The button is rendered in `src/front-end/src/components/auth/LoginForm.jsx`. It sends Google's ID token to `POST /api/auth/google` in `src/back-end/Controllers/AuthController.cs`, which checks it and returns our JWT.

## 1. Frontend: add the client ID

1. Open `src/back-end/appsettings.json` and copy the value of `Google` → `ClientId`. It ends in `.apps.googleusercontent.com`.
2. In `src/front-end`, create `.env.local` if you don't have one. Add this line, with no quotes or spaces:

   ```
   NEXT_PUBLIC_GOOGLE_CLIENT_ID=<the ClientId you copied>
   ```

3. Stop and restart `npm run dev`. Next.js only reads `NEXT_PUBLIC_*` variables when the dev server starts. If the button still doesn't appear, run `rm -rf .next` and start it again.

Never commit `.env.local`. The `.gitignore` already excludes `.env*.local`. The client ID itself isn't a secret, since Google shows it to every browser that loads the button.

## 2. Backend: check the matching client ID

The backend needs no changes for most people, because `Google:ClientId` is already in `src/back-end/appsettings.json`. It uses that value as the expected audience when checking Google's token.

- The frontend and backend IDs must match exactly. If they don't, every sign-in fails with "Invalid Google token" (401).
- If you've overridden it in user secrets, check it by running `dotnet user-secrets list` from `src/back-end`.
- After you change backend config, restart it with `dotnet run --launch-profile http`.

## 3. Authorized origins

Google only lets the button work on origins listed on our OAuth client. Any other origin gets a 403 or an "origin not allowed" popup.

| Where you open the app | Origin to authorize | Notes |
| --- | --- | --- |
| Your computer | `http://localhost:3000` | Should already be on the client |
| Your phone via ngrok | `https://<your-name>.ngrok-free.dev` | Each person's ngrok domain is different, so add yours once |

To add an origin, someone with access to the project in Google Cloud Console opens the OAuth 2.0 Client ID, adds it under **Authorized JavaScript origins**, and saves. Use no trailing slash and no path. The change can take a few minutes to start working.

> **TODO:** Confirm who on the team has Console access to add origins. The Google sign-in code was added by Tim Torpy in commit `3e0446c`.

## 4. Check that it works

- The login page shows an "or" divider and a Google button under **Sign In**.
- Clicking it opens Google's account picker without an origin error.
- After you pick an account, you land signed in. A first-time Google sign-in creates a new customer account.
- Optional: the same steps work on your phone through your ngrok URL.

## Troubleshooting

| Symptom | Cause | Fix |
| --- | --- | --- |
| No Google button or "or" divider | `NEXT_PUBLIC_GOOGLE_CLIENT_ID` is missing, or the dev server started before you added it | Do step 1, then restart `npm run dev` (and run `rm -rf .next` if needed) |
| 403 or "origin not allowed" when clicking the button | The URL you're on isn't an authorized origin | Do step 3 |
| "Invalid Google token" (401) | Frontend and backend client IDs don't match | Make both match `appsettings.json` (steps 1 and 2) |
| "Google sign-in is not available" (500) | Backend has no `Google:ClientId` | Check `appsettings.json` and your user secrets, then restart the backend |
| "Authentication failed" (500) | Any other backend error, such as the database not running | Check the backend console for a line starting with `Google login error:` |
| "Google email is not verified" (403) | The Google account's email isn't verified | Use a verified Google account |
| "Account is not active" (403) | The matching user in our database is deactivated | Reactivate the user, or sign in with a different account |
| "Google login failed." with no request to the backend | Google's popup was closed or blocked | Allow popups for the site and try again |
