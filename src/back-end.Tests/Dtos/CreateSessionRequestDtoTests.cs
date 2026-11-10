using Xunit;
using FluentAssertions;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using back_end.Configurations;
using back_end.Dtos;

namespace back_end.Tests.Dtos;

public class CreateSessionRequestDtoTests
{
    /// <summary>
    /// Tests for DiningSessiionDto.CreateSessionDto
    /// Only Menu_Id is validated ([Range(1, int.MaxValue)]); all other fields are optional.
    /// </summary>

    // The JSON settings the API uses
    private static readonly JsonSerializerOptions ApiOptions = CreateApiOptions();

    private static JsonSerializerOptions CreateApiOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ApiJsonOptions.Apply(options);
        return options;
    }

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
            MenuId = 1,
            LocationId = 1,
            TableId = 5,
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
            MenuId = 1,
            LocationId = 1
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
            MenuId = 1,
            LocationId = locationId,
            TableId = 1
        };

        // Act
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(dto, new ValidationContext(dto), validationResults, true);

        // Assert
        isValid.Should().BeTrue();
        dto.LocationId.Should().Be(locationId);
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
            MenuId = 1,
            LocationId = 1,
            TableId = tableId
        };

        // Act
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(dto, new ValidationContext(dto), validationResults, true);

        // Assert
        isValid.Should().BeTrue();
        dto.TableId.Should().Be(tableId);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void CreateSessionRequestDto_WithPositiveMenuId_IsValid(int menuId)
    {
        // Arrange
        var dto = new CreateSessionRequestDto { MenuId = menuId, LocationId = 1, TableId = 1 };

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
        var dto = new CreateSessionRequestDto { MenuId = menuId, LocationId = 1, TableId = 1 };

        // Act
        var (isValid, results) = Validate(dto);

        // Assert
        isValid.Should().BeFalse("Menu_Id has [Range(1, int.MaxValue)]");
        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(CreateSessionRequestDto.MenuId)));
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
        dto.MenuId.Should().Be(0);
        dto.LocationId.Should().Be(0);
        dto.TableId.Should().BeNull();
        dto.TableGroupId.Should().BeNull();
        dto.AssignmentType.Should().BeNull();
    }

    [Fact]
    public void CreateSessionRequestDto_SerializesAndDeserializesCorrectly()
    {
        // Arrange
        var dto = new CreateSessionRequestDto
        {
            MenuId = 1,
            LocationId = 2,
            TableId = 3,
            TableGroupId = null,
            AssignmentType = "table"
        };

        // Act
        var json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<CreateSessionRequestDto>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.MenuId.Should().Be(1);
        deserialized.LocationId.Should().Be(2);
        deserialized.TableId.Should().Be(3);
        deserialized.TableGroupId.Should().BeNull();
        deserialized.AssignmentType.Should().Be("table");
    }

    [Fact]
    public void CreateSessionRequestDto_UsesSnakeCaseJsonNames()
    {
        // Arrange
        var dto = new CreateSessionRequestDto { MenuId = 1, LocationId = 1 };

        // Act
        var json = JsonSerializer.Serialize(dto, ApiOptions);

        // Assert
        json.Should().Contain("\"menu_id\"");
        json.Should().Contain("\"location_id\"");
        json.Should().Contain("\"table_group_id\"");
        json.Should().Contain("\"assignment_type\"");
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
            MenuId = 1,
            LocationId = 1,
            TableId = tableId,
            TableGroupId = tableGroupId
        };

        // Act
        var (isValid, _) = Validate(dto);

        // Assert
        isValid.Should().BeTrue();
        dto.TableId.Should().Be(tableId);
        dto.TableGroupId.Should().Be(tableGroupId);
    }
}