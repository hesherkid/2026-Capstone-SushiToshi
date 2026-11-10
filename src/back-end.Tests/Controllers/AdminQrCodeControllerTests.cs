using System.Text.Json;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using back_end.Controllers;
using back_end.Services;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace back_end.Tests.Controllers;

public class AdminQrCodeControllerTests : IDisposable
{
    private const int LocId = 1;
    private const int SpaceLocId = 2;
    private const int EmptyLocId = 3;
    private readonly ApplicationDbContext _context;
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly Mock<ILogger<QrGeneratorService>> _mockServiceLogger;
    private readonly Mock<ILogger<AdminQrCodeController>> _mockControllerLogger;
    private readonly QrGeneratorService _qrService;
    private readonly AdminQrCodeController _controller;
    // Unique per test instance so parallel test classes never share QR files
    private readonly string _storageDir =
        Path.Combine(Path.GetTempPath(), "sushitoshi-qr-tests", Guid.NewGuid().ToString());

    public AdminQrCodeControllerTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _mockConfig = new Mock<IConfiguration>();
        _mockServiceLogger = new Mock<ILogger<QrGeneratorService>>();
        _mockControllerLogger = new Mock<ILogger<AdminQrCodeController>>();
        _mockConfig.Setup(c => c["QRCodeSettings:StoragePath"]).Returns(_storageDir);
        // Default WiFi credentials & session URL so generation succeeds for each location.
        // Tests override these as needed.
        _mockConfig.Setup(c => c["QRCodeSettings:WiFi:SSID"]).Returns("TestWiFi");
        _mockConfig.Setup(c => c["QRCodeSettings:WiFi:Password"]).Returns("TestPassword123");
        _mockConfig.Setup(c => c["QRCodeSettings:SessionPageUrl"]).Returns("http://localhost:3000");

        _qrService = new QrGeneratorService(_context, _mockConfig.Object, _mockServiceLogger.Object);
        _controller = new AdminQrCodeController(_qrService, _context, _mockControllerLogger.Object);

