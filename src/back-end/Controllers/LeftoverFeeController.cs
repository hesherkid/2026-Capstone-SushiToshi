using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using back_end.Helpers;
using back_end.Services;

namespace back_end.Controllers
{
  // Staff: the table's leftover fee, shared across its open bills by guest count (see Helpers/LeftoverFee.cs)
  [ApiController]
  [Route("api/bill/leftover-fee")]
  [Authorize(Policy = "staffOnly")]
  public class LeftoverFeeController : ControllerBase
  {
    private readonly ApplicationDbContext _context;
    private readonly ILogger<LeftoverFeeController> _logger;
    private readonly IPricingService _pricingService;

    public LeftoverFeeController(ApplicationDbContext context, ILogger<LeftoverFeeController> logger, IPricingService pricingService)
    {
      _context = context;
      _logger = logger;
      _pricingService = pricingService;
    }

    /// <summary>
    /// The table's leftover fee: the default amount, and each open bill's current share.
    /// </summary>
    /// <param name="sessionId">The dining session (table)</param>
    /// <param name="amount">Optional. Previews this fee: each open bill's share of it, without saving anything</param>
    /// <response code="200">The default fee, the current total and each open bill's share</response>
    /// <response code="400">If the previewed amount is negative</response>
    /// <response code="404">If the session doesn't exist</response>
    /// <response code="403">The table is at another location, or the caller isn't staff.</response>
    /// <response code="500">If there was an error retrieving the leftover fee</response>
    /// <remarks>
    /// Sample requests:
    ///
    ///     GET /api/bill/leftover-fee/456
    ///     GET /api/bill/leftover-fee/456?amount=15.00
    ///
    /// Without amount: the fee already applied and each open bill's share of it.
    /// With amount: what each open bill's share would be (shared by guest count, as PUT applies it),
    /// so the "Add leftover fee" dialog can show it before staff apply it. Nothing is saved.
    /// </remarks>
    [HttpGet("{sessionId}")]
    [ProducesResponseType(typeof(LeftoverFeeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetLeftoverFee(int sessionId, [FromQuery(Name = "amount")] decimal? amount = null)
    {
      try
      {
        var locationId = await SessionLocationAsync(sessionId);
        if (locationId == null)
        {
          return NotFound(new { message = "Session was not found" });
        }

        if (!await SessionAccess.CanViewSessionAsync(_context, User, sessionId))
        {
          return LocationResults.OtherLocation();
        }

        if (amount < 0)
        {
          return BadRequest(new { message = "The leftover fee can't be negative." });
        }

        var openBills = await OpenBillsAsync(sessionId);
        var pricing = await _pricingService.GetCurrentPricingAsync(locationId.Value);
        return Ok(amount == null
          ? LeftoverFeeResult(sessionId, openBills, pricing.LeftoverFee)
          : LeftoverFeePreview(sessionId, openBills, pricing.LeftoverFee, amount.Value));
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting leftover fee for session {SessionId}", sessionId);
        return StatusCode(500, new { message = "Error getting leftover fee" });
      }
    }

    /// <summary>
    /// Applies a leftover fee to the table, split evenly across its open bills.
    /// </summary>
    /// <param name="sessionId">The dining session (table)</param>
    /// <param name="request">The fee; leave amount out to use the default, or send 0 to remove it</param>
    /// <response code="200">The fee was applied; returns each open bill's share</response>
    /// <response code="400">If the amount is negative or the table has no open bills</response>
    /// <response code="403">The table is at another location, or the caller isn't staff.</response>
    /// <response code="404">If the session doesn't exist</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     PUT /api/bill/leftover-fee/456
    ///     { "amount": 15.00 }
    ///
    /// Staff decide when a table has left too much food. The fee is for the whole table and is
    /// shared across its open bills by how many guests are on each, toddlers included.
    /// Example $20.00 over a bill of 4 guests and a bill of 1 guest, bill of 4 = $16.00.
    /// bill of 1 = $4.00, replacing any fee applied before. The shares add up exactly.
    /// It's shared again whenever an open bill's guest count changes. Closed and
    /// paid bills keep what they had, so apply it before closing bills.
    /// </remarks>
    [HttpPut("{sessionId}")]
    [ProducesResponseType(typeof(LeftoverFeeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApplyLeftoverFee(int sessionId, [FromBody] LeftoverFeeRequest request)
    {
      try
      {
        if (!await _context.DiningSessions.AnyAsync(s => s.Session_Id == sessionId))
        {
          return NotFound(new { message = "Session was not found" });
        }

        if (!await SessionAccess.CanViewSessionAsync(_context, User, sessionId))
        {
          return LocationResults.OtherLocation();
        }
        var locationId = await SessionLocationAsync(sessionId);
        if (locationId == null)
        {
          return NotFound(new { message = "Session was not found" });
        }
        // The location's default fee unless staff entered an amount
        var defaultFee = (await _pricingService.GetCurrentPricingAsync(locationId.Value)).LeftoverFee;
        var amount = request?.Amount ?? defaultFee;
        if (amount < 0)
        {
          return BadRequest(new { message = "The leftover fee can't be negative." });
        }

        var openBills = await OpenBillsAsync(sessionId);
        if (openBills.Count == 0)
        {
          return BadRequest(new { message = "This table has no open bills to add a leftover fee to." });
        }

        var shares = LeftoverFee.SplitByGuests(amount, openBills.Select(b => b.Total_Count).ToList());
        for (var i = 0; i < openBills.Count; i++)
        {
          openBills[i].Leftover_Charge = shares[i];
        }
        await _context.SaveChangesAsync();

        _logger.LogInformation("Leftover fee {Amount:F2} applied to session {SessionId} across {Count} bills",
            amount, sessionId, openBills.Count);

        return Ok(LeftoverFeeResult(sessionId, openBills, defaultFee));
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error applying leftover fee to session {SessionId}", sessionId);
        return StatusCode(500, new { message = "Error applying leftover fee" });
      }
    }

    // Open bills in a fixed order (oldest first), so any extra cent always lands on the same bill
    private Task<List<Billing>> OpenBillsAsync(int sessionId) =>
        _context.Bills
            .Where(b => b.Session_Id == sessionId && b.Status == BillStatus.Open)
            .OrderBy(b => b.Bill_Id)
            .ToListAsync();

    // The session's location, or null if the session doesn't exist
    private Task<int?> SessionLocationAsync(int sessionId) =>
        _context.DiningSessions
            .Where(s => s.Session_Id == sessionId)
            .Select(s => (int?)s.Location_Id)
            .FirstOrDefaultAsync();

    private static LeftoverFeeResponse LeftoverFeeResult(int sessionId, List<Billing> openBills, decimal defaultFee) => new()
    {
      SessionId = sessionId,
      DefaultFee = defaultFee,
      Amount = openBills.Sum(b => b.Leftover_Charge),
      Bills = openBills.Select(b => new LeftoverFeeBillShare
      {
        BillId = b.Bill_Id,
        BillName = b.Bill_Name,
        GuestCount = b.Total_Count,
        LeftoverCharge = b.Leftover_Charge,
      }).ToList(),
    };

    // What each open bill's share of this fee would be, worked out the same way ApplyLeftoverFee does it
    private static LeftoverFeeResponse LeftoverFeePreview(int sessionId, List<Billing> openBills, decimal defaultFee, decimal amount)
    {
      var shares = LeftoverFee.SplitByGuests(amount, openBills.Select(b => b.Total_Count).ToList());
      return new()
      {
        SessionId = sessionId,
        DefaultFee = defaultFee,
        Amount = shares.Sum(),
        Bills = openBills.Select((b, i) => new LeftoverFeeBillShare
        {
          BillId = b.Bill_Id,
          BillName = b.Bill_Name,
          GuestCount = b.Total_Count,
          LeftoverCharge = shares[i],
        }).ToList(),
      };
    }
  }
}
