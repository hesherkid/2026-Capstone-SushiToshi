using back_end.domain.DbContexts;
using back_end.domain.enums;
using back_end.DTO.PastVisitDTOs;
using back_end.Helpers;
using back_end.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back_end.Controllers
{
    // A signed-in customer's past visits: when and where, what they ordered and what the bill came to.
    // Guests get 403, since their throwaway account is lost when the visit ends.
    [ApiController]
    [Route("api/me/visits")]
    [Authorize(Roles = "Customer")]
    public class MyVisitsController : ControllerBase
    {
        // Most recent visits only; each one works out its bill totals
        public const int VisitLimit = 20;

        private readonly ApplicationDbContext _context;
        private readonly IPricingService _pricingService;
        private readonly ILogger<MyVisitsController> _logger;

        public MyVisitsController(ApplicationDbContext context, IPricingService pricingService, ILogger<MyVisitsController> logger)
        {
            _context = context;
            _pricingService = pricingService;
            _logger = logger;
        }

        /// <summary>
        /// The customer's ended visits, most recent first (at most 20).
        /// </summary>
        /// <remarks>
        /// Sample response:
        ///
        ///     GET /api/me/visits
        ///     [
        ///       {
        ///         "session_id": 42,
        ///         "started_at": "2026-10-24T01:15:00Z",
        ///         "ended_at": "2026-10-24T02:40:00Z",
        ///         "location_name": "Sushi Toshi South",
        ///         "table_numbers": [14],
        ///         "items": [{ "item_id": 7, "name": "Salmon Roll", "quantity": 3 }],
        ///         "bills": [{ "bill_id": 88, "bill_name": "Lee", "guest_count": 2, "total": 71.45 }]
        ///       }
        ///     ]
        ///
        /// Rules:
        /// - A visit is a dining session the customer joined that staff have ended; the current visit isn't listed.
        /// - Items are only what this customer ordered. Cancelled orders and items don't count.
        /// - Bills are the ones the customer's orders went on; if they didn't order, every bill at the table.
        ///   Cancelled bills are left out. Totals include gratuity and GST, worked out as on the bill.
        /// </remarks>
        [HttpGet]
        [ProducesResponseType(typeof(List<PastVisitDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPastVisits()
        {
            if (!int.TryParse(ClaimsHelpers.GetUserId(User), out var userId))
            {
                return Unauthorized(new { message = "User ID not found in claims." });
            }
            if (GuestAccounts.IsGuest(User))
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "Sign in to an account to see your past visits." });
            }

            try
            {
                var sessions = await _context.DiningSessions
                    .AsNoTracking()
                    .Where(s => s.Ended_At != null && s.Participants.Any(p => p.User_Id == userId))
                    .OrderByDescending(s => s.Started_At)
                    .Take(VisitLimit)
                    .Include(s => s.Location)
                    .Include(s => s.Table)
                    .Include(s => s.TableGroup)
                        .ThenInclude(tg => tg!.Tables)
                    .Include(s => s.Bills)
                    .ToListAsync();

                var sessionIds = sessions.Select(s => s.Session_Id).ToList();

                // This customer's order lines on those visits
                var myLines = await _context.OrderItems
                    .AsNoTracking()
                    .Where(oi => oi.SessionOrder.User_Id == userId
                            && sessionIds.Contains(oi.SessionOrder.session_id)
                            && oi.SessionOrder.Status != OrderStatus.Cancelled
                            && oi.Order_Item_Status != OrderStatus.Cancelled)
                    .Select(oi => new
                    {
                        SessionId = oi.SessionOrder.session_id,
                        BillId = oi.SessionOrder.Bill_Id,
                        oi.Item_Id,
                        oi.MenuItem.Name,
                        oi.Quantity
                    })
                    .ToListAsync();

                var visits = new List<PastVisitDTO>();
                foreach (var session in sessions)
                {
                    var lines = myLines.Where(l => l.SessionId == session.Session_Id).ToList();
                    var myBillIds = lines.Select(l => l.BillId).ToHashSet();

                    var bills = session.Bills
                        .Where(b => b.Status != BillStatus.Cancelled
                                && (myBillIds.Count == 0 || myBillIds.Contains(b.Bill_Id)))
                        .OrderBy(b => b.Bill_Id)
                        .ToList();

                    var billDtos = new List<PastVisitBillDTO>();
                    foreach (var bill in bills)
                    {
                        billDtos.Add(new PastVisitBillDTO
                        {
                            BillId = bill.Bill_Id,
                            BillName = bill.Bill_Name,
                            GuestCount = bill.Adult_Count + bill.Senior_Count + bill.Child_Count + bill.Tot_Count,
                            Total = await BillTotalQuery.TotalAsync(_context, _pricingService, bill, session.Location_Id)
                        });
                    }

                    visits.Add(new PastVisitDTO
                    {
                        SessionId = session.Session_Id,
                        StartedAt = session.Started_At,
                        EndedAt = session.Ended_At,
                        LocationName = session.Location.Name,
                        TableNumbers = session.Table != null
                            ? [session.Table.table_number]
                            : session.TableGroup?.Tables.Select(t => t.table_number).OrderBy(n => n).ToList() ?? [],
                        Items = lines
                            .GroupBy(l => new { l.Item_Id, l.Name })
                            .Select(g => new PastVisitItemDTO { ItemId = g.Key.Item_Id, Name = g.Key.Name, Quantity = g.Sum(l => l.Quantity) })
                            .OrderByDescending(i => i.Quantity)
                            .ThenBy(i => i.Name)
                            .ToList(),
                        Bills = billDtos
                    });
                }

                return Ok(visits);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading past visits for user {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while loading your past visits." });
            }
        }
    }
}
