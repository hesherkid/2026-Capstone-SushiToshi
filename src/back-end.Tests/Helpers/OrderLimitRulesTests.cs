using Xunit;
using FluentAssertions;
using back_end.Helpers;
using back_end.domain.enums;

namespace back_end.Tests.Helpers;

// Pure limit logic: no database, so these run in milliseconds
public class OrderLimitRulesTests
{
    private static readonly OrderLimitRules.Limits Unlimited = new(0, 0, 0, 0, 0, OverLimitBehavior.Block);
    private static readonly OrderLimitRules.Headcount TwoAdults = new(2, 0, 0, 0);
    private static readonly OrderLimitRules.Usage NothingUsed = new(0, 0, 0, 0);

    private static OrderLimitRules.Limits PerPerson(int each, OverLimitBehavior behavior) =>
        new(each, each, each, 0, 0, behavior);

    private static OrderLimitRules.Limits PerOrder(int max) =>
        new(0, 0, 0, 0, max, OverLimitBehavior.Block);

    private static OrderLimitRules.Limits PerGuestPerOrder(int each) =>
        new(0, 0, 0, 0, PerOrder: each, OverLimitBehavior.Block, PerOrderPerGuest: true);

    // SessionCap

    [Fact]
    public void SessionCap_ZeroLimit_IsUnlimited()
    {
        OrderLimitRules.SessionCap(Unlimited, TwoAdults).Should().Be(int.MaxValue);
    }

    [Fact]
    public void SessionCap_MultipliesLimitByHeadcount()
    {
        OrderLimitRules.SessionCap(PerPerson(1, OverLimitBehavior.Charge), TwoAdults).Should().Be(2);
    }

    [Fact]
    public void SessionCap_ToddlerZero_UsesChildLimit()
    {
        var limits = new OrderLimitRules.Limits(Adult: 4, Child: 2, Senior: 4, Toddler: 0, PerOrder: 0, OverLimitBehavior.Block);
        var heads = new OrderLimitRules.Headcount(Adults: 1, Children: 0, Seniors: 0, Toddlers: 1);

        OrderLimitRules.SessionCap(limits, heads).Should().Be(4 + 2);
    }

    [Fact]
    public void SessionCap_AnyoneUnlimited_MakesTheTableUnlimited()
    {
        var limits = new OrderLimitRules.Limits(Adult: 0, Child: 2, Senior: 0, Toddler: 0, PerOrder: 0, OverLimitBehavior.Block);
        var heads = new OrderLimitRules.Headcount(Adults: 1, Children: 1, Seniors: 0, Toddlers: 0);

        OrderLimitRules.SessionCap(limits, heads).Should().Be(int.MaxValue);
    }

    // ApplyLimits

    private static (bool IsValid, string? Error, List<(int Quantity, decimal Price, ChargeReason Reason)> Lines) Apply(
        int requested,
        OrderLimitRules.Limits? category = null,
        OrderLimitRules.Limits? item = null,
        OrderLimitRules.Usage? used = null,
        bool isAddOn = false,
        decimal price = 0m)
    {
        var result = OrderLimitRules.ApplyLimits(
            itemName: "Test Item",
            categoryName: "Test Category",
            requested: requested,
            category: category ?? Unlimited,
            item: item ?? Unlimited,
            heads: TwoAdults,
            used: used ?? NothingUsed,
            isAddOn: isAddOn,
            price: price);

        return (result.IsValid, result.Error,
                result.Lines.Select(l => (l.Quantity, l.Price, l.Reason)).ToList());
    }

    [Fact]
    public void Unlimited_ReturnsOneFreeLine()
    {
        var result = Apply(requested: 5);

        result.IsValid.Should().BeTrue();
        result.Lines.Count.Should().Be(1);
        result.Lines[0].Quantity.Should().Be(5);
        result.Lines[0].Price.Should().Be(0m);
        result.Lines[0].Reason.Should().Be(ChargeReason.None);
    }

    [Fact]
    public void AddOn_IsChargedAtPrice()
    {
        var result = Apply(requested: 2, isAddOn: true, price: 1.00m);

        result.Lines[0].Price.Should().Be(1.00m);
    }

    [Fact]
    public void ItemPerOrder_OverTheLimit_Blocks()
    {
        var result = Apply(requested: 2, item: PerOrder(1));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("maximum 1 per order");
    }

