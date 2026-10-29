# WiFi Configuration Guide

Each restaurant location can have its own guest WiFi network. The WiFi QR codes printed for that location's tables use these credentials, so customers can join the network by scanning.

For how the QR codes are generated and stored, see the [QR code service README](../../src/back-end/Services/QrCode/README.md).

## How the backend picks credentials

For a location with ID `N`, the backend uses the first of these that has **both** an SSID and a password:

1. `QRCodeSettings:Locations:N:WiFi` (that location's own network)
2. `QRCodeSettings:WiFi` (the default for every location)

If neither is set, generating a WiFi QR code for that location fails with "Location N not found or WiFi credentials not configured".

Location IDs come from the database. You can find them on the admin **Manage Locations** page.

## Where to put the credentials

`src/back-end/appsettings.json` only holds placeholders (`DefaultWifi`, `Location-Test`). **Never put real WiFi passwords in it**, because it's committed to git.

### Development: user secrets

From `src/back-end`:

```bash
dotnet user-secrets set "QRCodeSettings:Locations:1:WiFi:SSID" "<network name>"
dotnet user-secrets set "QRCodeSettings:Locations:1:WiFi:Password" "<password>"
dotnet user-secrets list   # confirm they're set
```

Change the `1` to the location's ID. To set the default for all locations instead, leave out `Locations:1:`, e.g. `QRCodeSettings:WiFi:SSID`.

### Production: environment variables

Use double underscores in place of colons:

```bash
# Linux/Mac
export QRCodeSettings__Locations__1__WiFi__SSID="<network name>"
export QRCodeSettings__Locations__1__WiFi__Password="<password>"

# Windows PowerShell
$env:QRCodeSettings__Locations__1__WiFi__SSID="<network name>"
$env:QRCodeSettings__Locations__1__WiFi__Password="<password>"
```

On a hosting platform, set the same names in its environment or secrets settings.

## Changing a location's WiFi

1. Set the new SSID and password (see above).
2. **Restart the backend.** Settings are only read when it starts.
3. Regenerate that location's QR codes. The credentials are baked into each image, and saved images are reused until regenerated.
   - In the admin app: **QR Codes** → pick the location → **Generate QR Codes for All N Tables**. This overwrites the old images.
   - Then download the new PDF or ZIP and reprint.

## Check that it works

1. In **QR Codes**, pick the location, enter a table number and click **Preview** on the WiFi card.
2. Scan the code with a phone camera. It should offer to join the network you set.
3. If the phone already knows that network, forget it first, or scanning appears to do nothing.

## Troubleshooting

| Problem | Fix |
| --- | --- |
| Every location shows the same network | That location has no `Locations:N:WiFi` entry, so it uses the default. Check the location ID, then restart the backend. |
| QR code still has the old network | Restart the backend, then regenerate. Bulk generation always overwrites; for one table, the API also accepts `&refresh=true`. |
| "WiFi credentials not configured" error | Neither the location's entry nor the default has both an SSID and a password. |
| Phone can't join from the code | Check the SSID and password match the router exactly. The codes always use WPA/WPA2 and mark the network as visible, so hidden networks and WPA3-only routers may not work. |

## Security

- **WiFi QR codes reveal the password** to anyone who scans them. Only use a guest network, never the staff or POS network.
- **Use a different password for each location**, and change them now and then. After changing one, regenerate and reprint that location's codes.
- **Generated images contain the password.** They're saved in `src/back-end/storage/qrcodes/`, which git ignores. On a server, restrict who can read that folder.
