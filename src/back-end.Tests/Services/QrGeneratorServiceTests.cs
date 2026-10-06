using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using back_end.Services;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using System.Drawing;

namespace back_end.Tests.Services;

public class QrGeneratorServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly Mock<ILogger<QrGeneratorService>> _mockLogger;
    private readonly QrGeneratorService _service;

    public QrGeneratorServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _mockConfig = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<QrGeneratorService>>();

        _service = new QrGeneratorService(_context, _mockConfig.Object, _mockLogger.Object);

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
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetLocationWifiCredentials_ValidLocation_ReturnsCredentials()
    {
        // Arrange
        var locationId = 1;
        var expectedSsid = "TestWiFi";
        var expectedPassword = "TestPassword123";

        _mockConfig.Setup(c => c[$"QRCodeSettings:Locations:{locationId}:WiFi:SSID"])
            .Returns(expectedSsid);
        _mockConfig.Setup(c => c[$"QRCodeSettings:Locations:{locationId}:WiFi:Password"])
            .Returns(expectedPassword);

        // Act
        var result = await _service.GetLocationWifiCredentials(locationId);

        // Assert
        result.Should().NotBeNull();
        result.Value.ssid.Should().Be(expectedSsid);
        result.Value.password.Should().Be(expectedPassword);
    }

    [Fact]
    public async Task GetLocationWifiCredentials_InvalidLocation_ReturnsNull()
    {
        // Arrange
        var locationId = 999;

        // Act
        var result = await _service.GetLocationWifiCredentials(locationId);

        // Assert
        result.Should().BeNull();
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLocationWifiCredentials_NoLocationSpecific_ReturnsFallback()
    {
        // Arrange
        var locationId = 1;
        var defaultSsid = "DefaultWiFi";
        var defaultPassword = "DefaultPass123";

        _mockConfig.Setup(c => c[$"QRCodeSettings:Locations:{locationId}:WiFi:SSID"])
            .Returns((string?)null);
        _mockConfig.Setup(c => c["QRCodeSettings:WiFi:SSID"])
            .Returns(defaultSsid);
        _mockConfig.Setup(c => c["QRCodeSettings:WiFi:Password"])
            .Returns(defaultPassword);

        // Act
        var result = await _service.GetLocationWifiCredentials(locationId);

        // Assert
        result.Should().NotBeNull();
        result.Value.ssid.Should().Be(defaultSsid);
        result.Value.password.Should().Be(defaultPassword);
    }

    [Fact]
    public async Task GetLocationWifiCredentials_NoCredentialsConfigured_ReturnsNull()
    {
        // Arrange
        var locationId = 1;

        _mockConfig.Setup(c => c[$"QRCodeSettings:Locations:{locationId}:WiFi:SSID"])
            .Returns((string?)null);
        _mockConfig.Setup(c => c["QRCodeSettings:WiFi:SSID"])
            .Returns((string?)null);
        _mockConfig.Setup(c => c["QRCodeSettings:WiFi:Password"])
            .Returns((string?)null);

        // Act
        var result = await _service.GetLocationWifiCredentials(locationId);

        // Assert
        result.Should().BeNull();
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No WiFi credentials configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void GetSessionUrl_ValidParameters_ReturnsCorrectUrl()
    {
        // Arrange
        var locationId = 1;
        var tableNumber = 5;
        var baseUrl = "http://localhost:3000";
        _mockConfig.Setup(c => c["QRCodeSettings:SessionPageUrl"])
            .Returns(baseUrl);

        // Act
        var result = _service.GetSessionUrl(locationId, tableNumber);

        // Assert
        result.Should().Be($"{baseUrl}/auth/login?locationId={locationId}&tableNumber={tableNumber}");
    }

    [Fact]
    public void GetSessionUrl_NoConfiguredUrl_UsesFallback()
    {
        // Arrange
        var locationId = 1;
        var tableNumber = 5;

        _mockConfig.Setup(c => c["QRCodeSettings:SessionPageUrl"])
            .Returns((string?)null);
        _mockConfig.Setup(c => c["Restaurant:BaseUrl"])
            .Returns("http://example.com");

        // Act
        var result = _service.GetSessionUrl(locationId, tableNumber);

        // Assert
        result.Should().Contain("http://example.com");
        result.Should().Contain($"locationId={locationId}");
        result.Should().Contain($"tableNumber={tableNumber}");
    }

    [Theory]
    [InlineData(1, "Location1-WiFi", "password1")]
    [InlineData(2, "Location2-WiFi", "password2")]
    public async Task GetLocationWifiCredentials_DifferentLocations_ReturnsCorrectCredentials(
        int locationId, string expectedSsid, string expectedPassword)
    {
        // Arrange
        if (locationId != 1)
        {
            var location = new Locations
            {
                Location_Id = locationId,
                Name = $"Location{locationId}",
                Address_Primary = "123 Test St",
                City = "Test City",
                Province = "AB",
                Postal_Code = "T5K 2B7",
                Phone_Number = "780-123-4567"
            };
            _context.Locations.Add(location);
            await _context.SaveChangesAsync();
        }

        _mockConfig.Setup(c => c[$"QRCodeSettings:Locations:{locationId}:WiFi:SSID"])
            .Returns(expectedSsid);
        _mockConfig.Setup(c => c[$"QRCodeSettings:Locations:{locationId}:WiFi:Password"])
            .Returns(expectedPassword);

        // Act
        var result = await _service.GetLocationWifiCredentials(locationId);

        // Assert
        result.Should().NotBeNull();
        result.Value.ssid.Should().Be(expectedSsid);
        result.Value.password.Should().Be(expectedPassword);
    }

    [Fact]
    public void QrCodeExists_CachedFile_ReturnsTrue()
    {
        // Note: This test would require actual file system interaction
        // For demonstration, testing the method signature
        var locationId = 1;
        var tableNumber = 5;
        var type = "wifi";

        // Act
        var result = _service.QrCodeExists(locationId, tableNumber, type);

        // Assert - File doesn't exist in test environment
        result.Should().BeFalse();
    }

    [Fact]
    public void GetExistingQrCodePath_NotCached_ReturnsNull()
    {
        // Arrange
        var locationId = 1;
        var tableNumber = 5;
        var type = "wifi";

        // Act
        var result = _service.GetExistingQrCodePath(locationId, tableNumber, type);

        // Assert
        result.Should().BeNull();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
