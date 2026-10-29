# QR Code Management - Front-End Guide

## Overview

The QR Code Management page lets admins generate, preview and download two kinds of QR code for each table:

- **WiFi QR code:** customers scan it to join the location's WiFi network.
- **Session QR code:** opens sign-in for that table, then joins the customer to the dining session staff started for it.

For how the QR images are generated, stored and configured on the backend, see the [QR code service README](../../../../back-end/Services/QrCode/README.md).

## Files

```
src/front-end/src/
├── app/admin/qr-codes/
│   └── page.jsx                  # Route; checks the user is an admin
├── components/admin/
│   ├── QRCodeManagement.jsx      # The page's UI and API calls
│   └── QR_CODE_INTEGRATION.md    # This file
└── components/
    └── AppNavigationDrawer.jsx   # "QR Codes" menu entry (admin menu only)
```

## Access

- **Route:** `/admin/qr-codes`
- **Menu:** **QR Codes**, in the admin navigation drawer
- **Admins only.** Staff don't see the menu entry. `page.jsx` sends non-admins to `/unauthorized`, and the API (`AdminQrCodeController`) also requires the `Admin` role.

## Features

### Single table

1. Pick a **Location** (defaults to the location chosen in the app bar) and enter a **Table Number**.
2. On the WiFi or Session card, click:
   - **Preview** to see the code in a dialog, or
   - **Download** to save it as `wifi_L{locationId}_T{table}.png` or `session_L{locationId}_T{table}.png`.

### Bulk generation

1. Pick a location. The page shows how many tables it has.
2. Click **Generate QR Codes for All N Tables**. This creates a WiFi and a Session code for every table at that location and saves them on the server.
3. Download the results:
   - **Download as ZIP**: one PNG per code
   - **Download as PDF**: 16 codes per page (4 × 4, 1.75" each), sized for 8.5" × 11" paper

The download buttons only appear for the location you most recently generated codes for.

### Clearing saved codes

The backend saves each generated PNG and reuses it on later requests. After changing a location's WiFi credentials or the session URL, select the table and click **Clear Cache for Table N** so the next request makes a fresh code.

## API Endpoints Used

All are under `/api/admin/qr` and require the `Admin` role.

| Action | Request |
| --- | --- |
| WiFi QR code | `GET /wifi?locationId={id}&table={number}` |
| Session QR code | `GET /session?locationId={id}&table={number}` |
| Bulk generate | `POST /bulk?locationId={id}` |
| Download bulk as ZIP | `GET /bulk/download-zip?locationId={id}` |
| Download bulk as PDF | `GET /bulk/download-pdf?locationId={id}` |
| Clear a table's saved codes | `DELETE /clear?locationId={id}&table={number}` |

The page also calls `GET /api/location` for the location list and `GET /api/TableEntity?locationId={id}` for the table count.

## Error Messages

Messages appear in a snackbar at the bottom right. When the API returns an error message (for example, "Location 3 not found or WiFi credentials not configured"), the page shows that message. Otherwise it falls back to a generic one.

| Message | Cause |
| --- | --- |
| "Please select a location and enter a table number" | Location or table number is empty |
| "No tables found for this location. Please create tables first." | Bulk generation on a location with no tables |
| "Failed to fetch locations" / "Failed to fetch tables" | The backend isn't running, or the request was rejected |
| "Location N not found or WiFi credentials not configured" | The location doesn't exist, or has no WiFi settings in the backend config |
| "Failed to clear QR code cache" | The clear request failed |

## Testing It Manually

1. Sign in as an admin.
2. Open the menu and select **QR Codes**.
3. Pick a location, enter a table number (e.g. `5`), and click **Preview**, then **Download**. Check your downloads for `wifi_L1_T5.png` or `session_L1_T5.png`.
4. Click **Generate QR Codes for All N Tables**, then try both **Download as ZIP** and **Download as PDF**.
5. To test scanning with a real phone, follow [Testing QR codes on your phone](../../../../../docs/dev/phone-testing-setup.md).

## Ideas for Later

- Custom QR code colors or branding
- Scan tracking in analytics
- Emailing QR codes to staff
