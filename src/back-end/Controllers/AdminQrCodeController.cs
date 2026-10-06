using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using back_end.Services;
using back_end.domain.DbContexts;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using SkiaSharp;

namespace back_end.Controllers
{
    /// <summary>
    /// Admin/Staff QR Code endpoints for WiFi and session (menu redirect).
    /// </summary>
    [ApiController]
    [Route("api/admin/qr")]
    [Authorize(Roles = "Admin")]
    public class AdminQrCodeController : ControllerBase
    {
        private readonly QrGeneratorService _qrService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminQrCodeController> _logger;

        public AdminQrCodeController(QrGeneratorService qrService, ApplicationDbContext context, ILogger<AdminQrCodeController> logger)
        {
            _qrService = qrService;
            _context = context;
            _logger = logger;
        }

        [HttpGet("wifi")]
        [SwaggerOperation(
            Summary = "Get WiFi QR code",
            Description = "Returns the saved WiFi QR code for a location and table, generating it if none exists. " +
                          "Set refresh=true to regenerate from current settings (use after changing WiFi credentials)."
        )]
        [Produces("image/png")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetWifiQr(
            [FromQuery, SwaggerParameter("Location ID", Required = true)] int locationId,
            [FromQuery, SwaggerParameter("Table number", Required = true)] int table,
            CancellationToken ct,
            [FromQuery, SwaggerParameter("Regenerate instead of using the saved image")] bool refresh = false)
        {
            if (locationId <= 0 || table <= 0)
                return BadRequest("locationId and table must be positive integers");

            // Service handles the disk cache: uses the saved PNG unless refresh=true (or none exists),
            // otherwise generates from current config and saves it.
            var bytes = await _qrService.GetWifiQrBytesAsync(locationId, table, useCache: !refresh, ct);
            if (bytes is null)
                return NotFound($"Location {locationId} not found or WiFi credentials not configured");

            // Image contains the WiFi password: never allow shared/proxy caching,
            // and make the browser re-check with the server instead of reusing a stale copy.
            Response.Headers.CacheControl = "private, no-cache";

            _logger.LogInformation("Generated WiFi QR for L{loc} T{table} (refresh={refresh})", locationId, table, refresh);
            return File(bytes, "image/png", $"wifi_L{locationId}_T{table}.png");
        }

        [HttpGet("session")]
        [SwaggerOperation(
            Summary = "Get session QR code",
            Description = "Returns the saved session QR code for a location and table, generating it if none exists. " +
                          "Scanning it opens the login page with the location and table pre-filled. " +
                          "Set refresh=true to regenerate from current settings (use after changing SessionPageUrl)."
        )]
        [Produces("image/png")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSessionQr(
            [FromQuery, SwaggerParameter("Location ID", Required = true)] int locationId,
            [FromQuery, SwaggerParameter("Table number", Required = true)] int table,
            CancellationToken ct,
            [FromQuery, SwaggerParameter("Regenerate instead of using the saved image")] bool refresh = false)
        {
            if (locationId <= 0 || table <= 0)
                return BadRequest("locationId and table must be positive integers");

            // Service handles the disk cache: uses the saved PNG unless refresh=true (or none exists),
            // otherwise generates from current config and saves it.
            var bytes = await _qrService.GetSessionQrBytesAsync(locationId, table, useCache: !refresh, ct);
            if (bytes is null)
                return NotFound($"Location {locationId} not found");

            // Admin-only response: keep it out of shared caches, and make the browser
            // re-check with the server so a changed SessionPageUrl shows up immediately.
            Response.Headers.CacheControl = "private, no-cache";

            _logger.LogInformation("Generated Session QR for L{loc} T{table} (refresh={refresh})", locationId, table, refresh);
            return File(bytes, "image/png", $"session_L{locationId}_T{table}.png");
        }

        [HttpPost("bulk")]
        [SwaggerOperation(
            Summary = "Bulk generate QR codes",
            Description = "Generate WiFi and Session QR codes for all tables at a location. Saves to storage/qrcodes/."
        )]
        public async Task<IActionResult> BulkGenerate(
            [FromQuery, SwaggerParameter("Location ID", Required = true)] int locationId,
            CancellationToken ct)
        {
            if (locationId <= 0)
                return BadRequest("locationId must be positive");

            var name = await _qrService.GetLocationNameAsync(locationId, ct);
            if (name is null) return NotFound($"Location {locationId} not found");

            // Fetch all tables for this location from the database
            var tables = await _context.Tables
                .Where(t => t.Location_Id == locationId)
                .OrderBy(t => t.table_number)
                .ToListAsync(ct);

            if (!tables.Any())
                return BadRequest($"No tables found for location {locationId}");

            var files = new List<string>(tables.Count * 2);
            foreach (var table in tables)
            {
                ct.ThrowIfCancellationRequested();

                var wifi = await _qrService.GenerateAndSaveWifiQr(locationId, table.table_number, ct);
                if (wifi is not null) files.Add(Path.GetFileName(wifi));

                var sess = await _qrService.GenerateAndSaveSessionQr(locationId, table.table_number, ct);
                if (sess is not null) files.Add(Path.GetFileName(sess));
            }

            _logger.LogInformation("Bulk generated {count} PNGs for {tableCount} tables at L{loc}", files.Count, tables.Count, locationId);
            return Ok(new
            {
                success = true,
                locationId,
                tableCount = tables.Count,
                filesGenerated = files.Count,
                storageLocation = "storage/qrcodes/",
                files
            });
        }

        [HttpGet("bulk/download-zip")]
        [SwaggerOperation(
            Summary = "Download all QR codes as ZIP",
            Description = "Download all generated QR codes for a location as individual PNG files in a ZIP archive."
        )]
        [Produces("application/zip")]
        public async Task<IActionResult> DownloadBulkZip(
            [FromQuery, SwaggerParameter("Location ID", Required = true)] int locationId,
            CancellationToken ct)
        {
            if (locationId <= 0)
                return BadRequest("locationId must be positive");

            var name = await _qrService.GetLocationNameAsync(locationId, ct);
            if (name is null) return NotFound($"Location {locationId} not found");

            // Fetch all tables for this location
            var tables = await _context.Tables
                .Where(t => t.Location_Id == locationId)
                .OrderBy(t => t.table_number)
                .ToListAsync(ct);

            if (!tables.Any())
                return BadRequest($"No tables found for location {locationId}");

            // Create a memory stream for the ZIP file
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                foreach (var table in tables)
                {
                    ct.ThrowIfCancellationRequested();

                    // Add WiFi QR code
                    var wifiPath = _qrService.GetExistingQrCodePath(locationId, table.table_number, "wifi");
                    if (wifiPath != null && System.IO.File.Exists(wifiPath))
                    {
                        var entry = archive.CreateEntry($"wifi_L{locationId}_T{table.table_number}.png");
                        using var entryStream = entry.Open();
                        using var fileStream = System.IO.File.OpenRead(wifiPath);
                        await fileStream.CopyToAsync(entryStream, ct);
                    }

                    // Add Session QR code
                    var sessionPath = _qrService.GetExistingQrCodePath(locationId, table.table_number, "session");
                    if (sessionPath != null && System.IO.File.Exists(sessionPath))
                    {
                        var entry = archive.CreateEntry($"session_L{locationId}_T{table.table_number}.png");
                        using var entryStream = entry.Open();
                        using var fileStream = System.IO.File.OpenRead(sessionPath);
                        await fileStream.CopyToAsync(entryStream, ct);
                    }
                }
            }

            memoryStream.Position = 0;
            var fileName = $"QRCodes_Location{locationId}_{name?.Replace(" ", "_")}.zip";
            return File(memoryStream.ToArray(), "application/zip", fileName);
        }

        [HttpGet("bulk/download-pdf")]
        [SwaggerOperation(
            Summary = "Download all QR codes as PDF",
            Description = "Download all generated QR codes for a location as a PDF with 16 QR codes per page (4 across × 4 down). QR codes are sized at exactly 1.75\" × 1.75\" for printing on 8.5\" × 11\" paper at 300 DPI."
        )]
        [Produces("application/pdf")]
        public async Task<IActionResult> DownloadBulkPdf(
            [FromQuery, SwaggerParameter("Location ID", Required = true)] int locationId,
            CancellationToken ct)
        {
            if (locationId <= 0)
                return BadRequest("locationId must be positive");

            var locationName = await _qrService.GetLocationNameAsync(locationId, ct);
            if (locationName is null) return NotFound($"Location {locationId} not found");

            // Fetch all tables for this location
            var tables = await _context.Tables
                .Where(t => t.Location_Id == locationId)
                .OrderBy(t => t.table_number)
                .ToListAsync(ct);

            if (!tables.Any())
                return BadRequest($"No tables found for location {locationId}");

            // Collect all QR code paths with their metadata
            var qrCodes = new List<(string path, string type, int tableNumber)>();
            foreach (var table in tables)
            {
                var wifiPath = _qrService.GetExistingQrCodePath(locationId, table.table_number, "wifi");
                if (wifiPath != null && System.IO.File.Exists(wifiPath))
                    qrCodes.Add((wifiPath, "WiFi", table.table_number));

                var sessionPath = _qrService.GetExistingQrCodePath(locationId, table.table_number, "session");
                if (sessionPath != null && System.IO.File.Exists(sessionPath))
                    qrCodes.Add((sessionPath, "Session", table.table_number));
            }

            // Create PDF in memory
            using var pdfStream = new MemoryStream();
            using (var document = SKDocument.CreatePdf(pdfStream))
            {
                // Page dimensions (US Letter: 612 x 792 points at 72 DPI)
                const float pageWidth = 612f;
                const float pageHeight = 792f;

                // Layout: 4 across × 4 down = 16 QR codes per page
                // QR code print size: 1.75" × 1.75" (126 points at 72 DPI)
                const int qrPerRow = 4;
                const int qrPerColumn = 4;
                const int qrPerPage = qrPerRow * qrPerColumn; // 16

                // QR code specifications
                const float qrSize = 1.75f * 72f; // 126 points (1.75 inches)
                const float labelHeight = 28f; // Space for labels below QR code

                // Calculate cell dimensions with margins
                const float marginX = 36f; // 0.5" margins on left/right
                const float marginY = 36f; // 0.5" margins on top/bottom
                const float headerHeight = 30f; // Space for page header

                const float availableWidth = pageWidth - (2 * marginX);
                const float availableHeight = pageHeight - (2 * marginY) - headerHeight;

                const float cellWidth = availableWidth / qrPerRow; // 135 points per cell
                const float cellHeight = availableHeight / qrPerColumn; // ~169 points per cell

                // Fonts and paints, created once and reused for every page
                var boldTypeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) ?? SKTypeface.Default;
                var regularTypeface = SKTypeface.FromFamilyName("Arial") ?? SKTypeface.Default;
                using var headerFont = new SKFont(boldTypeface, 16);
                using var tableFont = new SKFont(boldTypeface, 12);
                using var typeFont = new SKFont(regularTypeface, 10);
                using var blackPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
                using var grayPaint = new SKPaint { Color = SKColors.DarkGray, IsAntialias = true };

                // Process QR codes in batches of 16 per page
                for (int pageIndex = 0; pageIndex < qrCodes.Count; pageIndex += qrPerPage)
                {
                    ct.ThrowIfCancellationRequested();

                    using var canvas = document.BeginPage(pageWidth, pageHeight);

                    // Draw page header (centered)
                    var headerText = $"{locationName} - QR Codes (Page {(pageIndex / qrPerPage) + 1})";
                    canvas.DrawText(headerText, pageWidth / 2, marginY - 8, SKTextAlign.Center, headerFont, blackPaint);

                    // Draw QR codes in grid
                    int qrOnThisPage = Math.Min(qrPerPage, qrCodes.Count - pageIndex);
                    for (int i = 0; i < qrOnThisPage; i++)
                    {
                        int row = i / qrPerRow;
                        int col = i % qrPerRow;

                        var (qrPath, qrType, tableNum) = qrCodes[pageIndex + i];

                        // Calculate position (centered in cell)
                        float cellX = marginX + (col * cellWidth);
                        float cellY = marginY + headerHeight + (row * cellHeight);
                        float qrX = cellX + (cellWidth - qrSize) / 2;
                        float qrY = cellY + (cellHeight - qrSize - labelHeight) / 2;

                        // Draw QR code at exact 1.75" × 1.75" size
                        using var bitmap = SKBitmap.Decode(qrPath);
                        var destRect = new SKRect(qrX, qrY, qrX + qrSize, qrY + qrSize);
                        canvas.DrawBitmap(bitmap, destRect);

                        // Draw labels centered under the QR code
                        float centerX = cellX + cellWidth / 2;
                        canvas.DrawText($"Table {tableNum}", centerX, qrY + qrSize + 16, SKTextAlign.Center, tableFont, blackPaint);
                        canvas.DrawText(qrType, centerX, qrY + qrSize + 28, SKTextAlign.Center, typeFont, grayPaint);
                    }

                    document.EndPage();
                }

                document.Close();
            }

            pdfStream.Position = 0;
            var fileName = $"QRCodes_Location{locationId}_{locationName?.Replace(" ", "_")}.pdf";
            return File(pdfStream.ToArray(), "application/pdf", fileName);
        }

