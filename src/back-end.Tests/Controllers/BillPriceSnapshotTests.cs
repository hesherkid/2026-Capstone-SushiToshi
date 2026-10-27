using System.Security.Claims;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using back_end.controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.bill;
using back_end.Services;

namespace back_end.Tests.Controllers
{
    // Bill 1: 4 adults; bill 2: 2 adults, so the table is 6 and gets the 18% gratuity
    public class BillPriceSnapshotTests : IDisposable
    {
        private const int SessionId = 800;
        private const int LocationId = 1;
        private readonly ApplicationDbContext _context;
        private PricingInfo _current = Weekday(adult: 40m);

        public BillPriceSnapshotTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new ApplicationDbContext(options);

            _context.Tables.Add(new TableEntity { Table_Id = 8, table_number = 8, Location_Id = LocationId, is_active = true });
            _context.DiningSessions.Add(new DiningSession
            {
                Session_Id = SessionId, Menu_Id = 1, Location_Id = LocationId, Table_Id = 8, Started_At = DateTime.UtcNow,
            });
            _context.Bills.AddRange(
                new Billing { Bill_Id = 1, Session_Id = SessionId, Bill_Name = "Lee", Adult_Count = 4, Total_Count = 4 },
                new Billing { Bill_Id = 2, Session_Id = SessionId, Bill_Name = "Park", Adult_Count = 2, Total_Count = 2 });
            // Staff only see sessions at the location they're working at
            _context.Users.Add(new User { User_id = 30, Email = "staff@example.com", Role = UserRoles.Staff, Location_id = LocationId });
            _context.SaveChanges();
        }

        public void Dispose() => _context.Dispose();

        private static PricingInfo Weekday(decimal adult) => new()
        {
            PricingType = "Weekday",
            PricingSource = "Default",
            AdultBasePrice = adult,
            TaxRate = 0.05m,
            GratuityRate = 0.18m,
            GratuityMinPartySize = 6,
        };

        private BillController Bills()
        {
            var pricing = new Mock<IPricingService>();
            pricing.Setup(p => p.GetPricingAsync(LocationId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(() => _current);

            return new BillController(_context, new Mock<IConfiguration>().Object, new Mock<ILogger<BillController>>().Object, pricing.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, "30"),
                            new Claim(ClaimTypes.Role, "Staff"),
                        }, "TestAuth")),
                    },
                },
            };
        }

        private async Task<BillSummaryResponse> Summary(int billId = 1)
        {
            _context.ChangeTracker.Clear();
            var result = await Bills().GetBillSummary(SessionId, billId);
            return (BillSummaryResponse)((OkObjectResult)result).Value!;
        }

        private async Task Close(int billId = 1)
        {
            (await Bills().Close_Bill(SessionId, billId)).Should().BeOfType<OkObjectResult>();
            _context.ChangeTracker.Clear();
        }

        [Fact]
        public async Task OpenBill_FollowsCurrentPrices()
        {
            (await Summary()).Base_Charges_Subtotal.Should().Be(160m);   // 4 × 40

            _current = Weekday(adult: 45m);

            var summary = await Summary();
            summary.Base_Charges_Subtotal.Should().Be(180m);
            summary.Prices_Locked.Should().BeFalse();
        }

        [Fact]
        public async Task ClosedBill_KeepsThePricesItClosedWith()
        {
            await Close();
            _current = Weekday(adult: 45m);

            var summary = await Summary();

            summary.Prices_Locked.Should().BeTrue();
            summary.Adult_Base_Price.Should().Be(40m);
            summary.Base_Charges_Subtotal.Should().Be(160m);
            summary.Total_Amount.Should().Be(198.24m);   // 160 + 28.80 gratuity + 9.44 GST
        }

        [Fact]
        public async Task Close_SavesTheSnapshotOnTheBill()
        {
            await Close();

            var bill = await _context.Bills.SingleAsync(b => b.Bill_Id == 1);
            bill.Pricing_Type.Should().Be(PricingDayType.Weekday);
            bill.Adult_Price.Should().Be(40m);
            bill.Tax_Rate.Should().Be(0.05m);
            bill.Gratuity_Rate.Should().Be(0.18m);   // the table had 6 guests
        }

        [Fact]
        public async Task ClosedBill_KeepsItsGratuityWhenThePartyShrinks()
        {
            await Close();

            // Another bill at the table is cancelled afterwards: the table is now 4, under the minimum
            var other = await _context.Bills.SingleAsync(b => b.Bill_Id == 2);
            other.Status = BillStatus.Cancelled;
            await _context.SaveChangesAsync();

            var summary = await Summary();
            summary.Party_Size.Should().Be(4);
            summary.Gratuity_Rate.Should().Be(0.18m);
            summary.Gratuity_Amount.Should().Be(28.80m);
        }

        [Fact]
        public async Task SmallTableClosed_KeepsZeroGratuityWhenThePartyGrows()
        {
            var other = await _context.Bills.SingleAsync(b => b.Bill_Id == 2);
            other.Status = BillStatus.Cancelled;
            await _context.SaveChangesAsync();
            await Close();   // table of 4: no gratuity

            other = await _context.Bills.SingleAsync(b => b.Bill_Id == 2);
            other.Status = BillStatus.Open;   // e.g. cancelled by mistake and restored
            await _context.SaveChangesAsync();

            (await Summary()).Gratuity_Amount.Should().Be(0m);
        }

        [Fact]
        public async Task BillClosedBeforeSnapshots_StillUsesCurrentPrices()
        {
            var bill = await _context.Bills.SingleAsync(b => b.Bill_Id == 1);
            bill.Status = BillStatus.Closed;   // closed by the old code: no Pricing_Type
            bill.Closed_At = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _current = Weekday(adult: 45m);

            var summary = await Summary();
            summary.Prices_Locked.Should().BeFalse();
            summary.Base_Charges_Subtotal.Should().Be(180m);
        }

        [Fact]
        public async Task HolidayName_IsShownAndKeptAfterClosing()
        {
            _current = Weekday(adult: 42.95m);
            _current.PricingType = "Holiday";
            _current.HolidayName = "Canada Day";

            (await Summary()).Holiday_Name.Should().Be("Canada Day");

            await Close();
            _current = Weekday(adult: 40m);   // e.g. the holiday was rejected later

            var summary = await Summary();
            summary.Pricing_Type.Should().Be("Holiday");
            summary.Holiday_Name.Should().Be("Canada Day");
            summary.Adult_Base_Price.Should().Be(42.95m);
        }

        [Fact]
        public async Task Close_CancelledBill_Returns400AndFreezesNothing()
        {
            var bill = await _context.Bills.SingleAsync(b => b.Bill_Id == 1);
            bill.Status = BillStatus.Cancelled;
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            var result = await Bills().Close_Bill(SessionId, 1);

            result.Should().BeOfType<BadRequestObjectResult>();
            _context.ChangeTracker.Clear();
            (await _context.Bills.SingleAsync(b => b.Bill_Id == 1)).Pricing_Type.Should().BeNull();
        }
    }
}
