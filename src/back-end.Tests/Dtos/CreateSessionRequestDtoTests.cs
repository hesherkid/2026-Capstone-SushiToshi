using Xunit;
using FluentAssertions;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using back_end.Dtos;

namespace back_end.Tests.Dtos;

public class CreateSessionRequestDtoTests
{
    /// <summary>
    /// Tests for DiningSessiionDto.CreateSessionDto
    /// Only Menu_Id is validated ([Range(1, int.MaxValue)]); all other fields are optional.
    /// </summary>

    private static (bool isValid, List<ValidationResult> results) Validate(object dto)
    {
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(dto, new ValidationContext(dto), results, true);
        return (isValid, results);
    }

    [Fact]
    public void CreateSessionRequestDto_WithValidData_IsValid()
    {
        // Arrange
        var dto = new CreateSessionRequestDto
        {
            Menu_Id = 1,
            Location_Id = 1,
            Table_Id = 5,
            AssignmentType = "table"
        };

        // Act
        var (isValid, results) = Validate(dto);

        // Assert
        isValid.Should().BeTrue();
        results.Should().BeEmpty();
    }

    [Fact]
    public void CreateSessionRequestDto_WithOnlyRequiredFields_IsValid()
    {
        // Arrange — only Menu_Id is validated; optional fields left null
        var dto = new CreateSessionRequestDto
        {
            Menu_Id = 1,
            Location_Id = 1
        };

        // Act
        var (isValid, results) = Validate(dto);

        // Assert 
        isValid.Should().BeTrue();
        results.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    public void CreateSessionRequestDto_WithValidLocationId_IsAccepted(int locationId)
    {
        // Arrange
        var dto = new CreateSessionRequestDto
        {
            Menu_Id = 1,
            Location_Id = locationId,
            Table_Id = 1
        };

        // Act
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(dto, new ValidationContext(dto), validationResults, true);

        // Assert
        isValid.Should().BeTrue();
        dto.Location_Id.Should().Be(locationId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    public void CreateSessionRequestDto_WithValidTableId_IsAccepted(int tableId)
    {
        // Arrange
        var dto = new CreateSessionRequestDto
        {
            Menu_Id = 1,
            Location_Id = 1,
            Table_Id = tableId
        };

        // Act
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(dto, new ValidationContext(dto), validationResults, true);

        // Assert
        isValid.Should().BeTrue();
        dto.Table_Id.Should().Be(tableId);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void CreateSessionRequestDto_WithPositiveMenuId_IsValid(int menuId)
    {
        // Arrange
        var dto = new CreateSessionRequestDto { Menu_Id = menuId, Location_Id = 1, Table_Id = 1 };

        // Act
        var (isValid, _) = Validate(dto);

        // Assert
        isValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateSessionRequestDto_WithZeroOrNegativeMenuId_IsInvalid(int menuId)
    {
        // Arrange
        var dto = new CreateSessionRequestDto { Menu_Id = menuId, Location_Id = 1, Table_Id = 1 };

        // Act
        var (isValid, results) = Validate(dto);

        // Assert
        isValid.Should().BeFalse("Menu_Id has [Range(1, int.MaxValue)]");
        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(CreateSessionRequestDto.Menu_Id)));
    }

    [Fact]
    public void CreateSessionRequestDto_DefaultInstance_IsInvalidBecauseMenuIdIsZero()
    {
        // Arrange & Act
        var (isValid, _) = Validate(new CreateSessionRequestDto());

        // Assert
        isValid.Should().BeFalse("Menu_Id defaults to 0, which is below the allowed range");
    }

    [Fact]
    public void CreateSessionRequestDto_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var dto = new CreateSessionRequestDto();

        // Assert
        dto.Menu_Id.Should().Be(0);
        dto.Location_Id.Should().Be(0);
        dto.Table_Id.Should().BeNull();
        dto.TableGroup_Id.Should().BeNull();
        dto.AssignmentType.Should().BeNull();
    }

    [Fact]
    public void CreateSessionRequestDto_SerializesAndDeserializesCorrectly()
    {
        // Arrange
        var dto = new CreateSessionRequestDto
        {
            Menu_Id = 1,
            Location_Id = 2,
            Table_Id = 3,
            TableGroup_Id = null,
            AssignmentType = "table"
        };

        // Act
        var json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<CreateSessionRequestDto>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Menu_Id.Should().Be(1);
        deserialized.Location_Id.Should().Be(2);
        deserialized.Table_Id.Should().Be(3);
        deserialized.TableGroup_Id.Should().BeNull();
        deserialized.AssignmentType.Should().Be("table");
    }

    [Fact]
    public void CreateSessionRequestDto_MenuId_UsesSnakeCaseJsonName()
    {
        // Arrange
        var dto = new CreateSessionRequestDto { Menu_Id = 1, Location_Id = 1 };

        // Act
        var json = JsonSerializer.Serialize(dto);

        // Assert — only Menu_Id has a [JsonPropertyName]; the rest keep their C# names
        json.Should().Contain("\"menu_id\"");
        json.Should().Contain("\"Location_Id\"");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(5, null)]
    [InlineData(null, 2)]
    public void CreateSessionRequestDto_TableOrTableGroup_AreOptional(int? tableId, int? tableGroupId)
    {
        // Arrange
        var dto = new CreateSessionRequestDto
        {
            Menu_Id = 1,
            Location_Id = 1,
            Table_Id = tableId,
            TableGroup_Id = tableGroupId
        };

        // Act
        var (isValid, _) = Validate(dto);

        // Assert
        isValid.Should().BeTrue();
        dto.Table_Id.Should().Be(tableId);
        dto.TableGroup_Id.Should().Be(tableGroupId);
    }
}