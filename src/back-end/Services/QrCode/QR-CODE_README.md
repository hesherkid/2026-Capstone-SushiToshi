# QR Code Generation Service

## Overview

This service generates location-based QR codes for **WiFi access** and **table login** (session start). It uses QRCoder for QR encoding and SkiaSharp for rendering, so it runs on Windows, Linux and macOS.

- All endpoints require the **`Admin`** role.
- Developer setup for testing on a phone (ngrok, user secrets): see [`TEAM_SETUP.md`](../../../../TEAM_SETUP.md).

## Folder Structure

```
src/back-end/
├── Services/QrCode/
│   ├── QrGeneratorService.cs         # Core QR generation logic (namespace back_end.Services)
│   └── README.md                      # This file
├── Controllers/
│   └── AdminQrCodeController.cs      # Admin-only endpoints
└── storage/qrcodes/                   # Generated PNGs (created automatically, ignored by git)
```

## Admin API Endpoints

All endpoints require a JWT for a user with the `Admin` role (`[Authorize(Roles = "Admin")]`).

| Method | Endpoint                          | Purpose                                                   |
| ------ | --------------------------------- | --------------------------------------------------------- |
| GET    | `/api/admin/qr/wifi`              | Single WiFi QR (PNG)                                      |
| GET    | `/api/admin/qr/session`           | Single session/login QR (PNG)                             |
| POST   | `/api/admin/qr/bulk`              | Generate WiFi + session QRs for every table at a location |
| GET    | `/api/admin/qr/bulk/download-zip` | All saved QRs for a location as a ZIP                     |
| GET    | `/api/admin/qr/bulk/download-pdf` | All saved QRs for a location as a printable PDF           |
| DELETE | `/api/admin/qr/clear`             | Delete a table's saved QRs                                |

### 1. Get WiFi QR Code

```http
GET /api/admin/qr/wifi?locationId={locationId}&table={tableNumber}[&refresh=true]
```

**Parameters:**

- `locationId` (int, required): Location ID
- `table` (int, required): Table number
- `refresh` (bool, optional, default `false`): regenerate from current settings instead of returning the saved image
  **Response:** PNG image, filename `wifi_L{locationId}_T{table}.png`

**Example:**

```bash
curl -H "Authorization: Bearer {token}" \
  "http://localhost:5264/api/admin/qr/wifi?locationId=1&table=5&refresh=true" \
  --output wifi_L1_T5.png
```

### 2. Get Session QR Code

```http
GET /api/admin/qr/session?locationId={locationId}&table={tableNumber}[&refresh=true]
```

**Parameters:** same as WiFi.

