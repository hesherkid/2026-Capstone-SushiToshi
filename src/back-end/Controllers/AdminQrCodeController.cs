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
    /// Admin QR Code endpoints for WiFi and session (menu redirect).
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

        /// <summary>
        /// Gets or generates the WiFi QR code for a specific location and table.
        /// </summary>
        /// <param name="locationId">Restaurant location ID</param>
        /// <param name="table">Restaurant table number</param>
        /// <param name="ct">Cancel token</param>
        /// <param name="refresh">Set refresh=true to regenerate the QR code instead of using the saved image. Use this after changing WiFi credentials.</param>
        /// <returns>An image file containing the WiFi QR code</returns>
        /// <response code="200">Returns the WiFi QR code image</response>
        /// <response code="404">Location not found or WiFi credentials not configured</response>
        /// <response code="400">Invalid location_id or table number</response>
        /// <response code="401">Unauthorized if the user is not signed in</response>
        /// <response code="403">Forbidden if the user is not an admin</response>
        [HttpGet("wifi")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetWifiQr(
            [FromQuery(Name = "location_id"), SwaggerParameter("Location ID", Required = true)] int locationId,
            [FromQuery, SwaggerParameter("Table number", Required = true)] int table,
            CancellationToken ct,
            [FromQuery, SwaggerParameter("Regenerate instead of using the saved image")] bool refresh = false)
        {
            if (locationId <= 0 || table <= 0)
                return BadRequest("location_id and table must be positive integers");

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

        /// <summary>
        /// Gets or generates the session QR code for a specific location and table.
        /// </summary>
        /// <param name="locationId">Restaurant location ID</param>
        /// <param name="table">Restaurant table number</param>
        /// <param name="ct">Cancel token</param>
        /// <param name="refresh">Set refresh=true to regenerate the QR code instead of using the saved image. Use this after changing SessionPageUrl.</param>
        /// <returns>An image file containing the session QR code</returns>
        /// <response code="200">Returns the session QR code image</response>
        /// <response code="404">Location not found</response>
        /// <response code="400">Invalid location_id or table number</response>
        /// <response code="401">Unauthorized if the user is not signed in</response>
        /// <response code="403">Forbidden if the user is not an admin</response>
        [HttpGet("session")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetSessionQr(
            [FromQuery(Name = "location_id"), SwaggerParameter("Location ID", Required = true)] int locationId,
            [FromQuery, SwaggerParameter("Table number", Required = true)] int table,
            CancellationToken ct,
            [FromQuery, SwaggerParameter("Regenerate instead of using the saved image")] bool refresh = false)
        {
            if (locationId <= 0 || table <= 0)
                return BadRequest("location_id and table must be positive integers");

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

        /// <summary>
        /// Generates and saves the WiFi and session QR codes for every table at a location,
        /// overwriting any saved images.
        /// </summary>
        /// <remarks>
        /// A WiFi QR is skipped when the location has no WiFi credentials, so files_generated
        /// can be lower than table_count × 2.
        /// </remarks>
        /// <param name="locationId">Restaurant location ID</param>
        /// <param name="ct">Cancel token</param>
        /// <returns>All QR codes generated for the location specified</returns>
        /// <response code="200">QR codes successfully generated</response>
        /// <response code="404">Location not found</response>
        /// <response code="400">Invalid location_id or no tables found for the location</response>
        /// <response code="401">Unauthorized if the user is not signed in</response>
        /// <response code="403">Forbidden if the user is not an admin</response>
        [HttpPost("bulk")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> BulkGenerate(
            [FromQuery(Name = "location_id"), SwaggerParameter("Location ID", Required = true)] int locationId,
            CancellationToken ct)
        {
            if (locationId <= 0)
                return BadRequest("location_id must be positive");

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
                location_id = locationId,
                table_count = tables.Count,
                files_generated = files.Count,
                storage_location = "storage/qrcodes/",
                files
            });
        }

        /// <summary>
        /// Downloads a location's saved QR codes as a ZIP file.
        /// </summary>
        /// <remarks>
        /// Only includes images already saved; run POST /api/admin/qr/bulk first.
        /// Tables with no saved image are left out.
        /// </remarks>
        /// <param name="locationId">Restaurant location ID</param>
        /// <param name="ct">Cancel token</param>
        /// <returns>ZIP file containing all QR codes for the location</returns>
        /// <response code="200">ZIP file successfully generated</response>
        /// <response code="404">Location not found</response>
        /// <response code="400">Invalid location_id or no tables found for the location</response>
        /// <response code="401">Unauthorized if the user is not signed in</response>
        /// <response code="403">Forbidden if the user is not an admin</response>
        [HttpGet("bulk/download-zip")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DownloadBulkZip(
            [FromQuery(Name = "location_id"), SwaggerParameter("Location ID", Required = true)] int locationId,
            CancellationToken ct)
        {
            if (locationId <= 0)
                return BadRequest("location_id must be positive");

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

        /// <summary>
        /// Downloads a location's saved QR codes as a printable PDF (8 tables per page).
        /// </summary>
        /// <remarks>
        /// Only includes images already saved; run POST /api/admin/qr/bulk first.
        /// Tables with no saved image are left out.
        /// </remarks>
        /// <param name="locationId">Restaurant location ID</param>
        /// <param name="ct">Cancel token</param>
        /// <returns>PDF file containing all QR codes for the location</returns>
        /// <response code="200">PDF file successfully generated</response>
        /// <response code="404">Location not found</response>
        /// <response code="400">Invalid location_id or no tables found for the location</response>
        /// <response code="401">Unauthorized if the user is not signed in</response>
        /// <response code="403">Forbidden if the user is not an admin</response>
        [HttpGet("bulk/download-pdf")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DownloadBulkPdf(
            [FromQuery(Name = "location_id"), SwaggerParameter("Location ID", Required = true)] int locationId,
            CancellationToken ct)
        {
            if (locationId <= 0)
                return BadRequest("location_id must be positive");

            var locationName = await _qrService.GetLocationNameAsync(locationId, ct);
            if (locationName is null) return NotFound($"Location {locationId} not found");

            // Fetch all tables for this location
            var tables = await _context.Tables
                .Where(t => t.Location_Id == locationId)
                .OrderBy(t => t.table_number)
                .ToListAsync(ct);

            if (!tables.Any())
                return BadRequest($"No tables found for location {locationId}");

            // Group QR codes by table so a shared table label is drawn once per table group.
            var qrCodesByTable = new List<(int tableNumber, List<(string path, string type)> qrCodes)>();
            foreach (var table in tables)
            {
                var tableQrCodes = new List<(string path, string type)>();

                var wifiPath = _qrService.GetExistingQrCodePath(locationId, table.table_number, "wifi");
                if (wifiPath != null && System.IO.File.Exists(wifiPath))
                    tableQrCodes.Add((wifiPath, "Scan for WiFi"));

                var sessionPath = _qrService.GetExistingQrCodePath(locationId, table.table_number, "session");
                if (sessionPath != null && System.IO.File.Exists(sessionPath))
                    tableQrCodes.Add((sessionPath, "Scan to Order"));

                if (tableQrCodes.Count > 0)
                    qrCodesByTable.Add((table.table_number, tableQrCodes));
            }

            // Create PDF in memory
            using var pdfStream = new MemoryStream();
            using (var document = SKDocument.CreatePdf(pdfStream))
            {
                // Page dimensions (US Letter: 612 x 792 points at 72 DPI)
                const float pageWidth = 612f;
                const float pageHeight = 792f;

                // Layout: 2 table groups across × 4 down = 8 tables per page
                // QR code print size: 1.75" × 1.75" (126 points at 72 DPI)
                const int tableGroupsPerRow = 2;
                const int tableGroupsPerColumn = 4;
                const int tableGroupsPerPage = tableGroupsPerRow * tableGroupsPerColumn;
                const float qrGap = 6f;

                // QR code specifications
                const float qrSize = 1.75f * 72f; // 126 points (1.75 inches)
                // Calculate cell dimensions with margins
                const float marginX = 36f; // 0.5" margins on left/right
                const float marginY = 36f; // 0.5" margins on top/bottom
                const float headerHeight = 30f; // Space for page header

                const float availableWidth = pageWidth - (2 * marginX);
                const float availableHeight = pageHeight - (2 * marginY) - headerHeight;

                const float cellWidth = availableWidth / tableGroupsPerRow;
                const float cellHeight = availableHeight / tableGroupsPerColumn;

                // Fonts and paints, created once and reused for every page
                var boldTypeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) ?? SKTypeface.Default;
                var regularTypeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal) ?? SKTypeface.Default;
                using SKFont headerFont = new(boldTypeface, 16);
                using SKFont tableFont = new(boldTypeface, 15);
                using SKFont typeFont = new(regularTypeface, 13) { Embolden = true };
                using SKPaint blackPaint = new() { Color = SKColors.Black, IsAntialias = true };
                using SKPaint grayPaint = new() { Color = SKColors.Black, IsAntialias = true };

                // Process table groups in batches of 8 per page.
                for (int pageIndex = 0; pageIndex < qrCodesByTable.Count; pageIndex += tableGroupsPerPage)
                {
                    ct.ThrowIfCancellationRequested();

                    using var canvas = document.BeginPage(pageWidth, pageHeight);

                    // Draw page header (centered)
                    var headerText = $"{locationName} - QR Codes (Page {(pageIndex / tableGroupsPerPage) + 1})";
                    canvas.DrawText(headerText, pageWidth / 2, marginY - 8, SKTextAlign.Center, headerFont, blackPaint);

                    // Draw table groups in grid; each group may contain both WiFi and Order QR codes.
                    int tablesOnThisPage = Math.Min(tableGroupsPerPage, qrCodesByTable.Count - pageIndex);
                    for (int i = 0; i < tablesOnThisPage; i++)
                    {
                        int row = i / tableGroupsPerRow;
                        int col = i % tableGroupsPerRow;

                        var (tableNum, qrCodes) = qrCodesByTable[pageIndex + i];

                        // Center the QR pair within the table group.
                        float cellX = marginX + (col * cellWidth);
                        float cellY = marginY + headerHeight + (row * cellHeight);
                        float centerX = cellX + cellWidth / 2;
                        float pairWidth = qrCodes.Count * qrSize + Math.Max(0, qrCodes.Count - 1) * qrGap;
                        float pairX = cellX + (cellWidth - pairWidth) / 2;
                        float qrY = cellY + 22f;

                        // Draw the table label once per table group.
                        canvas.DrawText($"Table {tableNum}", centerX, cellY + 21f, SKTextAlign.Center, tableFont, blackPaint);

                        for (int qrIndex = 0; qrIndex < qrCodes.Count; qrIndex++)
                        {
                            var (qrPath, qrType) = qrCodes[qrIndex];

                            float qrX = pairX + qrIndex * (qrSize + qrGap);

                            using var bitmap = SKBitmap.Decode(qrPath);
                            var destRect = new SKRect(qrX, qrY, qrX + qrSize, qrY + qrSize);
                            canvas.DrawBitmap(bitmap, destRect);

                            canvas.DrawText(qrType, qrX + qrSize / 2, qrY + qrSize + 8, SKTextAlign.Center, typeFont, grayPaint);
                        }
                    }

                    document.EndPage();
                }

                document.Close();
            }

            pdfStream.Position = 0;
            var fileName = $"QRCodes_Location{locationId}_{locationName?.Replace(" ", "_")}.pdf";
            return File(pdfStream.ToArray(), "application/pdf", fileName);
        }

        /// <summary>
        /// Clear cached QR codes for a specific table at a location.
        /// </summary>
        /// <param name="locationId">Restaurant location ID</param>
        /// <param name="table">Table number</param>
        /// <returns>Result of the clear operation</returns>
        /// <response code="200">QR codes successfully cleared</response>
        /// <response code="401">Unauthorized if the user is not signed in</response>
        /// <response code="403">Forbidden if the user is not an admin</response>
        [HttpDelete("clear")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public IActionResult Clear(
            [FromQuery(Name = "location_id"), SwaggerParameter("Location ID", Required = true)] int locationId,
            [FromQuery, SwaggerParameter("Table number", Required = true)] int table)
        {
            _qrService.DeleteQrCode(locationId, table, "wifi");
            _qrService.DeleteQrCode(locationId, table, "session");
            _logger.LogInformation("Cleared cached QRs for L{loc} T{table}", locationId, table);
            return Ok(new { success = true, message = $"Cleared QR codes for Location {locationId}, Table {table}" });
        }
    }
}