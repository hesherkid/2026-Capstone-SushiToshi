using Xunit;
using FluentAssertions;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using back_end.DTO.DiningSessionDTOs;

namespace back_end.Tests.DTO;

public class CreateSessionRequestDTOTests
{
    /// <summary>
    /// Tests for DiningSessiionDTO.CreateSessionDTO
    /// Only Menu_Id is validated ([Range(1, int.MaxValue)]); all other fields are optional.
    /// </summary>

    private static (bool isValid, List<ValidationResult> results) Validate(object dto)
    {
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(dto, new ValidationContext(dto), results, true);
        return (isValid, results);
    }

    [Fact]
    public void CreateSessionRequestDTO_WithValidData_IsValid()
    {
        // Arrange
        var dto = new CreateSessionRequestDTO
        {
            Menu_Id = 1,
            Location_Id = 1,
            Table_Id = 5,
            Request_By_User_Id = 123,
            Request_By_Name = "John Doe"
        };

        // Act
        var (isValid, results) = Validate(dto);

        // Assert
        isValid.Should().BeTrue();
        results.Should().BeEmpty();
    }

    [Fact]
    public void CreateSessionRequestDTO_WithOnlyRequiredFields_IsValid()
    {
        // Arrange — only Menu_Id is validated; optional fields left null
        var dto = new CreateSessionRequestDTO
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
    public void CreateSessionRequestDTO_WithValidLocationId_IsAccepted(int locationId)
    {
        // Arrange
        var dto = new CreateSessionRequestDTO
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
    public void CreateSessionRequestDTO_WithValidTableId_IsAccepted(int tableId)
    {
        // Arrange
        var dto = new CreateSessionRequestDTO
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
    public void CreateSessionRequestDTO_WithPositiveMenuId_IsValid(int menuId)
    {
        // Arrange
        var dto = new CreateSessionRequestDTO { Menu_Id = menuId, Location_Id = 1, Table_Id = 1 };

        // Act
        var (isValid, _) = Validate(dto);

        // Assert
        isValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateSessionRequestDTO_WithZeroOrNegativeMenuId_IsInvalid(int menuId)
    {
        // Arrange
        var dto = new CreateSessionRequestDTO { Menu_Id = menuId, Location_Id = 1, Table_Id = 1 };

        // Act
        var (isValid, results) = Validate(dto);

        // Assert
        isValid.Should().BeFalse("Menu_Id has [Range(1, int.MaxValue)]");
        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(CreateSessionRequestDTO.Menu_Id)));
    }

    [Fact]
    public void CreateSessionRequestDTO_DefaultInstance_IsInvalidBecauseMenuIdIsZero()
    {
        // Arrange & Act
        var (isValid, _) = Validate(new CreateSessionRequestDTO());

        // Assert
        isValid.Should().BeFalse("Menu_Id defaults to 0, which is below the allowed range");
    }

    [Fact]
    public void CreateSessionRequestDTO_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var dto = new CreateSessionRequestDTO();

        // Assert
        dto.Menu_Id.Should().Be(0);
        dto.Location_Id.Should().Be(0);
        dto.Table_Id.Should().BeNull();
        dto.TableGroup_Id.Should().BeNull();
        dto.AssignmentType.Should().BeNull();
        dto.Request_By_User_Id.Should().BeNull();
        dto.Request_By_Name.Should().BeNull();
    }

    [Fact]
    public void CreateSessionRequestDTO_SerializesAndDeserializesCorrectly()
    {
        // Arrange
        var dto = new CreateSessionRequestDTO
        {
            Menu_Id = 1,
            Location_Id = 2,
            Table_Id = 3,
            TableGroup_Id = null,
            AssignmentType = "table",
            Request_By_User_Id = 456,
            Request_By_Name = "Jane Smith"
        };

        // Act
        var json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<CreateSessionRequestDTO>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Menu_Id.Should().Be(1);
        deserialized.Location_Id.Should().Be(2);
        deserialized.Table_Id.Should().Be(3);
        deserialized.TableGroup_Id.Should().BeNull();
        deserialized.AssignmentType.Should().Be("table");
        deserialized.Request_By_User_Id.Should().Be(456);
        deserialized.Request_By_Name.Should().Be("Jane Smith");
    }

    [Fact]
    public void CreateSessionRequestDTO_MenuId_UsesSnakeCaseJsonName()
    {
        // Arrange
        var dto = new CreateSessionRequestDTO { Menu_Id = 1, Location_Id = 1 };

        // Act
        var json = JsonSerializer.Serialize(dto);

        // Assert — only Menu_Id has a [JsonPropertyName]; the rest keep their C# names
        json.Should().Contain("\"menu_id\"");
        json.Should().Contain("\"Location_Id\"");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CreateSessionRequestDTO_WithBlankOrMissingName_IsValid(string? name)
    {
        // Arrange — guests may have no name; the controller falls back to "Guest"
        var dto = new CreateSessionRequestDTO { Menu_Id = 1, Location_Id = 1, Request_By_Name = name };

        // Act
        var (isValid, _) = Validate(dto);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void CreateSessionRequestDTO_WithLongName_IsValid()
    {
        // Arrange
        var dto = new CreateSessionRequestDTO
        {
            Menu_Id = 1,
            Location_Id = 1,
            Request_By_Name = new string('x', 500)
        };

        // Act
        var (isValid, _) = Validate(dto);

        // Assert
        isValid.Should().BeTrue("Request_By_Name has no length limit");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(5, null)]
    [InlineData(null, 2)]
    public void CreateSessionRequestDTO_TableOrTableGroup_AreOptional(int? tableId, int? tableGroupId)
    {
        // Arrange
        var dto = new CreateSessionRequestDTO
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