**Response:** PNG image, filename `session_L{locationId}_T{table}.png`. Scanning it opens the login page for that table (see [Session URL Format](#session-url-format)).

**Example:**

```bash
curl -H "Authorization: Bearer {token}" \
  "http://localhost:5264/api/admin/qr/session?locationId=1&table=5" \
  --output session_L1_T5.png
```

### 3. Bulk Generate QR Codes

```http
POST /api/admin/qr/bulk?locationId={locationId}
```

**Parameters:**

- `locationId` (int, required): Location ID
  Tables are read from the database for that location (no `tableCount` parameter). Every table gets a WiFi and a session QR, and existing files are always **overwritten**.

**Response:**

```json
{
  "success": true,
  "locationId": 1,
  "tableCount": 20,
  "filesGenerated": 40,
  "storageLocation": "storage/qrcodes/",
  "files": ["wifi_L1_T1.png", "session_L1_T1.png", "..."]
}
```

**Errors:**

- `404`: location not found
- `400`: location has no tables
  **Example:**

```bash
curl -X POST -H "Authorization: Bearer {token}" \
  "http://localhost:5264/api/admin/qr/bulk?locationId=1"
```

### 4. Download All (ZIP / PDF)

```http
GET /api/admin/qr/bulk/download-zip?locationId={locationId}
GET /api/admin/qr/bulk/download-pdf?locationId={locationId}
```

- Both use the **saved** files, so run bulk generate first.
- **ZIP:** individual PNGs.
- **PDF:** US Letter pages, 16 QR codes each (4 × 4), each QR printed at 1.75" × 1.75" with a "Table N" and "WiFi"/"Session" label.

### 5. Clear Saved QR Codes

```http
DELETE /api/admin/qr/clear?locationId={locationId}&table={tableNumber}
```

**Response:**

```json
{
  "success": true,
  "message": "Cleared QR codes for Location 1, Table 5"
}
```

## Key Features

### Location-Based WiFi Credentials

Each location can have its own WiFi credentials (`QrGeneratorService.GetLocationWifiCredentials`):

1. **First:** `QRCodeSettings:Locations:{locationId}:WiFi` (both SSID and Password must be set)
2. **Fallback:** `QRCodeSettings:WiFi`
3. **If neither is set:** the WiFi endpoint returns `404`.
   Values can come from `appsettings.json`, user secrets (development), or environment variables (production, e.g. `QRCodeSettings__Locations__1__WiFi__Password`).
   **Configuration Example:**

```json
"QRCodeSettings": {
  "WiFi": {
    "SSID": "DefaultWiFi",         // Fallback for all locations
    "Password": "defaultpass"
  },
  "Locations": {
    "1": {
      "WiFi": {
        "SSID": "Location1-WiFi",  // Location 1 specific
        "Password": "location1pass"
      }
    }
  }
}
```

> **📖 For detailed WiFi configuration instructions, see:** [`WIFI_CONFIGURATION_GUIDE.md`](../../../../WIFI_CONFIGURATION_GUIDE.md)

### File Caching

Generated QR codes are saved to disk and reused:

- **Storage Location:** `storage/qrcodes/` under the folder the backend runs from (normally `src/back-end/storage/qrcodes/`). Created automatically on startup.
- **Filenames:** `wifi_L{locationId}_T{table}.png`, `session_L{locationId}_T{table}.png`
- **Cache Behavior:**
  - `GET /api/admin/qr/wifi` and `/session` return the saved file if one exists; add `&refresh=true` to regenerate from current settings
  - `POST /api/admin/qr/bulk` always regenerates and overwrites
  - `DELETE /api/admin/qr/clear` removes a table's saved files
  - Responses send `Cache-Control: private, no-cache`, so browsers re-check instead of reusing old images
- **After changing `SessionPageUrl` or WiFi settings:** restart the backend, then regenerate. Values are baked into the image.
  **Note:** `storage/qrcodes/` is ignored by git. Generated files are not committed to version control.

### Labeled QR Codes

Each PNG has a label strip below the QR code with the location name (from the database), the table number, and the type:

```

Location Name — Table 5 — WiFi
Location Name — Table 5 — Menu

```

### Security Notes

1. **Admin only:** all endpoints require the `Admin` role.
2. **WiFi passwords:** never returned as text by the API, but they **are encoded in the WiFi QR image**. Anyone who can scan the code can read the password. Print them only for guest networks.
3. **Not cached by shared proxies:** responses use `Cache-Control: private, no-cache`.
4. **Session QR codes are public by design:** anyone at the table can scan them.
5. **Never commit real credentials:** keep placeholders in `appsettings.json`.

## Configuration

### appsettings.json

Committed values are placeholders. Real values go in user secrets (dev) or environment variables (production); see `TEAM_SETUP.md`, step 6.

```json
{
  "QRCodeSettings": {
    "WiFi": {
      "SSID": "Location-Test",
      "Password": "Location-Password"
    },
    "SessionPageUrl": "http://localhost:3000",
    "Locations": {
      "1": {
        "WiFi": {
          "SSID": "Location-Test",
          "Password": "Location-Password"
        }
      }
    }
  }
}
```

- `SessionPageUrl` is the frontend's base URL only (no path, no trailing slash). The service appends `/auth/login?locationId={id}&tableNumber={n}`.
  - If it's missing, the service falls back to `Restaurant:BaseUrl`, then `http://localhost:3000`.
- A location's own `WiFi` entry overrides the default `WiFi`.

### Service Registration (Program.cs)

```csharp
builder.Services.AddScoped<QrGeneratorService>(); // namespace back_end.Services
```

## Usage Examples

### C# Client Example

```csharp
using System.Net.Http.Headers;

// Download a WiFi QR code (requires an Admin JWT)
using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", jwtToken);

// Add &refresh=true to regenerate from current settings instead of using the saved image
var response = await httpClient.GetAsync(
    "http://localhost:5264/api/admin/qr/wifi?locationId=1&table=5&refresh=true");

if (response.IsSuccessStatusCode)
{
    var bytes = await response.Content.ReadAsByteArrayAsync();
    await File.WriteAllBytesAsync("wifi_qr.png", bytes);
}
```

### JavaScript/Fetch Example

Relative `/api/...` paths work in the frontend because Next.js forwards `/api/*` to the backend (`next.config.mjs` rewrite).

```javascript
// Download Session QR code
async function downloadSessionQR(locationId, table) {
  const response = await fetch(
    `/api/admin/qr/session?locationId=${locationId}&table=${table}`,
    {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    },
  );

  if (response.ok) {
    const blob = await response.blob();
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `session_L${locationId}_T${table}.png`;
    a.click();
    window.URL.revokeObjectURL(url);
  }
}
```

### Bulk Generation Example

```javascript
// Generate QR codes for every table at a location
async function generateAllQRCodes(locationId) {
  const response = await fetch(`/api/admin/qr/bulk?locationId=${locationId}`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${token}`,
    },
  });

  const result = await response.json();
  console.log(
    `Generated ${result.filesGenerated} QR codes for ${result.tableCount} tables`,
  );
}

