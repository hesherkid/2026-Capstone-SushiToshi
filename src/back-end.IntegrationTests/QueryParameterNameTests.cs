using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using back_end.domain.DbContexts;
using back_end.Helpers;

namespace back_end.IntegrationTests;

// Query parameters are snake_case like the JSON keys, and in development a parameter the action
// doesn't take is rejected. Binding ignores case but not underscores, so without the check an
// old ?locationId= call would quietly stop filtering instead of failing
public class QueryParameterNameTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Regex SnakeCase = new("^[a-z][a-z0-9]*(_[a-z0-9]+)*$");

    private readonly WebApplicationFactory<Program> _baseFactory;

    public QueryParameterNameTests(WebApplicationFactory<Program> factory)
    {
        _baseFactory = factory;
    }

    // Same in-memory swap as JsonUnknownKeyTests
    private WebApplicationFactory<Program> Factory(bool? rejectUnknownParameters = null)
    {
        var databaseName = "QueryParameterName" + Guid.NewGuid();
        return _baseFactory.WithWebHostBuilder(builder =>
        {
            if (rejectUnknownParameters.HasValue)
                builder.UseSetting("Query:RejectUnknownParameters", rejectUnknownParameters.Value.ToString());
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));
            });
        });
    }

    // Every action's query parameter names, as "GET api/Menu/{menu}/item" -> names
    private static Dictionary<string, HashSet<string>> QueryNamesByAction(WebApplicationFactory<Program> factory)
    {
        var actions = factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items;
        var metadata = factory.Services.GetRequiredService<IModelMetadataProvider>();
        return actions
            .OfType<ControllerActionDescriptor>()
            .GroupBy(a => $"{a.ActionConstraints?.OfType<Microsoft.AspNetCore.Mvc.ActionConstraints.HttpMethodActionConstraint>().FirstOrDefault()?.HttpMethods.First()} {a.AttributeRouteInfo?.Template}")
            .ToDictionary(g => g.Key, g => g.SelectMany(a => UnknownQueryParameterFilter.QueryNames(a.Parameters, metadata)).ToHashSet());
    }

    private static async Task<(int Status, string Body)> Get(WebApplicationFactory<Program> factory, string path, string query)
    {
        var context = await factory.Server.SendAsync(c =>
        {
            c.Request.Method = HttpMethods.Get;
            c.Request.Path = path;
            c.Request.QueryString = new QueryString(query);
        });
        return (context.Response.StatusCode, await new StreamReader(context.Response.Body).ReadToEndAsync());
    }

    [Fact]
    public void EveryQueryParameter_IsSnakeCase()
    {
        using var factory = Factory();

        var notSnakeCase = QueryNamesByAction(factory)
            .SelectMany(action => action.Value.Where(name => !SnakeCase.IsMatch(name)).Select(name => $"{action.Key}: {name}"))
            .ToList();

        notSnakeCase.Should().BeEmpty();
    }

    [Theory]
    [InlineData("GET api/Dashboard/sessions", "location_id")]
    [InlineData("GET api/Order/all-orders", "location_id", "delivered_open_sessions_only")]
    [InlineData("GET api/Order/history", "from", "to", "location_id", "table_number", "name")]
    [InlineData("GET api/Bill/get-bills/{sessionId}", "table_id")]
    [InlineData("GET api/Bill/summary/{billId}", "session_id")]
    [InlineData("GET api/DiningSession/get-list-dining-sessions", "active_only")]
    [InlineData("GET api/TableGroup", "active_only", "location_id")]
    [InlineData("GET api/admin/qr/wifi", "location_id", "table", "refresh")]
    [InlineData("GET api/analytics/browsing-behavior", "range", "from", "to", "location_id", "category_id", "tag_id", "top", "min_views")]
    public void Action_TakesTheseQueryParameters(string action, params string[] expected)
    {
        using var factory = Factory();

        var names = QueryNamesByAction(factory);

        names.Should().ContainKey(action);
        names[action].Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task UnknownQueryParameter_InDevelopment_Returns400NamingItAndTheRightName()
    {
        await using var factory = Factory();

        var (status, body) = await Get(factory, "/api/menu/999/item", "?locationId=1");

        status.Should().Be(StatusCodes.Status400BadRequest);
        body.Should().Contain("locationId").And.Contain("location_id");
    }

    [Fact]
    public async Task KnownQueryParameter_InDevelopment_IsAccepted()
    {
        await using var factory = Factory();

        var (status, _) = await Get(factory, "/api/menu/999/item", "?location_id=1");

        // The menu doesn't exist, but the parameter got through
        status.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task QueryParameter_DiffersOnlyInCase_IsAccepted()
    {
        await using var factory = Factory();

        // Binding ignores case, so the check does too
        var (status, _) = await Get(factory, "/api/menu/999/item", "?Location_Id=1");

        status.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task UnknownQueryParameter_WithSettingOff_IsIgnored()
    {
        await using var factory = Factory(rejectUnknownParameters: false);

        var (status, _) = await Get(factory, "/api/menu/999/item", "?locationId=1");

        status.Should().Be(StatusCodes.Status404NotFound);
    }
}