        [HttpDelete("clear")]
        [SwaggerOperation(
            Summary = "Clear cached QR codes",
            Description = "Delete cached WiFi and Session QR codes for a specific table."
        )]
        public IActionResult Clear(
            [FromQuery, SwaggerParameter("Location ID", Required = true)] int locationId,
            [FromQuery, SwaggerParameter("Table number", Required = true)] int table)
        {
            _qrService.DeleteQrCode(locationId, table, "wifi");
            _qrService.DeleteQrCode(locationId, table, "session");
            _logger.LogInformation("Cleared cached QRs for L{loc} T{table}", locationId, table);
            return Ok(new { success = true, message = $"Cleared QR codes for Location {locationId}, Table {table}" });
        }

        // FIXME: Is this needed? not called anywhere in the current code.
        // <summary>
        // Draw a QR code on a page with exact print dimensions (1.75" × 1.75")
        // </summary>
        // private void DrawQRCodeForPrint(SKCanvas canvas, string qrPath, string label, int tableNumber,
        //     string locationName, float pageWidth, float pageHeight, float qrPrintSize)
        // {
        //     using var bitmap = SKBitmap.Decode(qrPath);

        //     // Center QR code on page
        //     float x = (pageWidth - qrPrintSize) / 2;
        //     float y = 150f; // Top margin