// Usage
await generateAllQRCodes(1);
```

## WiFi QR Code Format

WiFi QR codes use the standard format, generated by QRCoder's `PayloadGenerator.WiFi`:

```
WIFI:T:WPA;S:{ssid};P:{password};;
```

- `T`: authentication type. The service always uses `WPA`, which phones treat as WPA/WPA2.
- `S`: SSID (network name)
- `P`: password
- The network is always marked as **not hidden**, and QRCoder escapes special characters (`; , : \ "`) automatically.
  When scanned with a phone camera, this prompts the user to join the network (tap **Join** on iPhone, **Connect** on Android).

**Limitations:** routers set to WPA3-only, or with a hidden SSID, may not join from these codes.

## Session URL Format

Session QR codes link to:

```
{SessionPageUrl}/auth/login?locationId={locationId}&tableNumber={tableNumber}
```

> **Note:** the session QR flow is under review.

**Example:**

```
https://<your-name>.ngrok-free.dev/auth/login?locationId=1&tableNumber=5
```

> **Note:** earlier versions linked to `/start-session?...`, which created a dining session and redirected to the menu. The service now links to `/auth/login`.

## Troubleshooting

### QR Code Not Generating

1. **Check the database:** the location (and, for bulk, its tables) must exist.
2. **Check configuration:** WiFi credentials must be set (see [Location-Based WiFi Credentials](#location-based-wifi-credentials)). A `404` from `/wifi` usually means they're missing.
3. **Check permissions:** the user needs the `Admin` role.
4. **Check logs:** look for errors in the backend console.

### QR Shows Old Values

- **Saved file:** use `&refresh=true` or bulk generate.
- **Browser cache:** in DevTools → Network, if the request's **Size** column shows `(disk cache)`, right-click → **Clear browser cache** and tick **Disable cache**.
- **Settings not picked up:** restart the backend after changing user secrets or `appsettings.json`.

### Clearing Saved QR Codes

For a specific table:

```bash
curl -X DELETE -H "Authorization: Bearer {token}" \
  "http://localhost:5264/api/admin/qr/clear?locationId=1&table=5"
```

For all tables, run from `src/back-end`:

```bash
# Windows (cmd)
del /Q "storage\qrcodes\*.png"

# Linux/Mac/Git Bash
rm -f storage/qrcodes/*.png
```

## Best Practices

1. **Regenerate after any settings change:** bulk generate, or `&refresh=true` for a single code.
2. **Print table tents:** bulk generate, then download the PDF.
3. **Guest network only:** WiFi QR codes reveal the password, so never encode a staff or office network.
4. **Separate networks per location:** use `QRCodeSettings:Locations:{id}:WiFi`.
5. **Secure storage:** `storage/qrcodes/` contains WiFi passwords inside images; restrict file permissions on servers.
6. **Don't commit QR codes or credentials:** `storage/qrcodes/` is git-ignored; keep `appsettings.json` on placeholders.

---
