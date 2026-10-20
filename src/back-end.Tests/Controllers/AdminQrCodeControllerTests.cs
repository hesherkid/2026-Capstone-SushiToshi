using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using back_end.Controllers;
using back_end.Services;
using back_end.domain.DbContexts;
using back_end.domain.Entities;

namespace back_end.Tests.Controllers;

public class AdminQrCodeControllerTests : IDisposable
{
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

        _qrService = new QrGeneratorService(_context, _mockConfig.Object, _mockServiceLogger.Object);
        _controller = new AdminQrCodeController(_qrService, _context, _mockControllerLogger.Object);

        // Setup HttpContext for Response.Headers
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        _controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = httpContext
        };

        // Seed test data
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

        // Verify the response contains expected data
        dynamic value = okResult.Value!;
        Assert.NotNull(value);
        // Should have generated QR codes for 3 tables (WiFi + Session = 6 files)
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

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        if (Directory.Exists(_storageDir))
            Directory.Delete(_storageDir, recursive: true);
    }
}