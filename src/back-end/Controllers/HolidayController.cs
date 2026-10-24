using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using back_end.Configurations;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.HolidayDTOs;
using back_end.Helpers;
using back_end.Services.Holidays;

namespace back_end.Controllers
{
    [ApiController]
    [Route("api/holidays")]
    [Authorize(Policy = "adminOnly")]
    public class HolidayController : ControllerBase
    {
        private readonly IHolidayService _holidays;
        private readonly HolidayApiSettings _settings;
        private readonly ILogger<HolidayController> _logger;

        public HolidayController(IHolidayService holidays, IOptions<HolidayApiSettings> settings, ILogger<HolidayController> logger)
        {
            _holidays = holidays;
            _settings = settings.Value;
            _logger = logger;
        }

        /// <summary>
        /// Provinces, loadable years and defaults for the Holidays page.
        /// </summary>
        [HttpGet("options")]
        [ProducesResponseType(typeof(HolidayOptionsResponse), StatusCodes.Status200OK)]
        public IActionResult GetOptions()
        {
            var years = _holidays.GetAllowedYears();
            return Ok(new HolidayOptionsResponse
            {
                Provinces = Provinces.Codes,
                Default_Province = _settings.DefaultProvince,
                Year_From = years.From,
                Year_To = years.To,
                Include_Optional = _settings.IncludeOptional,
            });
        }

        /// <summary>
        /// Holidays for a province, optionally one year and one status.
        /// </summary>
        /// <remarks>
        ///     GET /api/holidays?province=AB&amp;year=2026&amp;status=Pending
        /// </remarks>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<HolidayResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public Task<IActionResult> List([FromQuery] string? province, [FromQuery] int? year, [FromQuery] HolidayStatus? status) =>
          Run("listing holidays", async () =>
          {
              var holidays = await _holidays.ListAsync(province ?? _settings.DefaultProvince, year, status);
              return Ok(holidays.Select(ToResponse));
          });

        /// <summary>
        /// Load button: fetches a province's holidays for a year and stores new dates as Pending.
        /// </summary>
        /// <remarks>
        ///     POST /api/holidays/import
        ///     { "province_code": "AB", "year": 2027, "include_optional": false }
        ///
        /// Dates already stored are skipped whatever their status, so loading again is safe.
        /// Nothing affects prices until it is approved.
        /// </remarks>
        [HttpPost("import")]
        [ProducesResponseType(typeof(HolidayImportResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public Task<IActionResult> Import(HolidayImportRequest request) =>
          Run("loading holidays", async () =>
          {
              var result = await _holidays.ImportAsync(request.Province_Code, request.Year, request.Include_Optional);
              return Ok(new HolidayImportResponse
              {
                  Added = result.Added,
                  Skipped = result.Skipped,
                  Holidays = result.AddedHolidays.Select(ToResponse).ToList(),
              });
          });

        /// <summary>
        /// Approves holidays, so holiday pricing applies on those dates.
        /// </summary>
        /// <remarks>
        ///     POST /api/holidays/approve
        ///     { "ids": [12, 13, 14] }
        /// </remarks>
        [HttpPost("approve")]
        [ProducesResponseType(typeof(HolidayReviewResponse), StatusCodes.Status200OK)]
        public Task<IActionResult> Approve(HolidayReviewRequest request) => Review(request, HolidayStatus.Approved);

        /// <summary>
        /// Rejects holidays. They stay listed (so loading again won't bring them back) but don't affect prices.
        /// </summary>
        [HttpPost("reject")]
        [ProducesResponseType(typeof(HolidayReviewResponse), StatusCodes.Status200OK)]
        public Task<IActionResult> Reject(HolidayReviewRequest request) => Review(request, HolidayStatus.Rejected);

        /// <summary>
        /// Adds a holiday by hand (e.g. Boxing Day). It is approved straight away.
        /// </summary>
        /// <remarks>
        ///     POST /api/holidays
        ///     { "province_code": "AB", "date": "2026-12-26", "name": "Boxing Day" }
        /// </remarks>
        [HttpPost]
        [ProducesResponseType(typeof(HolidayResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public Task<IActionResult> Create(HolidayCreateRequest request) =>
          Run("adding a holiday", async () =>
          {
              if (!int.TryParse(ClaimsHelpers.GetUserId(User), out var userId))
              {
                  return Unauthorized();
              }

              var holiday = await _holidays.AddManualAsync(request.Province_Code, request.Date!.Value, request.Name, userId);
              return StatusCode(StatusCodes.Status201Created, ToResponse(holiday));
          });

        /// <summary>
        /// Changes a holiday's date or name. Its status stays the same.
        /// </summary>
        [HttpPut("{holiday_id}")]
        [ProducesResponseType(typeof(HolidayResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public Task<IActionResult> Update(int holiday_id, HolidayUpdateRequest request) =>
          Run("updating a holiday", async () =>
          {
              var holiday = await _holidays.UpdateAsync(holiday_id, request.Date!.Value, request.Name);
              return holiday == null
            ? NotFound(new { message = "Holiday was not found" })
            : Ok(ToResponse(holiday));
          });

        /// <summary>
        /// Deletes a holiday. Unlike rejecting, loading the year again would bring it back as Pending.
        /// </summary>
        [HttpDelete("{holiday_id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<IActionResult> Delete(int holiday_id) =>
          Run("deleting a holiday", async () =>
            await _holidays.DeleteAsync(holiday_id)
              ? NoContent()
              : NotFound(new { message = "Holiday was not found" }));

        private Task<IActionResult> Review(HolidayReviewRequest request, HolidayStatus status) =>
          Run("reviewing holidays", async () =>
          {
              if (!int.TryParse(ClaimsHelpers.GetUserId(User), out var userId))
              {
                  return Unauthorized();
              }

              var updated = await _holidays.SetStatusAsync(request.Ids, status, userId);
              return Ok(new HolidayReviewResponse { Updated = updated });
          });

        // Turns the holiday service's errors into responses the admin page can show
        private async Task<IActionResult> Run(string action, Func<Task<IActionResult>> work)
        {
            try
            {
                return await work();
            }
            catch (ArgumentException ex)
            {
                // Drop the " (Parameter 'code')" suffix .NET adds; the page shows this message
                var message = ex.ParamName == null ? ex.Message : ex.Message.Replace($" (Parameter '{ex.ParamName}')", "");
                return BadRequest(new { message });
            }
            catch (HolidayConflictException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (HolidayProviderException ex)
            {
                _logger.LogWarning(ex, "Holiday service failed while {Action}", action);
                return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                // Two admins loading or adding the same date at once hits the unique index
                _logger.LogWarning(ex, "Conflict while {Action}", action);
                return Conflict(new { message = "These holidays were changed at the same time. Reload and try again." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error {Action}", action);
                return StatusCode(500, new { message = $"Error {action}" });
            }
        }

        private static HolidayResponse ToResponse(Holiday holiday) => new()
        {
            Holiday_Id = holiday.Holiday_Id,
            Province_Code = holiday.Province_Code,
            Date = holiday.Date,
            Observed_Date = holiday.Observed_Date,
            Name = holiday.Name,
            Source = holiday.Source,
            Status = holiday.Status,
            Imported_At = holiday.Imported_At,
            Reviewed_By = holiday.Reviewed_By,
            Reviewed_At = holiday.Reviewed_At,
        };
    }
}
