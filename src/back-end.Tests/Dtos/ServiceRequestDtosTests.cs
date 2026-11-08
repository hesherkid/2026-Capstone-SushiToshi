using Xunit;
using FluentAssertions;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using back_end.Dtos;
using back_end.domain.enums;

namespace back_end.Tests.Dtos;

public class ServiceRequestDtosTests
{
    /// <summary>
    /// Tests for ServiceRequestCreateDto and ServiceRequestResponseDto.
    /// The requestion user comes from the JWT token, not in the request body, create DTO only carries notes.
    /// User references in the response are numeric user IDs.
    /// </summary>

    // ------------ ServiceRequestCreateDto Tests ------------
    [Fact]
    public void ServiceRequestCreateDto_WithNotes_SerializesandDeserializes()
    {
        // Arrange
        var dto = new ServiceRequestCreateDto { Notes = "Need more napkins" };

        // Act
        var json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<ServiceRequestCreateDto>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Notes.Should().Be("Need more napkins");
    }

    [Fact]
    public void ServiceRequestCreateDto_JsonPropertyName_IsSnakeCase()
    {
        // Arrange
        var dto = new ServiceRequestCreateDto { Notes = "Test" };

        // Act
        var json = JsonSerializer.Serialize(dto);

        // Assert
        json.Should().Contain("\"notes\"");
        json.Should().NotContain("\"Notes\"");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ServiceRequestCreateDto_WithMissingOrBlankNotes_IsValid(string? notes)
    {
        // Arrange
        var dto = new ServiceRequestCreateDto { Notes = notes };

        // Act
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(dto, new ValidationContext(dto), validationResults, true);

        // Assert
        isValid.Should().BeTrue("Notes is optional");
        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void ServiceRequestCreateDto_WithLongNotes_IsValid()
    {
        // Arrange
        var dto = new ServiceRequestCreateDto { Notes = new string('x', 1000) };

        // Act
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(dto, new ValidationContext(dto), validationResults, true);

        // Assert
        isValid.Should().BeTrue("Notes has no length limit");
        dto.Notes!.Length.Should().Be(1000);
    }

    // ------------ ServiceRequestResponseDto Tests ------------

    [Fact]
    public void ServiceRequestResponseDto_WithValidData_SerializesAndDeserializes()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var dto = new ServiceRequestResponseDto
        {
            RequestId = 1,
            SessionId = 5,
            TableId = 3,
            RequestedBy = 123,
            ClaimedBy = 456,
            Notes = "Need water",
            Status = ServiceRequestStatus.Claimed,
            CreatedAt = now,
            ClaimedAt = now.AddMinutes(2),
            CompletedAt = null,
            TableNumber = 5
        };

        // Act
        var json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<ServiceRequestResponseDto>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.RequestId.Should().Be(1);
        deserialized.SessionId.Should().Be(5);
        deserialized.TableId.Should().Be(3);
        deserialized.RequestedBy.Should().Be(123);
        deserialized.ClaimedBy.Should().Be(456);
        deserialized.Status.Should().Be(ServiceRequestStatus.Claimed);
        deserialized.CreatedAt.Should().Be(now);
        deserialized.ClaimedAt.Should().Be(now.AddMinutes(2));
        deserialized.CompletedAt.Should().BeNull();
        deserialized.TableNumber.Should().Be(5);
    }

    [Fact]
    public void ServiceRequestResponseDto_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var dto = new ServiceRequestResponseDto();

        // Assert
        dto.RequestedBy.Should().Be(0);
        dto.ClaimedBy.Should().BeNull();
        dto.Notes.Should().BeNull();
        dto.ClaimedAt.Should().BeNull();
        dto.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void ServiceRequestResponseDto_JsonPropertyNames_AreSnakeCase()
    {
        // Arrange
        var dto = new ServiceRequestResponseDto
        {
            RequestId = 1,
            SessionId = 2,
            TableId = 3,
            RequestedBy = 123,
            ClaimedBy = 456,
            Status = ServiceRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            TableNumber = 5
        };

        // Act
        var json = JsonSerializer.Serialize(dto);

        // Assert
        json.Should().Contain("\"request_id\"");
        json.Should().Contain("\"session_id\"");
        json.Should().Contain("\"table_id\"");
        json.Should().Contain("\"requested_by\"");
        json.Should().Contain("\"claimed_by\"");
        json.Should().Contain("\"notes\"");
        json.Should().Contain("\"status\"");
        json.Should().Contain("\"created_at\"");
        json.Should().Contain("\"claimed_at\"");
        json.Should().Contain("\"completed_at\"");
        json.Should().Contain("\"table_number\"");

    }

    [Theory]
    [InlineData(ServiceRequestStatus.Pending)]
    [InlineData(ServiceRequestStatus.Claimed)]
    [InlineData(ServiceRequestStatus.Completed)]
    [InlineData(ServiceRequestStatus.Cancelled)]
    public void ServiceRequestResponseDto_SupportsAllServiceRequestStatuses(ServiceRequestStatus status)
    {
        // Arrange
        var dto = new ServiceRequestResponseDto
        {
            RequestId = 1,
            SessionId = 1,
            TableId = 1,
            RequestedBy = 1,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            TableNumber = 1
        };

        // Act
        var json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<ServiceRequestResponseDto>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Status.Should().Be(status);
    }

    [Fact]
    public void ServiceRequestResponseDto_UnclaimedRequest_KeepsOptionalFieldsNull()
    {
        // Arrange — a new, unclaimed request
        var dto = new ServiceRequestResponseDto
        {
            RequestId = 1,
            SessionId = 1,
            TableId = 1,
            RequestedBy = 123,
            ClaimedBy = null,
            Notes = null,
            Status = ServiceRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ClaimedAt = null,
            CompletedAt = null,
            TableNumber = 1
        };

        // Act
        var json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<ServiceRequestResponseDto>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.RequestedBy.Should().Be(123);
        deserialized.ClaimedBy.Should().BeNull();
        deserialized.Notes.Should().BeNull();
        deserialized.ClaimedAt.Should().BeNull();
        deserialized.CompletedAt.Should().BeNull();
    }
}