using System.Text;
using QRCoder;
using SkiaSharp;
using back_end.domain.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace back_end.Services
{
    /// <summary>
    /// Generates, labels, caches, and serves QR codes (WiFi + Session) using QRCoder + SkiaSharp.
    /// Cross-platform (Linux/Windows/macOS). Stores PNGs under storage/qrcodes/.
    /// </summary>
    public class QrGeneratorService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly ILogger<QrGeneratorService> _logger;
        private readonly string _storagePath;

        public QrGeneratorService(
            ApplicationDbContext context,
            IConfiguration config,
            ILogger<QrGeneratorService> logger)
        {
            _context = context;
            _config = config;
            _logger = logger;

            _storagePath = Path.Combine(Directory.GetCurrentDirectory(), "storage", "qrcodes");
            Directory.CreateDirectory(_storagePath);
        }

        // ---------- Public: Controller-facing helpers ----------

        public async Task<byte[]?> GetWifiQrBytesAsync(int locationId, int table, bool useCache = true, CancellationToken ct = default)
        {
            if (useCache)
            {
                var existing = GetExistingQrCodePath(locationId, table, "wifi");
                if (existing is not null && File.Exists(existing))
                    return await File.ReadAllBytesAsync(existing, ct);
            }

            var creds = await GetLocationWifiCredentials(locationId, ct);
            if (creds is null) return null;
            var (ssid, password) = creds.Value;

            var locationName = await GetLocationNameAsync(locationId, ct) ?? $"Location {locationId}";

            var payload = new PayloadGenerator.WiFi(
                ssid, password, PayloadGenerator.WiFi.Authentication.WPA, false).ToString();

            // Generate at 600x600px for 1.75" × 1.75" print at 300 DPI
            var qrBytes = CreateQrPng(payload, QRCodeGenerator.ECCLevel.H, pixelsPerModule: 20);
            var labeled = AddLabelWithSkiaBelow(qrBytes, $"{locationName} — Table {table} — WiFi");

            var filePath = Path.Combine(_storagePath, $"wifi_L{locationId}_T{table}.png");
            await File.WriteAllBytesAsync(filePath, labeled, ct);
            return labeled;
        }

        public async Task<byte[]?> GetSessionQrBytesAsync(int locationId, int table, bool useCache = true, CancellationToken ct = default)
        {
            if (useCache)
            {
                var existing = GetExistingQrCodePath(locationId, table, "session");
                if (existing is not null && File.Exists(existing))
                    return await File.ReadAllBytesAsync(existing, ct);
            }

            var locationName = await GetLocationNameAsync(locationId, ct);
            if (locationName is null) return null;

            var url = GetSessionUrl(locationId, table);
            // Generate at 600x600px for 1.75" × 1.75" print at 300 DPI
            var qrBytes = CreateQrPng(url, QRCodeGenerator.ECCLevel.H, pixelsPerModule: 20);
            var labeled = AddLabelWithSkiaBelow(qrBytes, $"{locationName} — Table {table} — Menu");

            var filePath = Path.Combine(_storagePath, $"session_L{locationId}_T{table}.png");
            await File.WriteAllBytesAsync(filePath, labeled, ct);
            return labeled;
        }

        public async Task<string?> GenerateAndSaveWifiQr(int locationId, int table, CancellationToken ct = default)
        {
            var bytes = await GetWifiQrBytesAsync(locationId, table, useCache: false, ct);
            if (bytes is null) return null;

            var filePath = Path.Combine(_storagePath, $"wifi_L{locationId}_T{table}.png");
            if (!File.Exists(filePath)) await File.WriteAllBytesAsync(filePath, bytes, ct);
            _logger.LogInformation("Generated WiFi QR: wifi_L{loc}_T{table}.png", locationId, table);
            return filePath;
        }

        public async Task<string?> GenerateAndSaveSessionQr(int locationId, int table, CancellationToken ct = default)
        {
            var bytes = await GetSessionQrBytesAsync(locationId, table, useCache: false, ct);
            if (bytes is null) return null;

            var filePath = Path.Combine(_storagePath, $"session_L{locationId}_T{table}.png");
            if (!File.Exists(filePath)) await File.WriteAllBytesAsync(filePath, bytes, ct);
            _logger.LogInformation("Generated Session QR: session_L{loc}_T{table}.png", locationId, table);
            return filePath;
        }

        public bool QrCodeExists(int locationId, int table, string type)
        {
            var path = Path.Combine(_storagePath, $"{type.ToLower()}_L{locationId}_T{table}.png");
            return File.Exists(path);
        }

        public string? GetExistingQrCodePath(int locationId, int table, string type)
        {
            var path = Path.Combine(_storagePath, $"{type.ToLower()}_L{locationId}_T{table}.png");
            return File.Exists(path) ? path : null;
        }

        public void DeleteQrCode(int locationId, int table, string type)
        {
            var path = Path.Combine(_storagePath, $"{type.ToLower()}_L{locationId}_T{table}.png");
            if (File.Exists(path))
            {
                File.Delete(path);
                _logger.LogInformation("Deleted {type} QR: {file}", type, Path.GetFileName(path));
            }
        }

        public async Task<string?> GetLocationNameAsync(int locationId, CancellationToken ct = default)
            => (await _context.Locations.FindAsync(new object[] { locationId }, ct))?.Name;

        public async Task<(string ssid, string password)?> GetLocationWifiCredentials(int locationId, CancellationToken ct = default)
        {
            // confirm location exists
            var loc = await _context.Locations.FindAsync(new object[] { locationId }, ct);
            if (loc is null)
            {
                _logger.LogWarning("Location {loc} not found", locationId);
                return null;
            }

            // location-specific overrides
            var locSsid = _config[$"QRCodeSettings:Locations:{locationId}:WiFi:SSID"];
            var locPwd = _config[$"QRCodeSettings:Locations:{locationId}:WiFi:Password"];
            if (!string.IsNullOrWhiteSpace(locSsid) && !string.IsNullOrWhiteSpace(locPwd))
                return (locSsid!, locPwd!);

            // defaults
            var defSsid = _config["QRCodeSettings:WiFi:SSID"];
            var defPwd = _config["QRCodeSettings:WiFi:Password"];
            if (string.IsNullOrWhiteSpace(defSsid) || string.IsNullOrWhiteSpace(defPwd))
            {
                _logger.LogError("No WiFi credentials configured (QRCodeSettings:WiFi)");
                return null;
            }
            return (defSsid!, defPwd!);
        }

        public string GetSessionUrl(int locationId, int table)
        {
            var baseUrl = _config["QRCodeSettings:SessionPageUrl"]
                       ?? _config["Restaurant:BaseUrl"]
                       ?? "http://localhost:3000";
            return $"{baseUrl}/auth/login?locationId={locationId}&tableNumber={table}";
        }

        // ---------- Private: rendering ----------

        private static byte[] CreateQrPng(string content, QRCodeGenerator.ECCLevel ecc, int pixelsPerModule)
        {
            using var gen = new QRCodeGenerator();
            using var data = gen.CreateQrCode(content, ecc);
            var png = new PngByteQRCode(data);
            return png.GetGraphic(pixelsPerModule);
        }

        /// <summary>Add a single-line label below the QR using SkiaSharp; returns PNG bytes.</summary>
        private static byte[] AddLabelWithSkiaBelow(byte[] qrPng, string label)
        {
            using var qrBitmap = SKBitmap.Decode(qrPng);
            int qrW = qrBitmap.Width;
            int qrH = qrBitmap.Height;

            int labelHeight = 60;

            var info = new SKImageInfo(qrW, qrH + labelHeight);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;

            // White background
            canvas.Clear(SKColors.White);

            // Draw QR
            canvas.DrawBitmap(qrBitmap, new SKPoint(0, 0));

            using var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
                                ?? SKTypeface.Default;

            using var font = new SKFont(typeface, 36);

            using var paint = new SKPaint
            {
                Color = SKColors.Black,
                IsAntialias = true
            };

            // Compute center X
            float textX = qrW / 2f;

            // Compute Y position manually
            float textY = qrH + (labelHeight / 2f) - 44f;

            // Draw centered text (set alignment on paint)
            canvas.DrawText(label, textX, textY, SKTextAlign.Center, font, paint);

            // Encode PNG
            using var img = surface.Snapshot();
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
    }
}