using Xunit;
using FluentAssertions;
using back_end.domain.enums;

namespace back_end.Tests.Helpers;

// users.Role stores these numbers, and the AddStaffLocations migration hardcodes Staff = 3.
// If this test fails, existing accounts would read back as the wrong role.
public class UserRolesTests
{
    [Theory]
    [InlineData(UserRoles.Admin, 0)]
    [InlineData(UserRoles.Employee, 1)]
    [InlineData(UserRoles.Customer, 2)]
    [InlineData(UserRoles.Staff, 3)]
    public void Stored_role_numbers_never_change(UserRoles role, int stored)
    {
        ((int)role).Should().Be(stored);
    }
}
