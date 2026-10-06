using Xunit;
using FluentAssertions;
using System.Security.Claims;
using back_end.Helpers;

namespace back_end.Tests.Helpers;

public class ClaimsHelpersTests
{

    [Fact]
    public void GetUserId_WithNameIdentifierClaim_ReturnsNameIdentifier()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "name-id-123")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = ClaimsHelpers.GetUserId(user);

        // Assert
        result.Should().Be("name-id-123");
    }

    [Fact]
    public void GetUserId_WithNoClaims_ReturnsEmptyString()
    {
        // Arrange
        var identity = new ClaimsIdentity(new List<Claim>(), "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = ClaimsHelpers.GetUserId(user);

        // Assert
        result.Should().Be(string.Empty);
    }

    [Fact]
    public void GetUserId_WithMultipleClaims_NameIdentifierTakesPrecedence()
    {
        // Arrange - matches JWT: NameIdentifier holds the numerid user id
        var claims = new List<Claim>
        {
            new Claim("user_id", "555"),
            new Claim("id", "666"),
            new Claim("sub", "777"),
            new Claim(ClaimTypes.NameIdentifier, "123")
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        // Act
        var result = ClaimsHelpers.GetUserId(user);

        // Assert
        result.Should().Be("123", "NameIdentifier is checked first");
    }

    [Theory]
    [InlineData("sub")]
    [InlineData("id")]
    [InlineData("user_id")]
    public void GetUserId_WithFallbackClaim_ReturnsIt(string claimType)
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            new List<Claim> { new Claim(claimType, "42") }, "TestAuth"));

        // Act & Assert
        ClaimsHelpers.GetUserId(user).Should().Be("42");
    }

    [Fact]
    public void GetUserDisplayName_WithGivenAndFamilyName_ReturnsCombinedName()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim("given_name", "John"),
            new Claim("family_name", "Doe")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = ClaimsHelpers.GetUserDisplayName(user);

        // Assert
        result.Should().Be("John Doe");
    }

    [Fact]
    public void GetUserDisplayName_WithOnlyGivenName_ReturnsGivenName()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim("given_name", "John")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = ClaimsHelpers.GetUserDisplayName(user);

        // Assert
        result.Should().Be("John");
    }

    [Fact]
    public void GetUserDisplayName_WithOnlyFamilyName_ReturnsFamilyName()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim("family_name", "Doe")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = ClaimsHelpers.GetUserDisplayName(user);

        // Assert
        result.Should().Be("Doe");
    }

    [Fact]
    public void GetUserDisplayName_WithNameClaim_ReturnsName()
    {
        // Arrange - the helper falls back to ClaimTypes.Name (not lowercase "name")
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            new List<Claim> { new Claim(ClaimTypes.Name, "John Doe") }, "TestAuth"));

        // Act & Assert
        ClaimsHelpers.GetUserDisplayName(user).Should().Be("John Doe");
    }

    [Fact]
    public void GetUserDisplayName_WithWhitespaceNames_TrimsAndHandlesCorrectly()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim("given_name", "  John  "),
            new Claim("family_name", "  Doe  ")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = ClaimsHelpers.GetUserDisplayName(user);

        // Assert
        result.Should().Be("John Doe");
    }

    [Fact]
    public void GetUserDisplayName_WithNoClaims_ReturnsUnknown()
    {
        // Arrange
        var identity = new ClaimsIdentity(new List<Claim>(), "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = ClaimsHelpers.GetUserDisplayName(user);

        // Assert
        result.Should().Be("Unknown");
    }

    [Fact]
    public void GetUserDisplayName_WithEmptyNames_ReturnsUnknown()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim("given_name", "   "),
            new Claim("family_name", "   ")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = ClaimsHelpers.GetUserDisplayName(user);

        // Assert
        result.Should().Be("Unknown");
    }

    [Fact]
    public void GetUserDisplayName_GivenAndFamilyTakePrecedenceOverName()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim("given_name", "John"),
            new Claim("family_name", "Doe"),
            new Claim(ClaimTypes.Name, "Jane Smith")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = ClaimsHelpers.GetUserDisplayName(user);

        // Assert
        result.Should().Be("John Doe", "given_name and family_name should take precedence over name claim");
    }

    [Fact]
    public void GetUserRole_WithClaimTypesRole_ReturnsRole()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = ClaimsHelpers.GetUserRole(user);

        // Assert
        result.Should().Be("Admin");
    }

    [Fact]
    public void GetUserRole_WithRolesClaim_ReturnsRole()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim("roles", "user.Staff")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = ClaimsHelpers.GetUserRole(user);

        // Assert
        result.Should().Be("user.Staff");
    }

    [Fact]
    public void GetUserRole_WithMicrosoftRoleClaim_ReturnsRole()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", "user.Admin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = ClaimsHelpers.GetUserRole(user);

        // Assert
        result.Should().Be("user.Admin");
    }

    [Fact]
    public void GetUserRole_WithNoRoleClaim_ReturnsNull()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim("name", "John Doe")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = ClaimsHelpers.GetUserRole(user);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetUserRole_WithNullUser_ReturnsNull()
    {
        // Act
        var result = ClaimsHelpers.GetUserRole(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetUserRole_WithMultipleRoleClaims_ReturnsFirstMatch()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Role, "FirstRole"),
            new Claim("roles", "SecondRole")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = ClaimsHelpers.GetUserRole(user);

        // Assert
        result.Should().Be("FirstRole", "Should return the first matching role claim");
    }
}
