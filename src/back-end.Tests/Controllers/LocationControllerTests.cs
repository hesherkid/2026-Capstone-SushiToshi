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
using back_end.Dtos;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace back_end.Tests.Controllers;

public class LocationControllerTests : IDisposable
{
    private const int Northside = 1;
    private const int Southside = 2;
    private const int Quiet = 3;
    private const int NoSuchLocation = 99;
    private const int NorthTable = 11;
    private const int PatioTable = 12;
    private const int SouthTable = 21;
    private const int PatioGroup = 50;
    private const int BarGroup = 51;
    private const int OpenSession = 100;
    private const int EndedSession = 101;
    private const int SouthSession = 200;
    private const int LunchMenu = 1;
    private const int DinnerMenu = 2;

    private const string ServerErrorMessage = "An error occurred while processing your request.";

    private readonly DateTime _now = DateTime.UtcNow;
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly ApplicationDbContext _context;
    private readonly LocationController _controller;
    private readonly Mock<ILogger<LocationController>> _logger = new();

    public LocationControllerTests()
    {
        using (var seed = NewContext())
        {
            seed.Locations.AddRange(
                new Locations { Location_Id = Quiet, Name = "Quiet", Address_Primary = "9 Side St", City = "Red Deer", Province = "AB", Postal_Code = "T4N 1A1", Phone_Number = "4035550000" },
                new Locations { Location_Id = Northside, Name = "Northside", Address_Primary = "123 Main St", City = "Edmonton", Province = "AB", Postal_Code = "T5K 2B7", Phone_Number = "780-123-4567" },
                new Locations { Location_Id = Southside, Name = "Southside", Address_Primary = "456 West Rd", City = "Calgary", Province = "AB", Postal_Code = "T2P 1A1", Phone_Number = "403-987-6543" });

            seed.Menus.AddRange(
                new Menu { Menu_id = LunchMenu, Name = "Lunch" },
                new Menu { Menu_id = DinnerMenu, Name = "Dinner" });
            seed.MenuLocations.AddRange(
                new MenuLocations { Location_Id = Northside, Menu_Id = LunchMenu },
                new MenuLocations { Location_Id = Northside, Menu_Id = DinnerMenu },
                new MenuLocations { Location_Id = Southside, Menu_Id = LunchMenu });

            seed.TableGroups.AddRange(
                new TableGroup { TableGroup_Id = PatioGroup, Group_Name = "Patio", Is_Active = true, Location_Id = Northside },
                new TableGroup { TableGroup_Id = BarGroup, Group_Name = "Bar", Is_Active = false, Location_Id = Northside });
            seed.Tables.AddRange(
                new TableEntity { Table_Id = NorthTable, table_number = 1, seat_count = 2, is_active = false, QR_Code = "qr-1", Location_Id = Northside },
                new TableEntity { Table_Id = PatioTable, table_number = 2, seat_count = 4, is_active = true, QR_Code = "qr-2", Location_Id = Northside, TableGroup_Id = PatioGroup },
                new TableEntity { Table_Id = SouthTable, table_number = 5, seat_count = 6, is_active = true, QR_Code = "qr-5", Location_Id = Southside });

            seed.DiningSessions.AddRange(
                new DiningSession { Session_Id = OpenSession, Menu_Id = LunchMenu, Location_Id = Northside, Table_Id = NorthTable, Started_At = _now.AddMinutes(-10) },
                new DiningSession { Session_Id = EndedSession, Menu_Id = LunchMenu, Location_Id = Northside, TableGroup_Id = PatioGroup, Started_At = _now.AddHours(-2), Ended_At = _now.AddHours(-1) },
                new DiningSession { Session_Id = SouthSession, Menu_Id = LunchMenu, Location_Id = Southside, Table_Id = SouthTable, Started_At = _now });
            seed.SessionParticipants.AddRange(
                new SessionParticipant { Participant_Id = 1, Session_Id = OpenSession, User_Name = "Ana" },
                new SessionParticipant { Participant_Id = 2, Session_Id = OpenSession, User_Name = "Ben" });

            seed.SaveChanges();
        }

        _context = NewContext();
        _controller = new LocationController(_context, _logger.Object);
    }

    public void Dispose() => _context.Dispose();

