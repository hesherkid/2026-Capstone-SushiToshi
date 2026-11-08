using Xunit;
using FluentAssertions;
using back_end.domain.Entities;
using back_end.Helpers;
using BCryptNet = BCrypt.Net.BCrypt;

namespace back_end.Tests.Helpers;

public class GoogleAccountsTests
{
    // Someone else may have registered the email; their password must not stay on the owner's account
    [Fact]
    public void ClaimUnverified_ConfirmsTheAccountAndRemovesItsPassword()
    {
        var user = new User { Password_hash = BCryptNet.HashPassword("Attacker1!"), Is_email_confirmed = false };

        GoogleAccounts.ClaimUnverified(user).Should().BeTrue();

        user.Is_email_confirmed.Should().BeTrue();
        user.Password_hash.Should().BeNull();
    }

    // The owner verified the email and set this password themselves
    [Fact]
    public void ClaimUnverified_LeavesAConfirmedAccountsPassword()
    {
        var hash = BCryptNet.HashPassword("Password1!");
        var user = new User { Password_hash = hash, Is_email_confirmed = true };

        GoogleAccounts.ClaimUnverified(user).Should().BeFalse();

        user.Password_hash.Should().Be(hash);
    }
}