        // Setup HttpContext for Response.Headers
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        _controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = httpContext
        };

        SeedTestData();
    }

    private void SeedTestData()
    {
        var location = new Locations
        {
            Location_Id = 1,
            Name = "Downtown",
            Address_Primary = "123 Main St",
            City = "Edmonton",
            Province = "AB",
            Postal_Code = "T5K 2B7",
            Phone_Number = "780-123-4567"
        };

        _context.Locations.Add(location);

        // Add some tables for bulk generation tests
        _context.Tables.Add(new TableEntity { Table_Id = 1, table_number = 1, Location_Id = 1 });
        _context.Tables.Add(new TableEntity { Table_Id = 2, table_number = 2, Location_Id = 1 });
        _context.Tables.Add(new TableEntity { Table_Id = 3, table_number = 3, Location_Id = 1 });

        // Extra locations for PDF filename and no-tables tests
        _context.Locations.Add(NewLocation(SpaceLocId, "West Edmonton"));
        _context.Locations.Add(NewLocation(EmptyLocId, "No Tables"));
        _context.Tables.Add(new TableEntity { Table_Id = 4, table_number = 1, Location_Id = SpaceLocId });


        _context.SaveChanges();
    }

    [Fact]
    public async Task GetWifiQr_ValidRequest_ReturnsFileResult()
    {
        // Arrange
        var locationId = 1;
        var table = 5;

        _mockConfig.Setup(c => c[$"QRCodeSettings:Locations:{locationId}:WiFi:SSID"])
            .Returns("TestWiFi");
        _mockConfig.Setup(c => c[$"QRCodeSettings:Locations:{locationId}:WiFi:Password"])
            .Returns("TestPassword123");

        // Act
        var result = await _controller.GetWifiQr(locationId, table, CancellationToken.None);

        // Assert
        result.Should().BeOfType<FileContentResult>();
        var fileResult = result as FileContentResult;
        fileResult!.ContentType.Should().Be("image/png");
        fileResult.FileDownloadName.Should().Be($"wifi_L{locationId}_T{table}.png");
        fileResult.FileContents.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetWifiQr_InvalidLocation_ReturnsNotFound()
    {
        // Arrange
        var locationId = 999;
        var table = 5;

        _mockConfig.Setup(c => c[$"QRCodeSettings:Locations:{locationId}:WiFi:SSID"])
            .Returns((string?)null);
        _mockConfig.Setup(c => c["QRCodeSettings:WiFi:SSID"])
            .Returns((string?)null);

        // Act
        var result = await _controller.GetWifiQr(locationId, table, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = result as NotFoundObjectResult;
        notFoundResult!.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetWifiQr_InvalidTableNumber_ReturnsBadRequest()
    {
        // Arrange
        var locationId = 1;
        var table = 0;

        // Act
        var result = await _controller.GetWifiQr(locationId, table, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetWifiQr_NegativeLocationId_ReturnsBadRequest()
    {
        // Arrange
        var locationId = -1;
        var table = 5;

        // Act
        var result = await _controller.GetWifiQr(locationId, table, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetSessionQr_ValidRequest_ReturnsFileResult()
    {
        // Arrange
        var locationId = 1;
        var table = 5;

        _mockConfig.Setup(c => c["QRCodeSettings:SessionPageUrl"])
            .Returns("http://localhost:3000");

        // Act
        var result = await _controller.GetSessionQr(locationId, table, CancellationToken.None);

        // Assert
        result.Should().BeOfType<FileContentResult>();
        var fileResult = result as FileContentResult;
        fileResult!.ContentType.Should().Be("image/png");
        fileResult.FileDownloadName.Should().Be($"session_L{locationId}_T{table}.png");
        fileResult.FileContents.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetSessionQr_InvalidLocation_ReturnsNotFound()
    {
        // Arrange
        var locationId = 999;
        var table = 5;

        // Act
        var result = await _controller.GetSessionQr(locationId, table, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetSessionQr_InvalidTableNumber_ReturnsBadRequest()
    {
        // Arrange
        var locationId = 1;
        var table = -1;

        // Act
        var result = await _controller.GetSessionQr(locationId, table, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task BulkGenerate_ValidRequest_ReturnsSuccessResponse()
    {
        // Arrange
        var locationId = 1;

        _mockConfig.Setup(c => c[$"QRCodeSettings:Locations:{locationId}:WiFi:SSID"])
            .Returns("TestWiFi");
        _mockConfig.Setup(c => c[$"QRCodeSettings:Locations:{locationId}:WiFi:Password"])
            .Returns("TestPassword123");
        _mockConfig.Setup(c => c["QRCodeSettings:SessionPageUrl"])
            .Returns("http://localhost:3000");

        // Act
        var result = await _controller.BulkGenerate(locationId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().NotBeNull();

        // The QR code page shows files_generated and table_count
        var json = JsonSerializer.SerializeToElement(okResult.Value);
        json.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            ["success", "location_id", "table_count", "files_generated", "storage_location", "files"]);
        json.GetProperty("location_id").GetInt32().Should().Be(locationId);
    }

    [Fact]
    public async Task BulkGenerate_InvalidLocation_ReturnsNotFound()
    {
        // Arrange
        var locationId = 999;

        // Act
        var result = await _controller.BulkGenerate(locationId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task BulkGenerate_NoTables_ReturnsBadRequest()
    {
        // Arrange
        var locationId = 1;

        // Remove all tables for this location
        var tables = _context.Tables.Where(t => t.Location_Id == locationId).ToList();
        _context.Tables.RemoveRange(tables);
        _context.SaveChanges();

        // Act
        var result = await _controller.BulkGenerate(locationId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult!.Value.Should().NotBeNull();
    }

    [Fact]
    public void Clear_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var locationId = 1;
        var table = 5;

        // Act
        var result = _controller.Clear(locationId, table);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetWifiQr_MultipleRequests_GeneratesConsistentQrCodes()
    {
        // Arrange
        var locationId = 1;
        var table = 5;

        _mockConfig.Setup(c => c[$"QRCodeSettings:Locations:{locationId}:WiFi:SSID"])
            .Returns("TestWiFi");
        _mockConfig.Setup(c => c[$"QRCodeSettings:Locations:{locationId}:WiFi:Password"])
            .Returns("TestPassword123");

        // Act - Make two requests
        var result1 = await _controller.GetWifiQr(locationId, table, CancellationToken.None);
        var result2 = await _controller.GetWifiQr(locationId, table, CancellationToken.None);

        // Assert - Both should return file results
        result1.Should().BeOfType<FileContentResult>();
        result2.Should().BeOfType<FileContentResult>();

        var file1 = result1 as FileContentResult;
        var file2 = result2 as FileContentResult;

        file1!.FileContents.Length.Should().Be(file2!.FileContents.Length);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 10)]
    [InlineData(1, 100)]
    public async Task GetWifiQr_DifferentTables_GeneratesUniqueQrCodes(int locationId, int table)
    {
        // Arrange
        _mockConfig.Setup(c => c[$"QRCodeSettings:Locations:{locationId}:WiFi:SSID"])
            .Returns("TestWiFi");
        _mockConfig.Setup(c => c[$"QRCodeSettings:Locations:{locationId}:WiFi:Password"])
            .Returns("TestPassword123");

        // Act
        var result = await _controller.GetWifiQr(locationId, table, CancellationToken.None);

        // Assert
        result.Should().BeOfType<FileContentResult>();
        var fileResult = result as FileContentResult;
        fileResult!.FileDownloadName.Should().Contain($"T{table}");
    }

    private static Locations NewLocation(int id, string name) => new()
    {
        Location_Id = id,
        Name = name,
        Address_Primary = "123 Main St",
        City = "Edmonton",
        Province = "AB",
        Postal_Code = "T5K 2B7",
        Phone_Number = "780-123-4567"
    };

    private async Task GenerateAllAsync(int locationId)
        => await _controller.BulkGenerate(locationId, CancellationToken.None);

    private string CachePath(string type, int loc, int table)
        => Path.Combine(_storageDir, $"{type}_L{loc}_T{table}.png");

    private static JsonElement ToJson(object? value)
        => JsonSerializer.SerializeToElement(value);

    private static List<string> ZipEntryNames(byte[] zipBytes)
    {
        using var ms = new MemoryStream(zipBytes);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        return zip.Entries.Select(e => e.FullName).ToList();
    }

    private static CancellationToken Cancelled()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        return cts.Token;
    }

    [Fact]
    public async Task GetWifiQr_ValidRequest_SetsPrivateNoCacheHeader()
    {
        await _controller.GetWifiQr(LocId, 1, CancellationToken.None);

        _controller.Response.Headers.CacheControl.ToString().Should().Be("private, no-cache");
    }

    [Fact]
    public async Task GetSessionQr_ValidRequest_SetsPrivateNoCacheHeader()
    {
        await _controller.GetSessionQr(LocId, 1, CancellationToken.None);

        _controller.Response.Headers.CacheControl.ToString().Should().Be("private, no-cache");
    }

    [Fact]
    public async Task GetWifiQr_CachedFileExists_ReturnsCachedBytes()
    {
        var fake = Encoding.ASCII.GetBytes("cached-wifi");
        await File.WriteAllBytesAsync(CachePath("wifi", LocId, 1), fake);

        var result = await _controller.GetWifiQr(LocId, 1, CancellationToken.None);

        result.Should().BeOfType<FileContentResult>()
              .Which.FileContents.Should().Equal(fake);
    }

    [Fact]
    public async Task GetWifiQr_RefreshTrue_IgnoresCacheAndRegenerates()
    {
        var fake = Encoding.ASCII.GetBytes("cached-wifi");
        var path = CachePath("wifi", LocId, 1);
        await File.WriteAllBytesAsync(path, fake);

        var result = await _controller.GetWifiQr(LocId, 1, CancellationToken.None, refresh: true);

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.FileContents.Should().NotEqual(fake);
        file.FileContents.Take(4).Should().Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        (await File.ReadAllBytesAsync(path)).Should().Equal(file.FileContents); // cache overwritten
    }

    [Fact]
    public async Task GetSessionQr_RefreshTrue_IgnoresCacheAndRegenerates()
    {
        var fake = Encoding.ASCII.GetBytes("cached-session");
        await File.WriteAllBytesAsync(CachePath("session", LocId, 1), fake);

        var result = await _controller.GetSessionQr(LocId, 1, CancellationToken.None, refresh: true);

        result.Should().BeOfType<FileContentResult>()
              .Which.FileContents.Should().NotEqual(fake);
    }

    [Fact]
    public async Task GetSessionQr_NegativeLocationId_ReturnsBadRequest()
    {
        var result = await _controller.GetSessionQr(-1, 1, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task BulkGenerate_NonPositiveLocationId_ReturnsBadRequest(int locationId)
    {
        var result = await _controller.BulkGenerate(locationId, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task BulkGenerate_ValidRequest_ReturnsCountsAndFileNames()
    {
        var result = await _controller.BulkGenerate(LocId, CancellationToken.None);

        var json = ToJson(result.Should().BeOfType<OkObjectResult>().Subject.Value);
        json.GetProperty("success").GetBoolean().Should().BeTrue();
        json.GetProperty("locationId").GetInt32().Should().Be(LocId);
        json.GetProperty("tableCount").GetInt32().Should().Be(3);
        json.GetProperty("filesGenerated").GetInt32().Should().Be(6);
        json.GetProperty("storageLocation").GetString().Should().Be("storage/qrcodes/");

        var files = json.GetProperty("files").EnumerateArray().Select(f => f.GetString()).ToList();
        files.Should().BeEquivalentTo(new[]
        {
            "wifi_L1_T1.png", "session_L1_T1.png",
            "wifi_L1_T2.png", "session_L1_T2.png",
            "wifi_L1_T3.png", "session_L1_T3.png"
        });
        files.Should().OnlyContain(f => File.Exists(Path.Combine(_storageDir, f!)));
    }

    [Fact]
    public async Task BulkGenerate_NoWifiCredentials_GeneratesSessionOnly()
    {
        _mockConfig.Setup(c => c["QRCodeSettings:WiFi:SSID"]).Returns((string?)null);
        _mockConfig.Setup(c => c["QRCodeSettings:WiFi:Password"]).Returns((string?)null);

        var result = await _controller.BulkGenerate(LocId, CancellationToken.None);

        var json = ToJson(result.Should().BeOfType<OkObjectResult>().Subject.Value);
        json.GetProperty("filesGenerated").GetInt32().Should().Be(3);
        json.GetProperty("files").EnumerateArray()
            .Should().OnlyContain(f => f.GetString()!.StartsWith("session_"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task DownloadBulkZip_NonPositiveLocationId_ReturnsBadRequest(int locationId)
    {
        var result = await _controller.DownloadBulkZip(locationId, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DownloadBulkZip_UnknownLocation_ReturnsNotFound()
    {
        var result = await _controller.DownloadBulkZip(999, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DownloadBulkZip_LocationWithNoTables_ReturnsBadRequest()
    {
        var result = await _controller.DownloadBulkZip(EmptyLocId, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DownloadBulkZip_AllCodesGenerated_ZipContainsEveryPng()
    {
        await GenerateAllAsync(LocId);

        var result = await _controller.DownloadBulkZip(LocId, CancellationToken.None);

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("application/zip");
        file.FileDownloadName.Should().Be("QRCodes_Location1_Downtown.zip");
        ZipEntryNames(file.FileContents).Should().BeEquivalentTo(new[]
        {
            "wifi_L1_T1.png", "session_L1_T1.png",
            "wifi_L1_T2.png", "session_L1_T2.png",
            "wifi_L1_T3.png", "session_L1_T3.png"
        });
    }

    [Fact]
    public async Task DownloadBulkZip_NoCodesGenerated_ReturnsEmptyZip()
    {
        var result = await _controller.DownloadBulkZip(LocId, CancellationToken.None);

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        ZipEntryNames(file.FileContents).Should().BeEmpty();
    }

    [Fact]
    public async Task DownloadBulkZip_LocationNameWithSpaces_UsesUnderscoresInFileName()
    {
        await GenerateAllAsync(SpaceLocId);

        var result = (FileContentResult)await _controller.DownloadBulkZip(SpaceLocId, CancellationToken.None);

        result.FileDownloadName.Should().Be("QRCodes_Location2_West_Edmonton.zip");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task DownloadBulkPdf_NonPositiveLocationId_ReturnsBadRequest(int locationId)
    {
        var result = await _controller.DownloadBulkPdf(locationId, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DownloadBulkPdf_UnknownLocation_ReturnsNotFound()
    {
        var result = await _controller.DownloadBulkPdf(999, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DownloadBulkPdf_LocationWithNoTables_ReturnsBadRequest()
    {
        var result = await _controller.DownloadBulkPdf(EmptyLocId, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DownloadBulkPdf_AllCodesGenerated_ReturnsValidPdf()
    {
        await GenerateAllAsync(LocId);

        var result = await _controller.DownloadBulkPdf(LocId, CancellationToken.None);

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("application/pdf");
        file.FileDownloadName.Should().Be("QRCodes_Location1_Downtown.pdf");
        Encoding.ASCII.GetString(file.FileContents, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task DownloadBulkPdf_LocationNameWithSpaces_UsesUnderscoresInFileName()
    {
        await GenerateAllAsync(SpaceLocId);

        var result = (FileContentResult)await _controller.DownloadBulkPdf(SpaceLocId, CancellationToken.None);

        result.FileDownloadName.Should().Be("QRCodes_Location2_West_Edmonton.pdf");
    }

    [Fact]
    public async Task DownloadBulkPdf_NoCodesGenerated_StillReturnsPdfFile()
    {
        var result = await _controller.DownloadBulkPdf(LocId, CancellationToken.None);

        result.Should().BeOfType<FileContentResult>()
              .Which.ContentType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task Clear_ExistingFiles_DeletesWifiAndSessionPngs()
    {
        await _controller.GetWifiQr(LocId, 1, CancellationToken.None);
        await _controller.GetSessionQr(LocId, 1, CancellationToken.None);
        File.Exists(CachePath("wifi", LocId, 1)).Should().BeTrue();
        File.Exists(CachePath("session", LocId, 1)).Should().BeTrue();

        var result = _controller.Clear(LocId, 1);

        result.Should().BeOfType<OkObjectResult>();
        File.Exists(CachePath("wifi", LocId, 1)).Should().BeFalse();
        File.Exists(CachePath("session", LocId, 1)).Should().BeFalse();
    }

    [Fact]
    public async Task Clear_OnlyAffectsRequestedTable()
    {
        await GenerateAllAsync(LocId);

        _controller.Clear(LocId, 1);

        File.Exists(CachePath("wifi", LocId, 2)).Should().BeTrue();
        File.Exists(CachePath("session", LocId, 3)).Should().BeTrue();
    }

    [Fact]
    public void Clear_ReturnsMessageWithLocationAndTable()
    {
        var result = (OkObjectResult)_controller.Clear(LocId, 7);

        var json = ToJson(result.Value);
        json.GetProperty("success").GetBoolean().Should().BeTrue();
        json.GetProperty("message").GetString().Should().Be("Cleared QR codes for Location 1, Table 7");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        if (Directory.Exists(_storageDir))
            Directory.Delete(_storageDir, recursive: true);
    }

}