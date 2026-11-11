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

// Staff-vs-admin location filtering for lists, create and update is in StaffLocationLockTests
public class TableEntityControllerTests : IDisposable
{
    private const int Northside = 1;
    private const int Southside = 2;

    private const int StaffId = 1;   // Northside staff
    private const int UnassignedStaffId = 2;   // no location yet
    private const int AdminId = 3;

    private const int QuietGroup = 1;   // Northside, no sessions
    private const int BusyGroup = 2;   // Northside, in an active session
    private const int SouthGroup = 3;   // Southside

    // Northside tables
    private const int FreeTable = 11;   // #1 active, never used
    private const int BusyTable = 12;   // #2 active, in an active session
    private const int UsedTable = 13;   // #3 active, only in an ended session
    private const int BusyGroupTable = 14;   // #4 active, in BusyGroup
    private const int InactiveTable = 15;   // #5 inactive
    private const int QuietGroupTable = 16;   // #6 active, in QuietGroup

    // Southside tables
    private const int SouthTable = 21;   // #1
    private const int SouthTable7 = 22;   // #7

    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<TableEntityController>> _logger = new();

    public TableEntityControllerTests()
    {
        _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        _context.Locations.AddRange(
            new Locations { Location_Id = Northside, Name = "Northside" },
            new Locations { Location_Id = Southside, Name = "Southside" });
        _context.Users.AddRange(
            new User { User_id = StaffId, Email = "staff@example.com", Role = UserRoles.Staff, Location_id = Northside },
            new User { User_id = UnassignedStaffId, Email = "new@example.com", Role = UserRoles.Staff },
            new User { User_id = AdminId, Email = "admin@example.com", Role = UserRoles.Admin, Location_id = Northside });
        _context.Menus.Add(new Menu { Menu_id = 1, Name = "Dinner" });
        _context.TableGroups.AddRange(
            new TableGroup { TableGroup_Id = QuietGroup, Group_Name = "Patio", Location_Id = Northside },
            new TableGroup { TableGroup_Id = BusyGroup, Group_Name = "Banquet", Location_Id = Northside },
            new TableGroup { TableGroup_Id = SouthGroup, Group_Name = "Booths", Location_Id = Southside });
        _context.Tables.AddRange(
            Table(FreeTable, 1, Northside),
            Table(BusyTable, 2, Northside),
            Table(UsedTable, 3, Northside),
            Table(BusyGroupTable, 4, Northside, group: BusyGroup),
            Table(InactiveTable, 5, Northside, active: false),
            Table(QuietGroupTable, 6, Northside, group: QuietGroup),
            Table(SouthTable, 1, Southside),
            Table(SouthTable7, 7, Southside));
        _context.DiningSessions.AddRange(
            new DiningSession { Session_Id = 100, Menu_Id = 1, Location_Id = Northside, Table_Id = BusyTable, Started_At = DateTime.UtcNow },
            new DiningSession { Session_Id = 101, Menu_Id = 1, Location_Id = Northside, Table_Id = UsedTable, Started_At = DateTime.UtcNow.AddHours(-3), Ended_At = DateTime.UtcNow.AddHours(-2) },
            new DiningSession { Session_Id = 102, Menu_Id = 1, Location_Id = Northside, TableGroup_Id = BusyGroup, Started_At = DateTime.UtcNow });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private static TableEntity Table(int id, int number, int location, bool active = true, int? group = null) => new()
    {
        Table_Id = id,
        table_number = number,
        seat_count = 4,
        is_active = active,
        Location_Id = location,
        TableGroup_Id = group,
        QR_Code = $"https://example.com/t/{id}",
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

    private TableEntityController Controller(int userId = StaffId, UserRoles role = UserRoles.Staff) =>
        new(_context, _logger.Object, new LocationAccessService(_context)) { ControllerContext = As(userId, role) };

    private TableEntityController Admin() => Controller(AdminId, UserRoles.Admin);

    private TableEntity Stored(int id) => _context.Tables.AsNoTracking().Single(t => t.Table_Id == id);


    [Fact]
    public async Task CreateTable_ValidTable_ReturnsOk()
    {
        // Act
        var result = await Controller().CreateTable(new TableEntityCreateDto
        {
            TableNumber = 9,
            SeatCount = 6,
            IsActive = false,
            QrCodeUrl = "https://example.com/qr/9",
            LocationId = Northside,
        });

        // Assert
        var dto = result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<TableEntityResponseDto>().Subject;
        dto.TableId.Should().BePositive();
        dto.Should().BeEquivalentTo(new { TableNumber = 9, SeatCount = 6, IsActive = false, QrCodeUrl = "https://example.com/qr/9", LocationId = Northside, TableGroupId = (int?)null });
        Stored(dto.TableId).seat_count.Should().Be(6);
    }

    [Fact]
    public async Task CreateTable_UnknownLocation_ReturnsBadRequest()
    {
        // Act
        var result = await Admin().CreateTable(new TableEntityCreateDto { TableNumber = 9, SeatCount = 2, LocationId = 99 });

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        _context.Tables.Count().Should().Be(8);
    }

    [Fact]
    public async Task CreateTable_NumberUsedAtSameLocation_ReturnsBadRequest()
    {
        // Act
        var result = await Controller().CreateTable(new TableEntityCreateDto { TableNumber = 1, SeatCount = 2, LocationId = Northside });

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        _context.Tables.Count(t => t.Location_Id == Northside && t.table_number == 1).Should().Be(1);
    }

    [Fact]
    public async Task ListEmptyTables_Staff_LeavesOutInactiveTablesAndTablesInUse()
    {
        // Act
        var result = await Controller().ListEmptyTables(locationId: null);

        // Assert
        var tables = (result.Result as OkObjectResult)!.Value as IEnumerable<TableEntityResponseDto>;
        tables!.Select(t => t.TableId).Should().BeEquivalentTo(new[] { FreeTable, UsedTable, QuietGroupTable });
    }

    [Fact]
    public async Task ListEmptyTables_StaffWithNoLocation_ReturnsForbidden()
    {
        // Act
        var result = await Controller(UnassignedStaffId).ListEmptyTables(locationId: Northside);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task GetTable_Exists_ReturnsOk()
    {
        // Act
        var result = await Controller().GetTable(QuietGroupTable);

        // Assert
        var dto = result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<TableEntityResponseDto>().Subject;
        dto.Should().BeEquivalentTo(new { TableId = QuietGroupTable, TableNumber = 6, SeatCount = 4, IsActive = true, LocationId = Northside, TableGroupId = (int?)QuietGroup });
    }

    [Fact]
    public async Task UpdateTable_EveryField_ReturnsOk()
    {
        // Act
        var result = await Controller().UpdateTable(FreeTable, new TableEntityUpdateDto
        {
            TableNumber = 20,
            SeatCount = 8,
            IsActive = false,
            QrCodeUrl = "https://example.com/new",
        });

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var saved = Stored(FreeTable);
        saved.table_number.Should().Be(20);
        saved.seat_count.Should().Be(8);
        saved.is_active.Should().BeFalse();
        saved.QR_Code.Should().Be("https://example.com/new");
    }

    [Fact]
    public async Task UpdateTable_NumberUsedAtSameLocation_ReturnsBadRequest()
    {
        // Act
        var result = await Controller().UpdateTable(FreeTable, new TableEntityUpdateDto { TableNumber = 2 });

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        Stored(FreeTable).table_number.Should().Be(1);
    }

    [Fact]
    public async Task UpdateTable_NumberOnlyUsedAtAnotherLocation_ReturnsOk()
    {
        // Act
        var result = await Controller().UpdateTable(FreeTable, new TableEntityUpdateDto { TableNumber = 7 });

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        Stored(FreeTable).table_number.Should().Be(7);
    }

    [Fact]
    public async Task DeleteTable_GroupNotInSession_ReturnsOk()
    {
        // Act
        var result = await Admin().DeleteTable(QuietGroupTable);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _context.Tables.Any(t => t.Table_Id == QuietGroupTable).Should().BeFalse();
    }

    [Theory]
    [InlineData(BusyTable, "Cannot delete a table that is currently in use in an active session")]
    [InlineData(UsedTable, "Cannot delete a table that has previously been used in a session")]
    [InlineData(BusyGroupTable, "Cannot delete a table that is part of a table group in an active session")]
    public async Task DeleteTable_InUseOrUsedBefore_ReturnsBadRequest(int tableId, string message)
    {
        // Act
        var result = await Admin().DeleteTable(tableId);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>().Which.Value.Should().Be(message);
        _context.Tables.Any(t => t.Table_Id == tableId).Should().BeTrue();
    }

    [Theory]
    [InlineData(InactiveTable, true)]
    [InlineData(QuietGroupTable, false)]
    public async Task ToggleTableStatus_NotInUse_ReturnsOk(int tableId, bool expected)
    {
        // Act
        var result = await Controller().ToggleTableStatus(tableId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        Stored(tableId).is_active.Should().Be(expected);
    }

    [Theory]
    [InlineData(BusyTable, "Cannot deactivate table that is currently in use in an active session")]
    [InlineData(BusyGroupTable, "Cannot deactivate table that is part of a table group in an active session")]
    public async Task ToggleTableStatus_InUse_ReturnsBadRequest(int tableId, string message)
    {
        // Act
        var result = await Controller().ToggleTableStatus(tableId);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>().Which.Value.Should().Be(message);
        Stored(tableId).is_active.Should().BeTrue();
    }

    [Theory]
    [InlineData(QuietGroupTable, true, "Table is available")]
    [InlineData(BusyTable, false, "Table is in an active session")]
    [InlineData(BusyGroupTable, false, "Table is part of a table group in an active session")]
    public async Task CheckTableActiveSession_ReturnsFreeTablesStatus(int tableId, bool success, string message)
    {
        // Act
        var result = await Controller().CheckTableActiveSession(tableId);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.SerializeToElement(ok.Value);
        json.GetProperty("success").GetBoolean().Should().Be(success);
        json.GetProperty("message").GetString().Should().Be(message);
    }

    [Fact]
    public async Task AssignTableToGroup_SameLocation_ReturnsOk()
    {
        // Act
        var result = await Controller().AssignTableToGroup(FreeTable, QuietGroup);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        Stored(FreeTable).TableGroup_Id.Should().Be(QuietGroup);
    }

    [Fact]
    public async Task AssignTableToGroup_GroupAtAnotherLocation_ReturnsBadRequest()
    {
        // Act
        var result = await Admin().AssignTableToGroup(FreeTable, SouthGroup);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        Stored(FreeTable).TableGroup_Id.Should().BeNull();
    }

    [Fact]
    public async Task AssignTableToGroup_TableInActiveSession_ReturnsBadRequest()
    {
        // Act
        var result = await Controller().AssignTableToGroup(BusyTable, QuietGroup);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        Stored(BusyTable).TableGroup_Id.Should().BeNull();
    }

    [Fact]
    public async Task RemoveTableFromGroup_GroupNotInSession_ReturnsOk()
    {
        // Act
        var result = await Controller().RemoveTableFromGroup(QuietGroupTable);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        Stored(QuietGroupTable).TableGroup_Id.Should().BeNull();
    }

    [Fact]
    public async Task RemoveTableFromGroup_TableHasNoGroup_ReturnsBadRequest()
    {
        // Act
        var result = await Controller().RemoveTableFromGroup(FreeTable);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RemoveTableFromGroup_GroupInActiveSession_ReturnsBadRequest()
    {
        // Act
        var result = await Controller().RemoveTableFromGroup(BusyGroupTable);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        Stored(BusyGroupTable).TableGroup_Id.Should().Be(BusyGroup);
    }
}