using System.Security.Claims;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using back_end.Controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Dtos;
using back_end.Helpers;
using back_end.Services;

namespace back_end.Tests.Controllers
{
    public class LeftoverFeeTests : IDisposable
    {
        private const int SessionId = 700;
        private const int Lee = 1;   // 2 guests
        private const int Park = 2;  // 1 guest
        private const int Kim = 3;   // 1 guest
        private readonly ApplicationDbContext _context;

        public LeftoverFeeTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new ApplicationDbContext(options);

            _context.Tables.Add(new TableEntity { Table_Id = 6, table_number = 6, seat_count = 8, Location_Id = 1, is_active = true });
            _context.DiningSessions.Add(new DiningSession { Session_Id = SessionId, Menu_Id = 1, Location_Id = 1, Table_Id = 6, Started_At = DateTime.UtcNow });
            _context.Bills.AddRange(
                new Billing { Bill_Id = Lee, Session_Id = SessionId, Bill_Name = "Lee", Adult_Count = 2, Total_Count = 2 },
                new Billing { Bill_Id = Park, Session_Id = SessionId, Bill_Name = "Park", Adult_Count = 1, Total_Count = 1 },
                new Billing { Bill_Id = Kim, Session_Id = SessionId, Bill_Name = "Kim", Adult_Count = 1, Total_Count = 1 });
            // Staff only see sessions at the location they're working at
            _context.Users.Add(new User { User_id = 30, Email = "staff@example.com", Role = UserRoles.Staff, Location_id = 1 });
            _context.SaveChanges();
        }

        public void Dispose() => _context.Dispose();

