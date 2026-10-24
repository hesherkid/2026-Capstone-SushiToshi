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
using back_end.Helpers;
using back_end.Services;

namespace back_end.Tests.Controllers
{
    public class LeftoverFeeTests : IDisposable
    {
        private const int SessionId = 700;
        private readonly ApplicationDbContext _context;

        public LeftoverFeeTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new ApplicationDbContext(options);

            _context.Tables.Add(new TableEntity { Table_Id = 6, table_number = 6, Location_Id = 1, is_active = true });
            _context.DiningSessions.Add(new DiningSession { Session_Id = SessionId, Menu_Id = 1, Location_Id = 1, Table_Id = 6, Started_At = DateTime.UtcNow });
            _context.Bills.AddRange(
                new Billing { Bill_Id = 1, Session_Id = SessionId, Bill_Name = "Lee", Adult_Count = 2, Total_Count = 2 },
                new Billing { Bill_Id = 2, Session_Id = SessionId, Bill_Name = "Park", Adult_Count = 1, Total_Count = 1 },
                new Billing { Bill_Id = 3, Session_Id = SessionId, Bill_Name = "Kim", Adult_Count = 1, Total_Count = 1 });
            _context.SaveChanges();
        }

        public void Dispose() => _context.Dispose();

        private BillController Bills()
        {
            var pricing = new Mock<IPricingService>();
            var info = new PricingInfo { PricingType = "Test", TaxRate = 0.05m, LeftoverFee = 10.00m };
            pricing.Setup(p => p.GetPricingForDate(It.IsAny<DateTime>())).Returns(info);
            pricing.Setup(p => p.GetCurrentPricing()).Returns(info);

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

        private async Task<LeftoverFeeResponse> Apply(decimal? amount)
        {
            var result = await Bills().ApplyLeftoverFee(SessionId, new LeftoverFeeRequest { Amount = amount });
            _context.ChangeTracker.Clear();
            return ((result as OkObjectResult)!.Value as LeftoverFeeResponse)!;
        }

        private decimal ChargeOn(int billId) => _context.Bills.Single(b => b.Bill_Id == billId).Leftover_Charge;

        // Splitting

        [Fact]
        public void SplitEvenly_SharesAddUpExactly()
        {
            LeftoverFee.SplitEvenly(10.00m, 3).Should().Equal(3.34m, 3.33m, 3.33m);
            LeftoverFee.SplitEvenly(15.00m, 2).Should().Equal(7.50m, 7.50m);
            LeftoverFee.SplitEvenly(0m, 2).Should().Equal(0m, 0m);
        }

        // Applying

        [Fact]
        public async Task NoAmount_UsesTheDefault_SplitAcrossOpenBills()
        {
            var response = await Apply(null);

            response.Amount.Should().Be(10.00m);
            response.Default_Fee.Should().Be(10.00m);
            ChargeOn(1).Should().Be(3.34m);
            ChargeOn(2).Should().Be(3.33m);
            ChargeOn(3).Should().Be(3.33m);
        }

        [Fact]
        public async Task StaffAmount_OverridesTheDefault()
        {
            (await Apply(15.00m)).Amount.Should().Be(15.00m);
            ChargeOn(1).Should().Be(5.00m);
        }

        [Fact]
        public async Task ApplyingAgain_ReplacesTheFee_AndZeroRemovesIt()
        {
            await Apply(15.00m);
            (await Apply(0m)).Amount.Should().Be(0m);
            ChargeOn(1).Should().Be(0m);
        }

        [Fact]
        public async Task ClosedBills_KeepWhatTheyHad()
        {
            var closed = _context.Bills.Single(b => b.Bill_Id == 3);
            closed.Status = BillStatus.Closed;
            closed.Leftover_Charge = 2.00m;
            _context.SaveChanges();
            _context.ChangeTracker.Clear();

            var response = await Apply(10.00m);

            response.Bills.Should().HaveCount(2);
            ChargeOn(1).Should().Be(5.00m);
            ChargeOn(2).Should().Be(5.00m);
            ChargeOn(3).Should().Be(2.00m);
        }

        [Fact]
        public async Task NegativeAmount_IsRejected()
        {
            (await Bills().ApplyLeftoverFee(SessionId, new LeftoverFeeRequest { Amount = -1m }))
                .Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task NoOpenBills_IsRejected()
        {
            foreach (var bill in _context.Bills) bill.Status = BillStatus.Closed;
            _context.SaveChanges();
            _context.ChangeTracker.Clear();

            (await Bills().ApplyLeftoverFee(SessionId, new LeftoverFeeRequest { Amount = 10m }))
                .Should().BeOfType<BadRequestObjectResult>();
        }

        // On the bill

        [Fact]
        public async Task BillSummary_IncludesTheShare_AndTaxesIt()
        {
            await Apply(15.00m);

            var summary = ((await Bills().GetBillSummary(SessionId, 1)) as OkObjectResult)!.Value as BillSummaryResponse;

            summary!.Leftover_Charge.Should().Be(5.00m);
            summary.Subtotal.Should().Be(5.00m);       // base prices are 0 in this test
            summary.Tax_Amount.Should().Be(0.25m);     // 5% of the fee
            summary.Total_Amount.Should().Be(5.25m);
        }
    }
}