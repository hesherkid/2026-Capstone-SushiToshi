using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.Controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.Analytics;
using back_end.Services.Analytics;

namespace back_end.Tests.Controllers;

public class TableTurnoverControllerTests : IDisposable
{
    private const string WeekFrom = "2025-01-06";
    private const string WeekTo = "2025-01-12";

    private const int North = 1, South = 2;
    private const int DinnerMenu = 1;
    private const int Table1 = 1, Table2 = 2, Table3 = 3, Table4 = 4, SouthTable = 5;
    private const int Group1 = 1;

    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<TableTurnoverController>> _mockLogger;
    private readonly TableTurnoverController _controller;
    private int _nextSessionId = 1;
    private int _nextBillId = 1;

    public TableTurnoverControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _mockLogger = new Mock<ILogger<TableTurnoverController>>();

        _controller = new TableTurnoverController(
            new TableTurnoverService(_context, TimeProvider.System),
            _mockLogger.Object);

        SeedTestData();
    }

    private static DateTime Mst(int day, int hour, int minute = 0) =>
        new DateTime(2025, 1, day, hour, minute, 0, DateTimeKind.Utc).AddHours(7);

    private void SeedTestData()
    {
        _context.Locations.AddRange(
            new Locations { Location_Id = North, Name = "North", Address_Primary = "1 North St", City = "Edmonton", Province = "AB", Postal_Code = "T5K 2B7", Phone_Number = "780-000-0001" },
            new Locations { Location_Id = South, Name = "South", Address_Primary = "1 South St", City = "Edmonton", Province = "AB", Postal_Code = "T6H 1A1", Phone_Number = "780-000-0002" });

        _context.Menus.Add(
            new Menu { Menu_id = DinnerMenu, Name = "Dinner", Description = "Dinner", Start_time = new TimeOnly(16, 0), End_time = new TimeOnly(22, 0), Is_active = true });

        _context.TableGroups.Add(
            new TableGroup { TableGroup_Id = Group1, Group_Name = "Banquet Area", Location_Id = North });

        _context.Tables.AddRange(
            new TableEntity { Table_Id = Table1, table_number = 1, seat_count = 2, is_active = true, Location_Id = North },
            new TableEntity { Table_Id = Table2, table_number = 2, seat_count = 4, is_active = true, Location_Id = North },
            new TableEntity { Table_Id = Table3, table_number = 3, seat_count = 4, is_active = true, Location_Id = North },
            new TableEntity { Table_Id = Table4, table_number = 4, seat_count = 4, is_active = true, Location_Id = North },
            new TableEntity { Table_Id = SouthTable, table_number = 1, seat_count = 4, is_active = true, Location_Id = South });

        _context.SaveChanges();
    }

    private int AddSession(
        DateTime startedAt,
        DateTime? endedAt = null,
        int? tableId = Table1,
        int? groupId = null,
        int location = North,
        int? guests = null)
    {
        var session = new DiningSession
        {
            Session_Id = _nextSessionId++,
            Location_Id = location,
            Menu_Id = DinnerMenu,
            Started_At = startedAt,
            Ended_At = endedAt,
            Table_Id = groupId.HasValue ? null : tableId,
            TableGroup_Id = groupId
        };

        _context.DiningSessions.Add(session);

        _context.SaveChanges();

        if (guests.HasValue)
        {
            AddBill(session.Session_Id, guests.Value);
        }

        return session.Session_Id;
    }

    private void AddBill(int sessionId, int total, BillStatus status = BillStatus.Closed)
    {
        _context.Bills.Add(new Billing
        {
            Bill_Id = _nextBillId++,
            Session_Id = sessionId,
            Bill_Name = $"Bill {_nextBillId}",
            Adult_Count = total,
            Total_Count = total,
            Status = status
        });

        _context.SaveChanges();
    }

    private async Task<TableTurnoverResponseDTO> GetWeekAsync(int? locationId = North)
    {
        var result = await _controller.GetTableTurnover("custom", WeekFrom, WeekTo, locationId);

        result.Should().BeOfType<OkObjectResult>();
        var value = (result as OkObjectResult)!.Value;
        value.Should().BeOfType<TableTurnoverResponseDTO>();
        return (TableTurnoverResponseDTO)value!;
    }

    [Fact]
    public async Task GetTableTurnover_BusiestTime_PeaksBySeatingsAndGuestsCanDiffer()
    {
        // Arrange
        AddSession(Mst(7, 17, 5), Mst(7, 18), tableId: Table1, guests: 2);
        AddSession(Mst(7, 17, 30), Mst(7, 18, 30), tableId: Table2, guests: 2);
        AddSession(Mst(7, 18, 15), Mst(7, 19, 30), groupId: Group1, guests: 8);

        // Act
        var response = await GetWeekAsync();

        // Assert
        response.SeatingsByHour.Should().HaveCount(24);
        response.PeakBySeatings!.Hour.Should().Be(17);
        response.PeakBySeatings.Seatings.Should().Be(2);
        response.PeakByGuests!.Hour.Should().Be(18);
        response.PeakByGuests.Guests.Should().Be(8);
        response.Summary.SessionsSeated.Should().Be(3);
        response.Summary.GuestsSeated.Should().Be(12);
    }

    [Fact]
    public async Task GetTableTurnover_PartySize_SumsSplitBillsAndIgnoresCancelled()
    {
        // Arrange
        var split = AddSession(Mst(7, 17), Mst(7, 18), tableId: Table1);
        AddBill(split, 2);
        AddBill(split, 3);
        AddBill(split, 4, BillStatus.Cancelled);

        // Act
        var response = await GetWeekAsync();

        // Assert
        response.DurationByPartySize.Should().ContainSingle()
            .Which.PartySize.Should().Be("5");
        response.Summary.GuestsSeated.Should().Be(5);
    }

    [Fact]
    public async Task GetTableTurnover_DurationByPartySize_BucketsClosedSessionsInOrder()
    {
        // Arrange
        AddSession(Mst(7, 17), Mst(7, 18), tableId: Table1, guests: 2);
        AddSession(Mst(7, 17), Mst(7, 18, 40), tableId: Table2, guests: 2);
        AddSession(Mst(7, 17), tableId: Table3, guests: 2);
        AddSession(Mst(7, 17), Mst(7, 18, 30), groupId: Group1, guests: 7);
        AddSession(Mst(7, 17), Mst(7, 18), tableId: Table4);

        // Act
        var response = await GetWeekAsync();

        // Assert
        response.DurationByPartySize.Select(b => b.PartySize)
            .Should().Equal("2", "6+", "unknown");

        var twos = response.DurationByPartySize[0];
        twos.Sessions.Should().Be(3);
        twos.OverTimeLimit.Should().Be(1);
        twos.Duration.Count.Should().Be(2);
        twos.Duration.AverageMinutes.Should().Be(80.0m);

        response.Summary.SessionsOpen.Should().Be(1);
        response.Summary.SessionsWithoutPartySize.Should().Be(1);
        response.Summary.SessionsOverTimeLimit.Should().Be(1);
    }

    [Fact]
    public async Task GetTableTurnover_TurnoverGap_TimesSameDayGapsPerUnit()
    {
        // Arrange
        AddSession(Mst(7, 17), Mst(7, 18), tableId: Table1);
        AddSession(Mst(7, 18, 10), Mst(7, 19, 30), tableId: Table1);
        AddSession(Mst(7, 19, 50), Mst(7, 21), tableId: Table1);
        AddSession(Mst(7, 17), Mst(7, 18), tableId: Table2);
        AddSession(Mst(7, 21), Mst(7, 22), tableId: Table2);
        AddSession(Mst(7, 17), Mst(7, 18, 30), tableId: Table3);
        AddSession(Mst(7, 18), Mst(7, 19), tableId: Table3);
        AddSession(Mst(7, 20), Mst(7, 21), tableId: Table4);
        AddSession(Mst(8, 17), Mst(8, 18), tableId: Table4);
        AddSession(Mst(7, 17), Mst(7, 18, 30), groupId: Group1);
        AddSession(Mst(7, 18, 45), Mst(7, 20), groupId: Group1);

        // Act
        var response = await GetWeekAsync();

        // Assert
        response.TurnoverGap.Tables.Count.Should().Be(2);
        response.TurnoverGap.Tables.AverageMinutes.Should().Be(15.0m);
        response.TurnoverGap.Tables.MedianMinutes.Should().Be(15.0m);
        response.TurnoverGap.Groups.Count.Should().Be(1);
        response.TurnoverGap.Groups.AverageMinutes.Should().Be(15.0m);
        response.TurnoverGap.GapsOverLimit.Should().Be(1);
        response.TurnoverGap.OverlappingSessions.Should().Be(1);
        response.Summary.MaxGapMinutes.Should().Be(TableTurnoverService.MaxGapMinutes);
    }

    [Fact]
    public async Task GetTableTurnover_TurnoverGap_SkipsWhenPreviousSessionStillOpen()
    {
        // Arrange
        AddSession(Mst(7, 17), tableId: Table1);
        AddSession(Mst(7, 18), Mst(7, 19), tableId: Table1);

        // Act
        var response = await GetWeekAsync();

        // Assert
        response.TurnoverGap.Tables.Count.Should().Be(0);
        response.TurnoverGap.OverlappingSessions.Should().Be(0);
    }

    [Fact]
    public async Task GetTableTurnover_LocationFilter_OnlyCountsThatLocation()
    {
        // Arrange
        AddSession(Mst(7, 17), Mst(7, 18), tableId: Table1, guests: 2);
        AddSession(Mst(7, 17), Mst(7, 18), tableId: SouthTable, location: South, guests: 4);

        // Act
        var north = await GetWeekAsync(North);
        var all = await GetWeekAsync(null);

        // Assert
        north.Summary.SessionsSeated.Should().Be(1);
        north.Summary.GuestsSeated.Should().Be(2);
        north.Location!.Name.Should().Be("North");
        all.Summary.SessionsSeated.Should().Be(2);
        all.Summary.GuestsSeated.Should().Be(6);
        all.Location.Should().BeNull();
    }

    [Fact]
    public async Task GetTableTurnover_SessionsOutsideWindow_AreIgnored()
    {
        // Arrange
        AddSession(Mst(5, 17), Mst(5, 18), tableId: Table1, guests: 2);
        AddSession(Mst(13, 17), Mst(13, 18), tableId: Table1, guests: 2);

        // Act
        var response = await GetWeekAsync();

        // Assert
        response.Summary.SessionsSeated.Should().Be(0);
    }

    [Fact]
    public async Task GetTableTurnover_UnknownLocation_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetTableTurnover("custom", WeekFrom, WeekTo, 999);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Theory]
    [InlineData("weekly", null, null, "range")]
    [InlineData("custom", null, WeekTo, "from")]
    [InlineData("custom", WeekTo, WeekFrom, "from")]
    public async Task GetTableTurnover_InvalidRange_ReturnsValidationProblem(
        string range, string? from, string? to, string expectedKey)
    {
        // Act
        var result = await _controller.GetTableTurnover(range, from, to, North);

        // Assert
        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        var problem = objectResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problem.Errors.Should().ContainKey(expectedKey);
    }

    [Fact]
    public async Task GetTableTurnover_NoData_ReturnsOkWithEmptyResults()
    {
        // Act
        var response = await GetWeekAsync();

        // Assert
        response.Summary.SessionsSeated.Should().Be(0);
        response.SeatingsByHour.Should().HaveCount(24);
        response.PeakBySeatings.Should().BeNull();
        response.PeakByGuests.Should().BeNull();
        response.DurationByPartySize.Should().BeEmpty();
        response.TurnoverGap.Tables.AverageMinutes.Should().BeNull();
        response.TurnoverGap.Groups.Count.Should().Be(0);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
