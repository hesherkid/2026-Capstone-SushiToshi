using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using back_end.Helpers;

namespace back_end.Controllers
{
  // Staff: the free ice cream offered with the bill (see Helpers/FreeIceCream.cs).
  // Guests say yes when they ask for the bill (BillController.RequestBill)
  [ApiController]
  [Route("api/bill")]
  [Authorize(Policy = "staffOnly")]
  public class FreeIceCreamController : ControllerBase
  {
    private readonly ApplicationDbContext _context;
    private readonly ILogger<FreeIceCreamController> _logger;

    public FreeIceCreamController(ApplicationDbContext context, ILogger<FreeIceCreamController> logger)
    {
      _context = context;
      _logger = logger;
    }

    /// <summary>
    /// The free ice cream flavours staff can send with the bill.
    /// </summary>
    /// <remarks>
    /// The Available items in the Dessert category, in the order they were added. Admins manage them
    /// with the menu item tools: add an item to Dessert for a new flavour, or set one Unavailable to
    /// stop offering it. Dessert items can't go on a menu, so guests can't order them.
    ///
    /// Sample request:
    ///
    ///     GET /api/bill/ice-cream-flavours
    /// </remarks>
    /// <response code="200">The flavours (possibly none).</response>
    /// <response code="403">The caller isn't staff.</response>
    [HttpGet("ice-cream-flavours")]
    [ProducesResponseType(typeof(List<IceCreamFlavourOptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetIceCreamFlavours()
    {
      try
      {
        var flavours = await FreeIceCream.Flavours(_context)
            .Select(i => new IceCreamFlavourOptionDto { ItemId = i.item_id, Name = i.Name })
            .ToListAsync();
        return Ok(flavours);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading ice cream flavours");
        return StatusCode(500, new { message = "Error loading ice cream flavours" });
      }
    }

    /// <summary>
    /// Staff send free ice cream to the kitchen for the bills at a table that are ready for it.
    /// </summary>
    /// <remarks>
    /// Guests say yes on their own bill when they ask for it. Staff go to the table, ask which
    /// flavours, and send them for every bill that's ready in one go: the ones that said yes, and
    /// any other party at the table that wants some too. Each bill gets its ice cream once
    /// (Ice_Cream_Sent_At); a bill that says yes later gets its own turn.
    ///
    /// Adds a pending order with a line for each flavour at $0. The flavours are the Available items
    /// in the Dessert category (see ice-cream-flavours), so analytics count each one. An order needs
    /// a bill, so it goes on the first chosen bill that said yes. It's free, so no total changes.
    /// It works on closed bills too: the ice cream comes after the bill, so staff can close it
    /// and take payment first. Up to one each for the guests on the chosen bills, so each chosen
    /// bill needs a guest count.
    /// Sending again for the same bills returns the first time.
    ///
    /// Sample request:
    ///
    ///     POST /api/bill/send-ice-cream/269
    ///     { "bill_ids": [332, 333], "flavours": [ { "item_id": 120, "quantity": 2 }, { "item_id": 121, "quantity": 1 } ] }
    /// </remarks>
    /// <param name="sessionId">The dining session (the table) the ice cream is for</param>
    /// <param name="request">The bills it's for, and how many of each flavour</param>
    /// <response code="200">The ice cream order is pending; returns when it was sent, and whether it already had been.</response>
    /// <response code="400">No bills chosen, a bill isn't at this table, was cancelled, already had ice cream or has no guests, a flavour isn't available, the count is 0 or more than the guests, or the table has left.</response>
    /// <response code="403">The table is at another location, or the caller isn't staff.</response>
    /// <response code="404">The session was not found.</response>
    [HttpPost("send-ice-cream/{sessionId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendIceCream(int sessionId, SendIceCreamDto request)
    {
      try
      {
        var chosenIds = (request?.BillIds ?? new List<int>()).Distinct().ToArray();
        if (chosenIds.Length == 0)
        {
          return BadRequest(new { message = "Choose who the ice cream is for." });
        }

        // So two taps (or two staff) can't send it twice
        await using var billLock = await BillLock.LockAsync(_context, chosenIds);

        var session = await _context.DiningSessions
            .Include(ds => ds.Bills)
            .FirstOrDefaultAsync(ds => ds.Session_Id == sessionId);
        if (session == null)
        {
          return NotFound(new { message = "Session was not found" });
        }
        if (!await SessionAccess.CanViewSessionAsync(_context, User, sessionId))
        {
          return LocationResults.OtherLocation();
        }

        var chosen = session.Bills.Where(b => chosenIds.Contains(b.Bill_Id)).ToList();
        if (chosen.Count != chosenIds.Length)
        {
          return BadRequest(new { message = "A chosen bill isn't at this table." });
        }
        // A repeat of a send that already went through (a double tap)
        if (chosen.All(b => b.Ice_Cream_Sent_At != null))
        {
          return Ok(new { ice_cream_sent_at = chosen.Max(b => b.Ice_Cream_Sent_At), already_sent = true });
        }
        if (session.Ended_At != null)
        {
          return BadRequest(new { message = "This table has already left." });
        }
        var cancelled = chosen.FirstOrDefault(b => b.Status == BillStatus.Cancelled);
        if (cancelled != null)
        {
          return BadRequest(new { message = $"{cancelled.Bill_Name} was cancelled." });
        }
        var served = chosen.FirstOrDefault(b => b.Ice_Cream_Sent_At != null);
        if (served != null)
        {
          return BadRequest(new { message = $"{served.Bill_Name} already had their ice cream." });
        }

        // Same flavour twice adds up; flavours with none are left out
        var flavourIds = await FreeIceCream.Flavours(_context).Select(i => i.item_id).ToListAsync();
        var counts = new Dictionary<int, int>();
        foreach (var line in request?.Flavours ?? new List<IceCreamFlavourDto>())
        {
          if (!flavourIds.Contains(line.ItemId))
          {
            return BadRequest(new { message = "That ice cream flavour isn't available. Refresh and choose again." });
          }
          if (line.Quantity < 0)
          {
            return BadRequest(new { message = "Ice cream counts can't be negative." });
          }
          counts[line.ItemId] = counts.GetValueOrDefault(line.ItemId) + line.Quantity;
        }
        var total = counts.Values.Sum();
        if (total == 0)
        {
          return BadRequest(new { message = "Choose at least one ice cream." });
        }
        // One each for the chosen bills' guests, so each needs a guest count first
        var uncounted = chosen.FirstOrDefault(b => b.Total_Count <= 0);
        if (uncounted != null)
        {
          return BadRequest(new { message = $"{uncounted.Bill_Name} has no guests yet. Fix the count with Edit Guests first." });
        }
        var guests = chosen.Sum(b => b.Total_Count);
        if (total > guests)
        {
          return BadRequest(new { message = $"That's {total} ice creams for {guests} guests: one each at most." });
        }

        // The first chosen bill that said yes, or the first chosen bill
        var bill = chosen
            .OrderByDescending(b => b.Wants_Ice_Cream)
            .ThenBy(b => b.Requested_At ?? DateTime.MaxValue)
            .ThenBy(b => b.Bill_Id)
            .First();

        int.TryParse(ClaimsHelpers.GetUserId(User), out var staffId);
        var now = DateTime.UtcNow;
        var order = new SessionOrder
        {
          session_id = session.Session_Id,
          Bill_Id = bill.Bill_Id,
          User_Id = staffId > 0 ? staffId : null,
          User_Name = ClaimsHelpers.GetUserDisplayName(User),
          Status = OrderStatus.Pending,
          Created_At = now,
        };
        // In the usual flavour order, so the kitchen sees them the same way every time
        foreach (var itemId in flavourIds.Where(id => counts.GetValueOrDefault(id) > 0))
        {
          order.OrderItems.Add(new OrderItems
          {
            Menu_Id = session.Menu_Id,
            Item_Id = itemId,
            Quantity = counts[itemId],
            Price_At_Time = 0m, // free, so the bill leaves it out
            Order_Item_Status = OrderStatus.Pending,
          });
        }
        _context.SessionOrders.Add(order);
        foreach (var b in chosen)
        {
          b.Ice_Cream_Sent_At = now;
        }
        await _context.SaveChangesAsync();
        if (billLock != null) await billLock.CommitAsync();

        return Ok(new { ice_cream_sent_at = now, already_sent = false });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error sending ice cream for session {SessionId}", sessionId);
        return StatusCode(500, new { message = "Error sending ice cream" });
      }
    }
  }
}
