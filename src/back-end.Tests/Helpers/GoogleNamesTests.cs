using Xunit;
using FluentAssertions;
using Google.Apis.Auth;
using back_end.Helpers;

namespace back_end.Tests.Helpers;

public class GoogleNamesTests
{
    [Fact]
    public void FromPayload_UsesTheGivenAndFamilyNames()
    {
        var payload = new GoogleJsonWebSignature.Payload { Name = "Sam Tanaka", GivenName = "Sam", FamilyName = "Tanaka" };

        GoogleNames.FromPayload(payload).Should().Be(("Sam", "Tanaka"));
    }

    // Without the parts, the full name goes in the first name rather than guessing where to split it
    [Fact]
    public void FromPayload_FallsBackToTheFullNameAsTheFirstName()
    {
        var payload = new GoogleJsonWebSignature.Payload { Name = "Sam Tanaka" };

        GoogleNames.FromPayload(payload).Should().Be(("Sam Tanaka", ""));
    }

    [Fact]
    public void FromPayload_KeepsAFamilyNameWithNoGivenName()
    {
        var payload = new GoogleJsonWebSignature.Payload { Name = "Tanaka", FamilyName = "Tanaka" };

        GoogleNames.FromPayload(payload).Should().Be(("", "Tanaka"));
    }

    // The name columns can't be null, so a token with no name must not give null
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromPayload_GivesEmptyNamesWhenGoogleSendsNone(string? name)
    {
        var payload = new GoogleJsonWebSignature.Payload { Name = name, GivenName = name, FamilyName = name };

        GoogleNames.FromPayload(payload).Should().Be(("", ""));
    }

    [Fact]
    public void FromPayload_CutsNamesToTheColumnLength()
    {
        var payload = new GoogleJsonWebSignature.Payload { GivenName = new string('a', 150), FamilyName = new string('b', 150) };

        var (first, last) = GoogleNames.FromPayload(payload);

        first.Should().HaveLength(GoogleNames.MaxLength);
        last.Should().HaveLength(GoogleNames.MaxLength);
    }
}