    private ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(_dbName).Options);

    private LocationController Broken()
    {
        var disposed = NewContext();
        disposed.Dispose();
        return new LocationController(disposed, _logger.Object);
    }
    private void Add(params object[] rows)
    {
        using var db = NewContext();
        db.AddRange(rows);
        db.SaveChanges();
    }

    private Locations? Saved(int locationId)
    {
        using var db = NewContext();
        return db.Locations.SingleOrDefault(l => l.Location_Id == locationId);
    }

    private static JsonElement OkJson(IActionResult result) =>
        JsonSerializer.SerializeToElement(result.Should().BeOfType<OkObjectResult>().Subject.Value);

    private static LocationCreateDto NewLocation(string? provinceCode = null) => new()
    {
        Name = "South Side",
        Address_One = "789 South Ave",
        Address_Two = "Unit 4",
        City = "Calgary",
        Province = "Alberta",
        Province_Code = provinceCode,
        Postal_Code = "T2P 1A1",
        Phone_Number = "4035550100"
    };

    [Fact]
    public async Task GetAllLocations_ReturnsAllLocationsInOrder()
    {
        var result = await _controller.GetAllLocations();

        var locations = result.Result.Should().BeOfType<OkObjectResult>()
            .Subject.Value.Should().BeAssignableTo<List<Locations>>().Subject;
        locations.Select(l => l.Location_Id).Should().Equal(Northside, Southside, Quiet);
    }

    [Fact]
    public async Task GetAllLocations_IsEmptyWhenNoLocationsExist()
    {
        using (var db = NewContext())
        {
            db.Locations.RemoveRange(db.Locations);
            db.SaveChanges();
        }

        var result = await _controller.GetAllLocations();

        result.Result.Should().BeOfType<OkObjectResult>()
            .Subject.Value.Should().BeAssignableTo<List<Locations>>()
            .Subject.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateLocation_SavesEveryField()
    {
        var result = await _controller.CreateLocation(NewLocation(provinceCode: "BC"));

        result.Should().BeOfType<OkObjectResult>();
        using var db = NewContext();
        db.Locations.Single(l => l.Name == "South Side").Should().BeEquivalentTo(new
        {
            Address_Primary = "789 South Ave",
            Address_Secondary = "Unit 4",
            City = "Calgary",
            Province = "Alberta",
            Province_Code = "BC",
            Postal_Code = "T2P 1A1",
            Phone_Number = "4035550100",
        });
    }

    [Fact]
    public async Task CreateLocation_UsesAlbertaAsDefault()
    {
        await _controller.CreateLocation(NewLocation(provinceCode: null));

        using var db = NewContext();
        db.Locations.Single(l => l.Name == "South Side").Province_Code.Should().Be("AB");
    }

    [Fact]
    public async Task GetLocations_ReturnsTheLocationAskedFor()
    {
        var result = await _controller.GetLocations(Southside);

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<Locations>()
            .Which.Name.Should().Be("Southside");
    }

    [Fact]
    public async Task GetLocations_WhenALocationDoesntExist_ReturnsNotFound()
    {
        var result = await _controller.GetLocations(NoSuchLocation);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateLocation_WhenALocationDoesntExist_ReturnsNotFound()
    {
        var result = await _controller.UpdateLocation(NoSuchLocation, new LocationUpdateDto { Name = "Nope" });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateLocation_SavesEveryFieldGiven()
    {
        var result = await _controller.UpdateLocation(Northside, new LocationUpdateDto
        {
            Name = "Northside Renamed",
            Address_One = "1 New St",
            Address_Two = "Floor 2",
            City = "St. Albert",
            Province = "Alberta",
            Province_Code = "SK",
            Postal_Code = "T8N 1A1",
            Phone_Number = "7805550199"
        });

        result.Should().BeOfType<OkObjectResult>();
        Saved(Northside).Should().BeEquivalentTo(new
        {
            Name = "Northside Renamed",
            Address_Primary = "1 New St",
            Address_Secondary = "Floor 2",
            City = "St. Albert",
            Province = "Alberta",
            Province_Code = "SK",
            Postal_Code = "T8N 1A1",
            Phone_Number = "7805550199",
        });
    }

    [Fact]
    public async Task UpdateLocation_LeavesOutFieldsUnchanged()
    {
        var result = await _controller.UpdateLocation(Northside, new LocationUpdateDto { Name = "Just A Rename" });

        result.Should().BeOfType<OkObjectResult>();
        Saved(Northside).Should().BeEquivalentTo(new
        {
            Name = "Just A Rename",
            Address_Primary = "123 Main St",
            City = "Edmonton",
            Postal_Code = "T5K 2B7",
            Phone_Number = "780-123-4567",
            Province_Code = "AB",
        });
    }

    [Fact]
    public async Task DeleteLocation_WhenALocationDoesntExist_ReturnsNotFound()
    {
        var result = await _controller.DeleteLocation(NoSuchLocation);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteLocation_RemovesALocationWithNothingAttached()
    {
        var result = await _controller.DeleteLocation(Quiet);

        result.Should().BeOfType<OkObjectResult>();
        Saved(Quiet).Should().BeNull();
    }

    [Fact]
    public async Task DeleteLocation_WhileASessionIsOpen_ReturnsBadRequest()
    {
        var result = await _controller.DeleteLocation(Northside);

        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().Be("Cannot delete location with active dining sessions");
        Saved(Northside).Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteLocation_ByEndedSessionsIsAllowed()
    {
        Add(new DiningSession { Session_Id = 300, Menu_Id = LunchMenu, Location_Id = Quiet, Started_At = _now.AddHours(-2), Ended_At = _now.AddHours(-1) });

        var result = await _controller.DeleteLocation(Quiet);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DeleteLocation_WhileTheLocationHasTables_ReturnsBadRequest()
    {
        Add(new TableEntity { Table_Id = 30, table_number = 1, seat_count = 2, Location_Id = Quiet });

        var result = await _controller.DeleteLocation(Quiet);

        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.As<string>().Should().Contain("existing tables");
        Saved(Quiet).Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteLocation_WhileTheLocationHasTableGroups_ReturnsBadRequest()
    {
        Add(new TableGroup { TableGroup_Id = 60, Group_Name = "Loft", Location_Id = Quiet });

        var result = await _controller.DeleteLocation(Quiet);

        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.As<string>().Should().Contain("existing table groups");
        Saved(Quiet).Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteLocation_MovesStaffWorkingThereToAnotherAssignedLocation()
    {
        Add(
            new User { User_id = 10, Email = "two@example.com", Role = UserRoles.Staff, Location_id = Quiet },
            new User { User_id = 11, Email = "one@example.com", Role = UserRoles.Staff, Location_id = Quiet },
            new StaffLocation { User_Id = 10, Location_Id = Quiet },
            new StaffLocation { User_Id = 10, Location_Id = Northside },
            new StaffLocation { User_Id = 11, Location_Id = Quiet });

        var result = await _controller.DeleteLocation(Quiet);

        result.Should().BeOfType<OkObjectResult>();
        using var db = NewContext();
        db.Users.Single(u => u.User_id == 10).Location_id.Should().Be(Northside);
        db.Users.Single(u => u.User_id == 11).Location_id.Should().BeNull();

    }

    [Fact]
    public async Task ListMenus_ListsOnlyThatLocationsMenus()
    {
        var result = await _controller.ListLocationMenus(Northside);

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<int>>()
            .Which.Should().BeEquivalentTo(new[] { LunchMenu, DinnerMenu });
    }

    [Fact]
    public async Task ListMenus_ALocationThatDoesntExist_ReturnsNotFound()
    {
        var result = await _controller.ListLocationMenus(NoSuchLocation);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AddMenu_CanBeAddedToALocation()
    {
        var result = await _controller.AddMenuToLocation(Quiet, new MenuLocationCreateDto { Menu_id = DinnerMenu });

        result.Should().BeOfType<OkObjectResult>();
        using var db = NewContext();
        db.MenuLocations.Should().ContainSingle(ml => ml.Location_Id == Quiet && ml.Menu_Id == DinnerMenu);
    }

    [Fact]
    public async Task AddMenu_ALocationThatDoesntExist_ReturnsNotFound()
    {
        var result = await _controller.AddMenuToLocation(NoSuchLocation, new MenuLocationCreateDto { Menu_id = LunchMenu });

        result.Should().BeOfType<NotFoundObjectResult>();
        using var db = NewContext();
        db.MenuLocations.Should().NotContain(ml => ml.Location_Id == NoSuchLocation);
    }

    [Fact]
    public async Task RemoveMenu_RemovingOneLeavesOtherLocationsAlone()
    {
        var result = await _controller.RemoveMenuFromLocation(Northside, LunchMenu);

        result.Should().BeOfType<OkObjectResult>();
        using var db = NewContext();
        db.MenuLocations.Should().NotContain(ml => ml.Location_Id == Northside && ml.Menu_Id == LunchMenu);
        db.MenuLocations.Should().Contain(ml => ml.Location_Id == Southside && ml.Menu_Id == LunchMenu);
        db.MenuLocations.Should().Contain(ml => ml.Location_Id == Northside && ml.Menu_Id == DinnerMenu);
    }

    [Fact]
    public async Task RemoveMenu_RemovingOneThatIsntThere_ReturnsNotFound()
    {
        var result = await _controller.RemoveMenuFromLocation(Quiet, LunchMenu);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Tables_ListOnlyThatLocationByTableNumberWithTableGroup()
    {
        var tables = OkJson(await _controller.GetLocationTables(Northside));

        tables.EnumerateArray().Select(t => t.GetProperty("table_number").GetInt32()).Should().Equal(1, 2);
        tables[0].GetProperty("tableGroup_Name").ValueKind.Should().Be(JsonValueKind.Null);

        var patio = tables[1];
        patio.GetProperty("table_Id").GetInt32().Should().Be(PatioTable);
        patio.GetProperty("seat_count").GetInt32().Should().Be(4);
        patio.GetProperty("is_active").GetBoolean().Should().BeTrue();
        patio.GetProperty("qr_code").GetString().Should().Be("qr-2");
        patio.GetProperty("tableGroup_Id").GetInt32().Should().Be(PatioGroup);
        patio.GetProperty("tableGroup_Name").GetString().Should().Be("Patio");
    }

    [Fact]
    public async Task Tables_AreEmptyWhenALocationHasNone()
    {
        var tables = OkJson(await _controller.GetLocationTables(Quiet));

        tables.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Tables_ForALocationThatDoesntExist_ReturnsNotFound()
    {
        var result = await _controller.GetLocationTables(NoSuchLocation);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task TableGroups_ListByNameWithTablesAndSeats()
    {
        var groups = OkJson(await _controller.GetLocationTableGroups(Northside));

        groups.EnumerateArray().Select(g => g.GetProperty("group_Name").GetString()).Should().Equal("Bar", "Patio");

        var bar = groups[0];
        bar.GetProperty("is_Active").GetBoolean().Should().BeFalse();
        bar.GetProperty("table_Count").GetInt32().Should().Be(0);
        bar.GetProperty("total_Seats").GetInt32().Should().Be(0);

        var patio = groups[1];
        patio.GetProperty("tableGroup_Id").GetInt32().Should().Be(PatioGroup);
        patio.GetProperty("table_Count").GetInt32().Should().Be(1);
        patio.GetProperty("total_Seats").GetInt32().Should().Be(4);
        patio.GetProperty("tables")[0].GetProperty("table_number").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task TableGroups_ForALocationThatDoesntExist_ReturnsNotFound()
    {
        var result = await _controller.GetLocationTableGroups(NoSuchLocation);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Sessions_ListEveryOneAtThatLocationNewestFirst()
    {
        var sessions = OkJson(await _controller.GetLocationDiningSessions(Northside, activeOnly: null));

        sessions.EnumerateArray().Select(s => s.GetProperty("session_Id").GetInt32()).Should().Equal(OpenSession, EndedSession);
    }

    [Fact]
    public async Task Sessions_ListOpenSessions_WithTableAndGuests()
    {
        var sessions = OkJson(await _controller.GetLocationDiningSessions(Northside, activeOnly: true));

        var open = sessions.EnumerateArray().Should().ContainSingle().Subject;
        open.GetProperty("session_Id").GetInt32().Should().Be(OpenSession);
        open.GetProperty("ended_At").ValueKind.Should().Be(JsonValueKind.Null);
        open.GetProperty("table_Numbers").EnumerateArray().Select(t => t.GetInt32()).Should().Equal(1);
        open.GetProperty("active_Participants").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Sessions_ListEndedSessionsWithGroupsTables()
    {
        var sessions = OkJson(await _controller.GetLocationDiningSessions(Northside, activeOnly: false));

        var ended = sessions.EnumerateArray().Should().ContainSingle().Subject;
        ended.GetProperty("session_Id").GetInt32().Should().Be(EndedSession);
        ended.GetProperty("table_Numbers").EnumerateArray().Select(t => t.GetInt32()).Should().Equal(2);
        ended.GetProperty("active_Participants").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Sessions_ForALocationThatDoesntExist_ReturnsNotFound()
    {
        var result = await _controller.GetLocationDiningSessions(NoSuchLocation, activeOnly: null);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