        //     // Draw title
        //     using var titlePaint = new SKPaint
        //     {
        //         Color = SKColors.Black,
        //         TextSize = 24,
        //         IsAntialias = true,
        //         Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
        //     };
        //     var titleText = $"{locationName} - Table {tableNumber}";
        //     var titleWidth = titlePaint.MeasureText(titleText);
        //     canvas.DrawText(titleText, (pageWidth - titleWidth) / 2, 80, titlePaint);

        //     // Draw label
        //     using var labelPaint = new SKPaint
        //     {
        //         Color = SKColors.Black,
        //         TextSize = 18,
        //         IsAntialias = true,
        //         Typeface = SKTypeface.FromFamilyName("Arial")
        //     };
        //     var labelWidth = labelPaint.MeasureText(label);
        //     canvas.DrawText(label, (pageWidth - labelWidth) / 2, 120, labelPaint);

        //     // Draw QR code at exact print size (1.75" × 1.75")
        //     // The image will be scaled to fit this size when printed
        //     var destRect = new SKRect(x, y, x + qrPrintSize, y + qrPrintSize);
        //     canvas.DrawBitmap(bitmap, destRect);

        //     // Add print specifications note at bottom
        //     using var notePaint = new SKPaint
        //     {
        //         Color = SKColors.Gray,
        //         TextSize = 10,
        //         IsAntialias = true,
        //         Typeface = SKTypeface.FromFamilyName("Arial")
        //     };
        //     var noteText = "Print size: 1.75\" × 1.75\" | 300 DPI | ECC Level H";
        //     var noteWidth = notePaint.MeasureText(noteText);
        //     canvas.DrawText(noteText, (pageWidth - noteWidth) / 2, pageHeight - 50, notePaint);
        // }
    }
}