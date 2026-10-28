# WiFi Configuration Guide - Quick Reference

> **Note:** For complete API documentation, technical details, and usage examples, see [`src/back-end/Services/QrCode/README.md`](../../src/back-end/Services/QrCode/README.md)

## Overview

This guide focuses on **configuring WiFi credentials** for your restaurant locations. Each location can have unique WiFi networks, and QR codes automatically use the correct credentials.

---

## Configuration File

**Location:** `src/back-end/appsettings.json`

---

## Current Configuration

### Structure:

```json
"QRCodeSettings": {
  "WiFi": {
    "SSID": "Redsox-5G",              // ← Default fallback
    "Password": "bpjh38vw83"
  },
  "SessionPageUrl": "http://192.168.1.78:3000",
  "Locations": {
    "1": {
      "WiFi": {
        "SSID": "SushiToshi-Downtown-WiFi",      // ← Location 1
        "Password": "downtown2025!"
      }
    },
    "2": {
      "WiFi": {
        "SSID": "SushiToshi-WestEnd-WiFi",       // ← Location 2
        "Password": "westend2025!"
      }
    },
    "3": {
      "WiFi": {
        "SSID": "SushiToshi-Southside-WiFi",     // ← Location 3
        "Password": "southside2025!"
      }
    }
  }
}
```

---

## Quick Reference Table

| Location ID | Location Name | WiFi SSID | WiFi Password | Tables |
|-------------|---------------|-----------|---------------|--------|
| 1 | Downtown | SushiToshi-Downtown-WiFi | downtown2025! | 1-20 |
| 2 | West End | SushiToshi-WestEnd-WiFi | westend2025! | 1-15 |
| 3 | Southside | SushiToshi-Southside-WiFi | southside2025! | 1-25 |
| *Fallback* | *Default* | Redsox-5G | bpjh38vw83 | *All* |

---

## How to Update WiFi Credentials

### Step-by-Step:

1. **Open `appsettings.json`**
   - Path: `src/back-end/appsettings.json`
   - Find the `QRCodeSettings` section (line ~29)

2. **Update WiFi credentials:**
   ```json
   "1": {
     "WiFi": {
       "SSID": "YourNewSSID",         // ← Change this
       "Password": "YourNewPassword"   // ← Change this
     }
   }
   ```

3. **Restart the backend:**
   ```bash
   cd src/back-end
   dotnet run
   ```

4. **Clear QR code cache:**
   - Option A: Admin UI → `/admin/qr-codes` → Click "Clear Cache"
   - Option B: API → `DELETE /api/admin/qr/clear?locationId=1&table=5`

5. **Regenerate QR codes:**
   - Individual: Download from admin UI
   - Bulk: Use "Generate Bulk QR Codes" button

---

## Adding a New Location

If you add Location 4:

```json
"Locations": {
  "1": { ... },
  "2": { ... },
  "3": { ... },
  "4": {                              // ← Add this
    "WiFi": {
      "SSID": "SushiToshi-NewLocation-WiFi",
      "Password": "newlocation2025!"
    }
  }
}
```

Then restart backend and generate QR codes.

---

## Production Deployment

### Option 1: Environment Variables (Recommended)

```bash
# Linux/Mac
export QRCodeSettings__Locations__1__WiFi__SSID="ProductionWiFi1"
export QRCodeSettings__Locations__1__WiFi__Password="SecurePass1"

# Windows PowerShell
$env:QRCodeSettings__Locations__1__WiFi__SSID="ProductionWiFi1"
$env:QRCodeSettings__Locations__1__WiFi__Password="SecurePass1"
```

### Option 2: appsettings.Production.json

Create `appsettings.Production.json` (add to `.gitignore`):

```json
{
  "QRCodeSettings": {
    "Locations": {
      "1": {
        "WiFi": {
          "SSID": "ProductionSSID1",
          "Password": "SecureProductionPassword1"
        }
      }
    }
  }
}
```

### Option 3: Azure Key Vault / AWS Secrets Manager

Reference secrets instead of hardcoding:

```json
"WiFi": {
  "SSID": "@Microsoft.KeyVault(SecretUri=https://...)",
  "Password": "@Microsoft.KeyVault(SecretUri=https://...)"
}
```

---

## Verification Steps

### Test Location-Specific WiFi:

1. **Generate QR for Location 1:**
   - Admin UI → Select "Downtown" → Table 1 → Preview

2. **Scan with phone:**
   - Should show: "SushiToshi-Downtown-WiFi"
   - Password auto-filled: "downtown2025!"

3. **Generate QR for Location 2:**
   - Select "West End" → Table 1 → Preview

4. **Scan with phone:**
   - Should show: "SushiToshi-WestEnd-WiFi"
   - Password auto-filled: "westend2025!"

✅ **If both work → Unique WiFi per location confirmed!**

---

## Troubleshooting

### Issue: All locations show same WiFi

**Cause:** Location-specific config missing or backend not restarted

**Fix:**
1. Check `appsettings.json` has location-specific `Locations` section
2. Restart backend: `dotnet run`
3. Clear cache and regenerate

### Issue: Old WiFi credentials still showing

**Cause:** Cached QR codes

**Fix:**
1. Admin UI → Clear Cache for affected tables
2. Or clear all: `del storage\qrcodes\*.png` (Windows) / `rm storage/qrcodes/*.png` (Linux)
3. Regenerate QR codes

### Issue: Can't connect to WiFi from QR code

**Possible Causes:**
- Wrong SSID/password in config
- WiFi network is hidden
- WiFi uses non-WPA security (WEP, Open)

**Fix:**
1. Verify SSID and password match actual WiFi router
2. Ensure WiFi is not hidden (or set `H:true` if it is)
3. Confirm WiFi uses WPA/WPA2 encryption

---

## Security Best Practices

### 1. Never Commit Production Passwords
- ❌ Don't commit real passwords to Git
- ✅ Use environment variables or secrets managers
- ✅ Use `.gitignore` for `appsettings.Production.json`

### 2. Use Strong Passwords
- Minimum 12 characters
- Mix of letters, numbers, symbols
- Different password per location

### 3. Separate Guest Network
- Don't use same WiFi as staff/POS systems
- Limit bandwidth per device
- Enable network isolation

### 4. Rotate Regularly
- Change passwords every 3-6 months
- Update config → Restart → Clear cache → Regenerate QR codes

---