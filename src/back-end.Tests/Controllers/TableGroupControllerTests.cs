using System.Security.Claims;
using System.Text.Json;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.Controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Dtos;
using back_end.Services;

namespace back_end.Tests.Controllers;

public class TableGroupControllerTests : IDisposable
{
    private const int Northside = 1;
    private const int Southside = 2;

    private const int StaffId = 1;   // Northside staff

    // Northside groups
    private const int QuietGroup = 1;   // "Patio", active, one table, no sessions
    private const int BusyGroup = 2;   // "Banquet", active, one table, in an active session
    private const int UsedGroup = 3;   // "Lounge", active, no tables, ended session
    private const int EmptyGroup = 4;   // "Bar", active, no tables, never used
    private const int InactiveGroup = 5;   // "Terrace", inactive

    // Southside groups
    private const int SouthGroup = 6;   // "Booths"

    // Northside tables
    private const int FreeTable = 11;   // no group, never used
    private const int BusyTable = 12;   // no group, in an active session
    private const int QuietGroupTable = 13;   // in QuietGroup
    private const int BusyGroupTable = 14;   // in BusyGroup

    // Southside tables
    private const int SouthTable = 21;

    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<TableGroupController>> _logger = new();

    public TableGroupControllerTests()
    {
        _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        _context.Locations.AddRange(
            new Locations { Location_Id = Northside, Name = "Northside" },
            new Locations { Location_Id = Southside, Name = "Southside" });
        _context.Users.Add(
            new User { User_id = StaffId, Email = "staff@example.com", Role = UserRoles.Staff, Location_id = Northside });
        _context.Menus.Add(new Menu { Menu_id = 1, Name = "Dinner" });
        _context.TableGroups.AddRange(
            Group(QuietGroup, "Patio", Northside),
            Group(BusyGroup, "Banquet", Northside),
            Group(UsedGroup, "Lounge", Northside),
            Group(EmptyGroup, "Bar", Northside),
            Group(InactiveGroup, "Terrace", Northside, active: false),
            Group(SouthGroup, "Booths", Southside));
        _context.Tables.AddRange(
            Table(FreeTable, 1, Northside),
            Table(BusyTable, 2, Northside),
            Table(QuietGroupTable, 3, Northside, group: QuietGroup),
            Table(BusyGroupTable, 4, Northside, group: BusyGroup),
            Table(SouthTable, 1, Southside));
        _context.DiningSessions.AddRange(
            new DiningSession { Session_Id = 100, Menu_Id = 1, Location_Id = Northside, Table_Id = BusyTable, Started_At = DateTime.UtcNow },
            new DiningSession { Session_Id = 101, Menu_Id = 1, Location_Id = Northside, TableGroup_Id = BusyGroup, Started_At = DateTime.UtcNow },
            new DiningSession { Session_Id = 102, Menu_Id = 1, Location_Id = Northside, TableGroup_Id = UsedGroup, Started_At = DateTime.UtcNow.AddHours(-3), Ended_At = DateTime.UtcNow.AddHours(-2) });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private static TableGroup Group(int id, string name, int location, bool active = true) => new()
    {
        TableGroup_Id = id,
        Group_Name = name,
        Is_Active = active,
        Location_Id = location,
    };

    private static TableEntity Table(int id, int number, int location, int? group = null) => new()
    {
        Table_Id = id,
        table_number = number,
        seat_count = 4,
        is_active = true,
        Location_Id = location,
        TableGroup_Id = group,
    };

    private static ControllerContext As(int userId, UserRoles role) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role.ToString()),
            }, "TestAuth")),
        },
    };

    private TableGroupController Controller() =>
        new(_context, _logger.Object, new LocationAccessService(_context)) { ControllerContext = As(StaffId, UserRoles.Staff) };

    private TableGroup StoredGroup(int id) => _context.TableGroups.AsNoTracking().Single(g => g.TableGroup_Id == id);

    private TableEntity StoredTable(int id) => _context.Tables.AsNoTracking().Single(t => t.Table_Id == id);


    [Fact]
    public async Task CreateTableGroup_ValidGroup_ReturnsOk()
    {
        // Act
        var result = await Controller().CreateTableGroup(new TableGroupCreateDto { GroupName = "Garden", LocationId = Northside });

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var saved = _context.TableGroups.AsNoTracking().Single(g => g.Group_Name == "Garden");
        saved.Location_Id.Should().Be(Northside);
        saved.Is_Active.Should().BeTrue();
    }

    [Fact]
    public async Task CreateTableGroup_UnknownLocation_ReturnsBadRequest()
    {
        // Act
        var result = await Controller().CreateTableGroup(new TableGroupCreateDto { GroupName = "Garden", LocationId = 99 });

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        _context.TableGroups.Count().Should().Be(6);
    }

    [Fact]
    public async Task CreateTableGroup_NameUsedAtSameLocation_ReturnsBadRequest()
    {
        // Act
        var result = await Controller().CreateTableGroup(new TableGroupCreateDto { GroupName = "Patio", LocationId = Northside });

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        _context.TableGroups.Count(g => g.Group_Name == "Patio").Should().Be(1);
    }

    [Fact]
    public async Task GetAllTableGroups_StaffActiveOnly_ReturnsTheirActiveGroupsByName()
    {
        // Act
        var result = await Controller().GetAllTableGroups(activeOnly: true, locationId: null);

        // Assert
        var groups = (result.Result as OkObjectResult)!.Value as IEnumerable<TableGroupResponseDto>;
        groups!.Select(g => g.TableGroupId).Should().Equal(BusyGroup, EmptyGroup, UsedGroup, QuietGroup);
    }

    [Fact]
    public async Task GetAvailableTableGroups_Staff_LeavesOutInactiveAndBusyGroups()
    {
        // Act
        var result = await Controller().GetAvailableTableGroups(locationId: null);

        // Assert
        var groups = (result.Result as OkObjectResult)!.Value as IEnumerable<TableGroupResponseDto>;
        groups!.Select(g => g.TableGroupId).Should().BeEquivalentTo(new[] { EmptyGroup, UsedGroup, QuietGroup });
    }

    [Fact]
    public async Task GetTableGroup_Exists_ReturnsOk()
    {
        // Act
        var result = await Controller().GetTableGroup(BusyGroup);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.SerializeToElement(ok.Value);
        json.GetProperty("group_name").GetString().Should().Be("Banquet");
        json.GetProperty("table_count").GetInt32().Should().Be(1);
        json.GetProperty("total_seats").GetInt32().Should().Be(4);
        json.GetProperty("is_in_active_session").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task UpdateTableGroup_NewNameAndDeactivate_ReturnsOk()
    {
        // Act
        var result = await Controller().UpdateTableGroup(QuietGroup, new TableGroupUpdateDto
        {
            GroupName = "Garden",
            IsActive = false,
        });

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var saved = StoredGroup(QuietGroup);
        saved.Group_Name.Should().Be("Garden");
        saved.Is_Active.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateTableGroup_NameUsedAtSameLocation_ReturnsBadRequest()
    {
        // Act
        var result = await Controller().UpdateTableGroup(QuietGroup, new TableGroupUpdateDto { GroupName = "Bar" });

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        StoredGroup(QuietGroup).Group_Name.Should().Be("Patio");
    }

    [Fact]
    public async Task UpdateTableGroup_NameOnlyUsedAtAnotherLocation_ReturnsOk()
    {
        // Act
        var result = await Controller().UpdateTableGroup(QuietGroup, new TableGroupUpdateDto { GroupName = "Booths" });

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        StoredGroup(QuietGroup).Group_Name.Should().Be("Booths");
    }

    [Fact]
    public async Task UpdateTableGroup_DeactivateGroupInActiveSession_ReturnsBadRequest()
    {
        // Act
        var result = await Controller().UpdateTableGroup(BusyGroup, new TableGroupUpdateDto { IsActive = false });

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        StoredGroup(BusyGroup).Is_Active.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteTableGroup_EmptyAndNeverUsed_ReturnsOk()
    {
        // Act
        var result = await Controller().DeleteTableGroup(EmptyGroup);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _context.TableGroups.Any(g => g.TableGroup_Id == EmptyGroup).Should().BeFalse();
    }

    [Theory]
    [InlineData(BusyGroup, "Cannot delete table group that is in an active session")]
    [InlineData(UsedGroup, "Cannot delete table group that is in an ended session")]
    [InlineData(QuietGroup, "Cannot delete table group with 1 assigned tables. Remove tables from group first.")]
    public async Task DeleteTableGroup_InUseUsedBeforeOrHasTables_ReturnsBadRequest(int groupId, string message)
    {
        // Act
        var result = await Controller().DeleteTableGroup(groupId);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>().Which.Value.Should().Be(message);
        _context.TableGroups.Any(g => g.TableGroup_Id == groupId).Should().BeTrue();
    }

    [Fact]
    public async Task AddTableToGroup_FreeTable_ReturnsOk()
    {
        // Act
        var result = await Controller().AddTableToGroup(QuietGroup, FreeTable);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        StoredTable(FreeTable).TableGroup_Id.Should().Be(QuietGroup);
    }

    [Theory]
    [InlineData(SouthTable, "Table and table group must belong to the same location. Table is at location 2, group is at location 1")]
    [InlineData(BusyGroupTable, "Table is already assigned to another group (Group ID: 2)")]
    [InlineData(QuietGroupTable, "Table is already in this group")]
    [InlineData(BusyTable, "Cannot add table to group while it's in an active session")]
    public async Task AddTableToGroup_TableCannotJoin_ReturnsBadRequest(int tableId, string message)
    {
        // Act
        var result = await Controller().AddTableToGroup(QuietGroup, tableId);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>().Which.Value.Should().Be(message);
    }

    [Fact]
    public async Task RemoveTableFromGroup_GroupNotInSession_ReturnsOk()
    {
        // Act
        var result = await Controller().RemoveTableFromGroup(QuietGroup, QuietGroupTable);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        StoredTable(QuietGroupTable).TableGroup_Id.Should().BeNull();
    }

    [Theory]
    [InlineData(QuietGroup, FreeTable, "Table is not in this group")]
    [InlineData(BusyGroup, BusyGroupTable, "Cannot remove table from group while the group is in an active session")]
    public async Task RemoveTableFromGroup_CannotRemove_ReturnsBadRequest(int groupId, int tableId, string message)
    {
        // Act
        var result = await Controller().RemoveTableFromGroup(groupId, tableId);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>().Which.Value.Should().Be(message);
    }

    [Theory]
    [InlineData(QuietGroup, false)]
    [InlineData(InactiveGroup, true)]
    public async Task ToggleTableGroupStatus_NotInUse_ReturnsOk(int groupId, bool expected)
    {
        // Act
        var result = await Controller().ToggleTableGroupStatus(groupId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        StoredGroup(groupId).Is_Active.Should().Be(expected);
    }

    [Fact]
    public async Task ToggleTableGroupStatus_InActiveSession_ReturnsBadRequest()
    {
        // Act
        var result = await Controller().ToggleTableGroupStatus(BusyGroup);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        StoredGroup(BusyGroup).Is_Active.Should().BeTrue();
    }

    [Theory]
    [InlineData(QuietGroup, true, "Table group is available")]
    [InlineData(BusyGroup, false, "Table group is in an active session")]
    public async Task CheckTableGroupActiveSession_ReturnsGroupStatus(int groupId, bool success, string message)
    {
        // Act
        var result = await Controller().CheckTableGroupActiveSession(groupId);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.SerializeToElement(ok.Value);
        json.GetProperty("success").GetBoolean().Should().Be(success);
        json.GetProperty("message").GetString().Should().Be(message);
    }
}