    [Fact]
    public void ItemPerOrder_CountsWhatIsAlreadyInTheOrder()
    {
        var result = Apply(requested: 1, item: PerOrder(1), used: new OrderLimitRules.Usage(0, OrderItem: 1, 0, 0));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CategoryPerOrder_CombinesItems()
    {
        // 25 sashimi already in this order; 6 more would make 31
        var result = Apply(requested: 6, category: PerOrder(30), used: new OrderLimitRules.Usage(OrderCategory: 25, 0, 0, 0));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Test Category");
    }

    [Fact]
    public void PerPersonBlock_OverTheCap_Blocks()
    {
        // 2 per person x 2 adults = 4; 3 already ordered
        var result = Apply(requested: 2, item: PerPerson(2, OverLimitBehavior.Block),
                           used: new OrderLimitRules.Usage(0, 0, 0, SessionItem: 3));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("limit for the bill is 4");
        result.Error.Should().Contain("limit for the bill is 4");
    }

    [Fact]
    public void PerPersonCharge_SplitsIntoFreeAndChargedLines()
    {
        // 1 per person x 2 adults = 2 free; the 3rd is charged at price
        var result = Apply(requested: 3, category: PerPerson(1, OverLimitBehavior.Charge), price: 5.00m);

        result.IsValid.Should().BeTrue();
        result.Lines.Count.Should().Be(2);
        result.Lines[0].Quantity.Should().Be(2);
        result.Lines[0].Price.Should().Be(0m);
        result.Lines[1].Quantity.Should().Be(1);
        result.Lines[1].Price.Should().Be(5.00m);
        result.Lines[1].Reason.Should().Be(ChargeReason.OverLimit);
    }

    [Fact]
    public void PerPersonCharge_AllUsedUp_ChargesEverything()
    {
        var result = Apply(requested: 2, category: PerPerson(1, OverLimitBehavior.Charge), price: 5.00m,
                           used: new OrderLimitRules.Usage(0, 0, SessionCategory: 2, 0));

        result.Lines.Count.Should().Be(1);
        result.Lines[0].Reason.Should().Be(ChargeReason.OverLimit);
        result.Lines[0].Quantity.Should().Be(2);
    }

    [Fact]
    public void Charge_WithoutPrice_IsRejected()
    {
        var result = Apply(requested: 3, category: PerPerson(1, OverLimitBehavior.Charge), price: 0m);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void TighterLimitBinds_ItemBlockBeatsCategoryCharge()
    {
        // Category: 5 per person (Charge) = 10 left. Item: 1 per person (Block) = 2, with 1 already used = 1 left.
        // The item is tighter, so its Block applies and nothing is charged
        var result = Apply(requested: 2,
                           category: PerPerson(5, OverLimitBehavior.Charge),
                           item: PerPerson(1, OverLimitBehavior.Block),
                           used: new OrderLimitRules.Usage(0, 0, 0, SessionItem: 1),
                           price: 5.00m);

        result.IsValid.Should().BeFalse();
    }

    // What the menu is told is left

    [Fact]
    public void PerOrderCap_ZeroIsNoCap()
    {
        OrderLimitRules.PerOrderCap(Unlimited).Should().BeNull();
        OrderLimitRules.PerOrderCap(PerOrder(3)).Should().Be(3);
    }

    [Fact]
    public void SessionLeftBeforeBlock_IsTheCapMinusWhatsOrdered()
    {
        OrderLimitRules.SessionLeftBeforeBlock(PerPerson(2, OverLimitBehavior.Block), TwoAdults, sessionUsed: 1)
            .Should().Be(3);
    }

    [Fact]
    public void SessionLeftBeforeBlock_NeverGoesBelowZero()
    {
        OrderLimitRules.SessionLeftBeforeBlock(PerPerson(1, OverLimitBehavior.Block), TwoAdults, sessionUsed: 5)
            .Should().Be(0);
    }

    [Fact]
    public void SessionLeftBeforeBlock_ChargeLimitsDontBlock()
    {
        OrderLimitRules.SessionLeftBeforeBlock(PerPerson(1, OverLimitBehavior.Charge), TwoAdults, sessionUsed: 5)
            .Should().BeNull();
    }

    [Fact]
    public void SessionLeftBeforeBlock_UnlimitedIsNull()
    {
        OrderLimitRules.SessionLeftBeforeBlock(Unlimited, TwoAdults, sessionUsed: 0).Should().BeNull();
    }

    [Fact]
    public void SessionLeftBeforeCharge_IsTheFreeUnitsLeft_AndNeverBelowZero()
    {
        OrderLimitRules.SessionLeftBeforeCharge(PerPerson(1, OverLimitBehavior.Charge), TwoAdults, sessionUsed: 1)
            .Should().Be(1);
        OrderLimitRules.SessionLeftBeforeCharge(PerPerson(1, OverLimitBehavior.Charge), TwoAdults, sessionUsed: 5)
            .Should().Be(0);
    }

    [Fact]
    public void SessionLeftBeforeCharge_IsNullForBlockAndUnlimited()
    {
        OrderLimitRules.SessionLeftBeforeCharge(PerPerson(1, OverLimitBehavior.Block), TwoAdults, sessionUsed: 0)
            .Should().BeNull();
        OrderLimitRules.SessionLeftBeforeCharge(Unlimited, TwoAdults, sessionUsed: 0).Should().BeNull();
    }

    [Fact]
    public void ItemCharge_CannotPushPastCategoryBlock()
    {
        // Category Block: 5 per person x 2 = 10, 5 used -> 5 left. Item Charge: 1 per person x 2 = 2, 2 used -> 0 left
        var result = Apply(requested: 6,
                           category: PerPerson(5, OverLimitBehavior.Block),
                           item: PerPerson(1, OverLimitBehavior.Charge),
                           used: new OrderLimitRules.Usage(0, 0, SessionCategory: 5, SessionItem: 2),
                           price: 5.00m);

        result.IsValid.Should().BeFalse();   // the 6th is over the category's Block limit
    }

    [Fact]
    public void PerOrderCap_perGuest_MultipliesByEveryoneOnTheBill()
    {
        var fourTop = new OrderLimitRules.Headcount(Adults: 2, Children: 1, Seniors: 0, Toddlers: 1);

        OrderLimitRules.PerOrderCap(PerGuestPerOrder(30), fourTop).Should().Be(120);   // toddlers count too
        OrderLimitRules.PerOrderCap(PerGuestPerOrder(30), TwoAdults).Should().Be(60);
    }

    [Fact]
    public void PerOrderCap_NotPerGuest_IgnoresHeadcount()
    {
        OrderLimitRules.PerOrderCap(PerOrder(30), TwoAdults).Should().Be(30);
    }

    [Fact]
    public void CategoryPerOrderPerGuest_BlocksPastTheCapTimesHeadcount_AndSaysWhy()
    {
        // 2 adults = 60 in one order; 55 already in it
        var used = new OrderLimitRules.Usage(OrderCategory: 55, 0, 0, 0);

        Apply(requested: 5, category: PerGuestPerOrder(30), used: used).IsValid.Should().BeTrue();

        var over = Apply(requested: 6, category: PerGuestPerOrder(30), used: used);
        over.IsValid.Should().BeFalse();
        over.Error.Should().Contain("maximum 60 combined per order (30 per guest x 2 guests)");
    }

    private static readonly OrderLimitRules.Limits TwoEach =
        new(Adult: 2, Child: 2, Senior: 2, Toddler: 0, PerOrder: 0, OverLimitBehavior.Block);

    private static readonly OrderLimitRules.Headcount OneAdult = new(1, 0, 0, 0);
    private static readonly OrderLimitRules.Headcount ThreeAdults = new(3, 0, 0, 0);

    [Fact]
    public void EffectSessionUsed_GuestsMovedWithoutOrders_TableLeftWins()
    {
        // Bill B has 1 adult who was moved off bill A without their orders.
        // The table (2 adults, cap 4) already ate 4 on bill A
        var billHeads = OneAdult;
        var tableHeads = TwoAdults;
        const int billUsed = 0;
        const int tableUsedFree = 4;

        var used = OrderLimitRules.EffectSessionUsed(TwoEach, billHeads, billUsed, tableHeads, tableUsedFree);
        var left = OrderLimitRules.SessionLeftBeforeBlock(TwoEach, billHeads, used);

        // No new allowance
        left.Should().Be(0);
    }

    [Fact]
    public void EffectSessionUsed_OtherBillWithinItsCap_BillKeepsFullAllowance()
    {
        // This bill has 2 adults and nothing ordered. The table has 3 adults (cap 6),
        // the other bill's 1 adult ate their 2
        var billHeads = TwoAdults;
        var tableHeads = ThreeAdults;
        const int billUsed = 0;
        const int tableUsedFree = 2;

        var used = OrderLimitRules.EffectSessionUsed(TwoEach, billHeads, billUsed, tableHeads, tableUsedFree);
        var left = OrderLimitRules.SessionLeftBeforeBlock(TwoEach, billHeads, used);

        // 2 per adult x 2 adults
        left.Should().Be(4);
    }

    [Fact]
    public void EffectSessionUsed_ChargedLinesLeftOut_DontUseUpOtherBills()
    {
        // Hot Stone, 1 each then charged. Bill A (1 adult) had 1 included and 2 charged,
        // so only 1 counts at the table. Bill B (1 adult) has nothing ordered
        var hotStone = new OrderLimitRules.Limits(Adult: 1, Child: 1, Senior: 1, Toddler: 0, PerOrder: 0, OverLimitBehavior.Charge);
        var billHeads = OneAdult;
        var tableHeads = TwoAdults;
        const int billUsed = 0;
        const int tableUsedFree = 1;

        var used = OrderLimitRules.EffectSessionUsed(hotStone, billHeads, billUsed, tableHeads, tableUsedFree);
        var freeLeft = OrderLimitRules.SessionLeftBeforeCharge(hotStone, billHeads, used);

        // Bill B still gets its 1 included
        freeLeft.Should().Be(1);
    }

    [Fact]
    public void EffectSessionUsed_Unlimited_ReturnsBillUsage()
    {
        // No limit, so the table's usage doesn't matter
        var billHeads = OneAdult;
        var tableHeads = TwoAdults;
        const int billUsed = 3;
        const int tableUsedFree = 50;

        var used = OrderLimitRules.EffectSessionUsed(Unlimited, billHeads, billUsed, tableHeads, tableUsedFree);

        used.Should().Be(billUsed);
    }
}
