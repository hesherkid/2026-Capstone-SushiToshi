using System.Reflection;
using System.Security.Claims;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using back_end.Configurations;
using back_end.Controllers;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.HolidayDTOs;
using back_end.Services.Holidays;

namespace back_end.Tests.Controllers
{
    public class HolidayControllerTests
    {
        private const int AdminId = 1;
        private static readonly DateOnly BoxingDay = new(2026, 12, 26);
        private readonly Mock<IHolidayService> _service = new();

        private HolidayController Holidays(bool signedIn = true) => new(
            _service.Object,
            Options.Create(new HolidayApiSettings
            {
                BaseUrl = "https://example.test/",
                DefaultProvince = "AB",
                IncludeOptional = false,
            }),
            new Mock<ILogger<HolidayController>>().Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = signedIn
                        ? new ClaimsPrincipal(new ClaimsIdentity(new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, AdminId.ToString()),
                            new Claim(ClaimTypes.Role, "Admin"),
                        }, "TestAuth"))
                        : new ClaimsPrincipal(new ClaimsIdentity()),
                },
            },
        };

        private static Holiday Holiday(int id, HolidayStatus status = HolidayStatus.Pending) => new()
        {
            Holiday_Id = id,
            Province_Code = "AB",
            Date = BoxingDay,
            Name = "Boxing Day",
            Status = status,
        };

        // The { message } the admin page shows
        private static string? Message(IActionResult result)
        {
            var value = (result as ObjectResult)?.Value;
            return value?.GetType().GetProperty("message")?.GetValue(value) as string;
        }

        private static int? StatusCode(IActionResult result) => (result as IStatusCodeActionResult)?.StatusCode;

        // ---------- Options and listing ----------

        [Fact]
        public void GetOptions_ReturnsProvincesYearsAndDefaults()
        {
            _service.Setup(s => s.GetAllowedYears()).Returns(new HolidayYearRange(2025, 2028));

            var options = (HolidayOptionsResponse)((OkObjectResult)Holidays().GetOptions()).Value!;

            options.Provinces.Should().HaveCount(13).And.Contain("AB");
            options.Default_Province.Should().Be("AB");
            (options.Year_From, options.Year_To).Should().Be((2025, 2028));
            options.Include_Optional.Should().BeFalse();
        }

        [Fact]
        public async Task List_WithoutProvince_UsesTheDefaultProvince()
        {
            _service.Setup(s => s.ListAsync("AB", 2026, HolidayStatus.Pending, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<Holiday> { Holiday(1) });

            var result = await Holidays().List(province: null, year: 2026, status: HolidayStatus.Pending);

            ((IEnumerable<HolidayResponse>)((OkObjectResult)result).Value!)
                .Should().ContainSingle().Which.Name.Should().Be("Boxing Day");
        }

        // ---------- Load button ----------

        [Fact]
        public async Task Import_ReturnsAddedAndSkippedCounts()
        {
            _service.Setup(s => s.ImportAsync("AB", 2027, null, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new HolidayImportResult(2, 7, new List<Holiday> { Holiday(1), Holiday(2) }));

            var result = await Holidays().Import(new HolidayImportRequest { Province_Code = "AB", Year = 2027 });

            var body = (HolidayImportResponse)((OkObjectResult)result).Value!;
            body.Added.Should().Be(2);
            body.Skipped.Should().Be(7);
            body.Holidays.Should().HaveCount(2);
        }

        [Fact]
        public async Task Import_HolidayServiceDown_Returns502WithMessage()
        {
            _service.Setup(s => s.ImportAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new HolidayProviderException("The holiday service didn't respond in time."));

            var result = await Holidays().Import(new HolidayImportRequest { Province_Code = "AB", Year = 2027 });

            StatusCode(result).Should().Be(StatusCodes.Status502BadGateway);
            Message(result).Should().Be("The holiday service didn't respond in time.");
        }

        [Fact]
        public async Task Import_YearNotAllowed_Returns400WithoutParameterSuffix()
        {
            _service.Setup(s => s.ImportAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new ArgumentException("Holidays can only be loaded for 2025 to 2028.", "year"));

            var result = await Holidays().Import(new HolidayImportRequest { Province_Code = "AB", Year = 2030 });

            result.Should().BeOfType<BadRequestObjectResult>();
            Message(result).Should().Be("Holidays can only be loaded for 2025 to 2028.");
        }

        [Fact]
        public async Task Import_SameDateSavedAtTheSameTime_Returns409()
        {
            _service.Setup(s => s.ImportAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new DbUpdateException("Duplicate entry"));

            var result = await Holidays().Import(new HolidayImportRequest { Province_Code = "AB", Year = 2027 });

            result.Should().BeOfType<ConflictObjectResult>();
        }

        [Fact]
        public async Task Import_UnexpectedError_Returns500()
        {
            _service.Setup(s => s.ImportAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new Exception("boom"));

            var result = await Holidays().Import(new HolidayImportRequest { Province_Code = "AB", Year = 2027 });

            StatusCode(result).Should().Be(500);
            Message(result).Should().Be("Error loading holidays");   // no internal details
        }

        // ---------- Approve / reject ----------

        [Fact]
        public async Task Approve_RecordsTheAdminAsReviewer()
        {
            _service.Setup(s => s.SetStatusAsync(
                        It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 1, 2 })),
                        HolidayStatus.Approved, AdminId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(2);

            var result = await Holidays().Approve(new HolidayReviewRequest { Ids = new() { 1, 2 } });

            ((HolidayReviewResponse)((OkObjectResult)result).Value!).Updated.Should().Be(2);
        }

        [Fact]
        public async Task Reject_SetsRejectedStatus()
        {
            await Holidays().Reject(new HolidayReviewRequest { Ids = new() { 3 } });

            _service.Verify(s => s.SetStatusAsync(
                It.IsAny<IReadOnlyCollection<int>>(), HolidayStatus.Rejected, AdminId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Approve_WithoutUserId_Returns401AndChangesNothing()
        {
            var result = await Holidays(signedIn: false).Approve(new HolidayReviewRequest { Ids = new() { 1 } });

            result.Should().BeOfType<UnauthorizedResult>();
            _service.Verify(s => s.SetStatusAsync(
                It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<HolidayStatus>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ---------- Manual add, edit, delete ----------

        [Fact]
        public async Task Create_Returns201AndApprovesAsTheAdmin()
        {
            _service.Setup(s => s.AddManualAsync("AB", BoxingDay, "Boxing Day", AdminId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Holiday(5, HolidayStatus.Approved));

            var result = await Holidays().Create(new HolidayCreateRequest
            {
                Province_Code = "AB", Date = BoxingDay, Name = "Boxing Day",
            });

            StatusCode(result).Should().Be(StatusCodes.Status201Created);
            ((HolidayResponse)((ObjectResult)result).Value!).Status.Should().Be(HolidayStatus.Approved);
        }

        [Fact]
        public async Task Create_DateAlreadyTaken_Returns409WithMessage()
        {
            _service.Setup(s => s.AddManualAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new HolidayConflictException("AB already has a holiday on 2026-12-26."));

            var result = await Holidays().Create(new HolidayCreateRequest
            {
                Province_Code = "AB", Date = BoxingDay, Name = "Boxing Day",
            });

            result.Should().BeOfType<ConflictObjectResult>();
            Message(result).Should().Be("AB already has a holiday on 2026-12-26.");
        }

        [Fact]
        public async Task Update_MissingHoliday_Returns404()
        {
            _service.Setup(s => s.UpdateAsync(99, It.IsAny<DateOnly>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Holiday?)null);

            var result = await Holidays().Update(99, new HolidayUpdateRequest { Date = BoxingDay, Name = "Boxing Day" });

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Theory]
        [InlineData(true, typeof(NoContentResult))]
        [InlineData(false, typeof(NotFoundObjectResult))]
        public async Task Delete_Returns204OrNotFound(bool deleted, Type expected)
        {
            _service.Setup(s => s.DeleteAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(deleted);

            (await Holidays().Delete(7)).Should().BeOfType(expected);
        }

        // ---------- Authorization ----------

        [Fact]
        public void Controller_IsAdminOnly()
        {
            typeof(HolidayController).GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be("adminOnly");
        }
    }
}
