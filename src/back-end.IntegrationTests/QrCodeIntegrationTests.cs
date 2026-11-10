using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using back_end.domain.DbContexts;

namespace back_end.IntegrationTests;

public class QrCodeIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public QrCodeIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                // Remove the ApplicationDbContext registration
                var contextDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

                if (contextDescriptor != null)
                {
                    services.Remove(contextDescriptor);
                }

                // Add DbContext using in-memory database for testing
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb" + Guid.NewGuid().ToString());
                });
            });
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetWifiQr_NoAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var locationId = 1;
        var table = 5;

        // Act
        var response = await _client.GetAsync($"/api/admin/qr/wifi?location_id={locationId}&table={table}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSessionQr_NoAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var locationId = 1;
        var table = 5;

        // Act
        var response = await _client.GetAsync($"/api/admin/qr/session?location_id={locationId}&table={table}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BulkGenerate_NoAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var locationId = 1;

        // Act - Updated to match new API (no tableCount parameter)
        var response = await _client.PostAsync($"/api/admin/qr/bulk?location_id={locationId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Clear_NoAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var locationId = 1;
        var table = 5;

        // Act
        var response = await _client.DeleteAsync($"/api/admin/qr/clear?location_id={locationId}&table={table}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Note: To test authenticated endpoints, you would need to:
    // 1. Create a test user in the database
    // 2. Generate a valid JWT token
    // 3. Add the token to the request headers
    // Example:
    // _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", testToken);
}