        private static IPricingService Pricing()
        {
            var pricing = new Mock<IPricingService>();
            var info = new PricingInfo { PricingType = "Test", TaxRate = 0.05m, LeftoverFee = 10.00m };
            pricing.Setup(p => p.GetPricingAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(info);
            pricing.Setup(p => p.GetCurrentPricingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(info);
            return pricing.Object;
        }

        // Signed in as staff member 30
        private static ControllerContext AsStaff() => new()
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "30"),
                    new Claim(ClaimTypes.Role, "Staff"),
                }, "TestAuth")),
            },
        };

        private BillController Bills() =>
            new(_context, new Mock<IConfiguration>().Object, new Mock<ILogger<BillController>>().Object, Pricing())
            {
                ControllerContext = AsStaff(),
            };

        private LeftoverFeeController Fees() =>
            new(_context, new Mock<ILogger<LeftoverFeeController>>().Object, Pricing())
            {
                ControllerContext = AsStaff(),
            };

        private async Task<LeftoverFeeResponse> Apply(decimal? amount)
        {
            var result = await Fees().ApplyLeftoverFee(SessionId, new LeftoverFeeRequest { Amount = amount });
            _context.ChangeTracker.Clear();
            return ((result as OkObjectResult)!.Value as LeftoverFeeResponse)!;
        }

        private decimal ChargeOn(int billId) => _context.Bills.Single(b => b.Bill_Id == billId).Leftover_Charge;

        private void Close(int billId, decimal charge)
        {
            var bill = _context.Bills.Single(b => b.Bill_Id == billId);
            bill.Status = BillStatus.Closed;
            bill.Leftover_Charge = charge;
            _context.SaveChanges();
            _context.ChangeTracker.Clear();
        }

        [Fact]
        public void SplitByGuests_SharesFollowGuestCounts()
        {
            // A party of 4 and a party of 1
            var guests = new[] { 4, 1 };

            var shares = LeftoverFee.SplitByGuests(20.00m, guests);

            // $4.00 a guest on both bills
            shares.Should().Equal(16.00m, 4.00m);
        }

        [Fact]
        public void SplitByGuests_TiedRemainders_OldestBillFirst()
        {
            // Equal parties, so every remainder is the same
            var guests = new[] { 1, 1, 1 };

            var shares = LeftoverFee.SplitByGuests(10.00m, guests);

            shares.Should().Equal(3.34m, 3.33m, 3.33m);
        }

        [Theory]
        [InlineData(10.00, new[] { 2, 1, 1 })]
        [InlineData(10.01, new[] { 3, 1, 1 })]
        [InlineData(7.77, new[] { 5, 2, 1, 1 })]
        [InlineData(0.05, new[] { 1, 1, 1, 1, 1, 1 })]
        public void SplitByGuests_AlwaysAddsUpToTheFee(double fee, int[] guests)
        {
            var amount = (decimal)fee;

            var shares = LeftoverFee.SplitByGuests(amount, guests);

            shares.Sum().Should().Be(amount);
            shares.Should().OnlyContain(s => s >= 0m);
        }

        [Fact]
        public void SplitByGuests_ZeroFee_IsZeroForEveryBill()
        {
            var shares = LeftoverFee.SplitByGuests(0m, new[] { 2, 1 });

            shares.Should().Equal(0m, 0m);
        }

        [Fact]
        public void SplitByGuests_NoBills_IsEmpty()
        {
            var shares = LeftoverFee.SplitByGuests(10.00m, Array.Empty<int>());

            shares.Should().BeEmpty();
        }

        [Fact]
        public async Task NoAmount_UsesTheDefault_SharedByGuests()
        {
            var response = await Apply(null);

            // $10.00 over 2, 1 and 1 guest count bills
            response.Amount.Should().Be(10.00m);
            response.DefaultFee.Should().Be(10.00m);
            ChargeOn(Lee).Should().Be(5.00m);
            ChargeOn(Park).Should().Be(2.50m);
            ChargeOn(Kim).Should().Be(2.50m);
        }

        [Fact]
        public async Task Response_IncludesEachBillsGuestCount()
        {
            var response = await Apply(10.00m);

            response.Bills.Select(b => b.GuestCount).Should().Equal(2, 1, 1);
        }

        [Fact]
        public async Task EnteredAmount_OverridesTheDefault()
        {
            var response = await Apply(20.00m);

            response.Amount.Should().Be(20.00m);
            ChargeOn(Lee).Should().Be(10.00m);
        }

        [Fact]
        public async Task ApplyingAgain_ReplacesTheFee_AndZeroRemovesIt()
        {
            await Apply(15.00m);

            var response = await Apply(0m);

            response.Amount.Should().Be(0m);
            ChargeOn(Lee).Should().Be(0m);
        }

        [Fact]
        public async Task ClosedBills_KeepWhatTheyHad()
        {
            Close(Kim, 2.00m);

            // $10.00 over the open bills' 2 and 1 guests
            var response = await Apply(10.00m);

            response.Bills.Should().HaveCount(2);
            ChargeOn(Lee).Should().Be(6.67m);
            ChargeOn(Park).Should().Be(3.33m);
            ChargeOn(Kim).Should().Be(2.00m);
        }

        [Fact]
        public async Task NegativeAmount_IsRejected()
        {
            var result = await Fees().ApplyLeftoverFee(SessionId, new LeftoverFeeRequest { Amount = -1m });

            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task NoOpenBills_IsRejected()
        {
            foreach (var bill in _context.Bills) bill.Status = BillStatus.Closed;
            _context.SaveChanges();
            _context.ChangeTracker.Clear();

            var result = await Fees().ApplyLeftoverFee(SessionId, new LeftoverFeeRequest { Amount = 10m });

            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task StaffEditGuests_SharesTheFeeAgain()
        {
            // $10.00 over 2, 1 and 1 guests
            await Apply(10.00m);

            // Park goes from 1 guest to 3
            await Bills().UpdateGuestCounts(SessionId, Park, new UpdateGuestCountsDto { AdultCount = 3 });
            _context.ChangeTracker.Clear();

            // $10.00 over 2, 3 and 1 guests, still adding up exactly
            ChargeOn(Lee).Should().Be(3.33m);
            ChargeOn(Park).Should().Be(5.00m);
            ChargeOn(Kim).Should().Be(1.67m);
        }

        [Fact]
        public async Task StaffEditGuests_NoFee_StaysAtZero()
        {
            await Bills().UpdateGuestCounts(SessionId, Park, new UpdateGuestCountsDto { AdultCount = 3 });
            _context.ChangeTracker.Clear();

            ChargeOn(Lee).Should().Be(0m);
            ChargeOn(Park).Should().Be(0m);
        }

        [Fact]
        public async Task StaffEditGuests_LeavesClosedBillsAlone()
        {
            await Apply(10.00m);
            Close(Kim, 2.50m);

            // The open bills hold $7.50, now over 2 and 3 guests
            await Bills().UpdateGuestCounts(SessionId, Park, new UpdateGuestCountsDto { AdultCount = 3 });
            _context.ChangeTracker.Clear();

            ChargeOn(Lee).Should().Be(3.00m);
            ChargeOn(Park).Should().Be(4.50m);
            ChargeOn(Kim).Should().Be(2.50m);
        }

        [Fact]
        public async Task BillSummary_IncludesTheShare_AndTaxesIt()
        {
            await Apply(20.00m);

            var summary = ((await Bills().GetBillSummary(SessionId, Lee)) as OkObjectResult)!.Value as BillSummaryResponse;

            summary!.LeftoverCharge.Should().Be(10.00m);
            summary.Subtotal.Should().Be(10.00m);      // base prices are 0 in this test
            summary.TaxAmount.Should().Be(0.50m);      // 5% of the fee
            summary.TotalAmount.Should().Be(10.50m);
        }
    }
}