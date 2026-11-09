using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using FluentAssertions;
using back_end.Configurations;
using back_end.Dtos;

namespace back_end.Tests.Dtos;

// POST /api/Order with the development setting that rejects keys a DTO doesn't have
public class OrderCreateDtoTests
{
    private static readonly JsonSerializerOptions DevelopmentOptions = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        ApiJsonOptions.Apply(options);
        return options;
    }

    [Fact]
    public void OrderCreateDto_AcceptsTheItemsTheCartSends()
    {
        var order = JsonSerializer.Deserialize<OrderCreateDto>(
            """{"session_id": 42, "bill_id": 8, "items": [{"menu_id": 7, "item_id": 11, "quantity": 2, "price_at_time": 0}]}""",
            DevelopmentOptions);

        order!.Items.Should().ContainSingle().Which.Quantity.Should().Be(2);
    }

    [Fact]
    public void OrderCreateDto_RejectsAnItemName()
    {
        var withName = () => JsonSerializer.Deserialize<OrderCreateDto>(
            """{"session_id": 42, "bill_id": 8, "items": [{"menu_id": 7, "item_id": 11, "quantity": 2, "price_at_time": 0, "name": "Miso Soup"}]}""",
            DevelopmentOptions);

        withName.Should().Throw<JsonException>().WithMessage("*name*");
    }
}
