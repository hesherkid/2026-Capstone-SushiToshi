using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using System.Linq.Expressions;
using back_end.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using back_end.domain.Seeders;
using System.Security.Claims;
using Swashbuckle.AspNetCore.Annotations;
using back_end.Helpers;

namespace back_end.Controllers
{
    [ApiController]
    [Route("api/[controller]")]

    public class SessionParticipantController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SessionParticipantController> _logger;

        public SessionParticipantController(ApplicationDbContext context, ILogger<SessionParticipantController> logger)
        {
            _context = context;
            _logger = logger;
        }


        /// <summary>
        /// Leaves the specified dining session.
        /// </summary>
        /// <param name="sessionId">The session identifier to leave.</param>
        /// <remarks>
        /// - You must currently be an active participant.  
        /// - You cannot leave if you have any non-delivered orders in the session.  
        /// - Sets <c>Left_At</c> to the current UTC time.
        /// </remarks>
        /// <response code="200">Left the session successfully.</response>
        /// <response code="401">The request is unauthenticated or the user claim is missing.</response>
        /// <response code="404">The session or user record was not found.</response>
        /// <response code="400">The user is not an active participant, or has active orders.</response>
        /// <response code="500">An unexpected error occurred while leaving the session.</response>
        [Authorize]
        [HttpPost("{sessionId:int}/leave")]
        [Produces("application/json")]
        [SwaggerOperation(
            OperationId = "LeaveSession",
            Summary = "Leave a dining session",
            Description = "Marks the current user as having left the specified session, provided there are no active orders."
        )]
        public async Task<IActionResult> LeaveSession(
                int sessionId
            )
        {
            try
            {

                var session = await _context.DiningSessions.FirstOrDefaultAsync(ds => ds.Session_Id == sessionId);
                if (session is null)
                {
                    return NotFound("Dining Session was not found");
                }

                // Get authenticated user info
                var userIdString = ClaimsHelpers.GetUserId(User);

                if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
                {
                    return Unauthorized("User ID not found in claims or invalid format.");
                }

                var existingParticipant = await _context.SessionParticipants
                    .FirstOrDefaultAsync(sp => sp.Session_Id == sessionId && sp.User_Id == userId && !sp.Left_At.HasValue);
                if (existingParticipant is null)
                {
                    return BadRequest("You are not a active participant to the session");
                }
                var activeOrders = await _context.SessionOrders
                    .FirstOrDefaultAsync(so => so.session_id == sessionId && so.User_Id == userId && so.Status != OrderStatus.Delivered);
                if (activeOrders != null)
                {
                    return BadRequest("You can not leave a active session with active orders");
                }
                existingParticipant.Left_At = DateTime.UtcNow;
                _context.SessionParticipants.Update(existingParticipant);
                await _context.SaveChangesAsync();
                return Ok("You have successfully left a session");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving session {SessionId}", sessionId);
                return StatusCode(500, "Error leaving session");
            }
        }

        /// <summary>
        /// The bill you're on at your table, and the table's open bills you could join.
        /// </summary>
        /// <remarks>
        /// Guests pick their party's bill (or start one with POST /api/bill/create-bill) before ordering.
        /// Other bills show only their name and party size.
        /// </remarks>
        /// <response code="200">Your bill (null if you haven't picked one) and the open bills.</response>
        /// <response code="403">You aren't seated at this table.</response>
        [Authorize]
        [HttpGet("{sessionId:int}/bill")]
        [ProducesResponseType(typeof(MyBillResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMyBill(int sessionId)
        {
            var seat = await MySeatAsync(sessionId);
            if (seat is null)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You aren't seated at this table." });
            }

            var bills = await _context.Bills
                .Where(b => b.Session_Id == sessionId && (b.Status == BillStatus.Open || b.Bill_Id == seat.Bill_Id))
                .OrderBy(b => b.Created_At)
                .ToListAsync();

            // People still seated on each bill
            var people = await _context.SessionParticipants
                .Where(sp => sp.Session_Id == sessionId && sp.Left_At == null && sp.Bill_Id != null)
                .GroupBy(sp => sp.Bill_Id!.Value)
                .Select(g => new { Bill_Id = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Bill_Id, g => g.Count);
            int PeopleOn(Billing bill) => people.GetValueOrDefault(bill.Bill_Id);

            var myBill = bills.FirstOrDefault(b => b.Bill_Id == seat.Bill_Id);
            return Ok(new MyBillResponseDto
            {
                MyBill = myBill is null ? null : new MyBillDto
                {
                    BillId = myBill.Bill_Id,
                    BillName = myBill.Bill_Name,
                    TotalCount = myBill.Total_Count,
                    Status = myBill.Status.ToString(),
                    RequestedAt = myBill.Requested_At,
                    PeopleCount = PeopleOn(myBill),
                    AdultCount = myBill.Adult_Count,
                    SeniorCount = myBill.Senior_Count,
                    ChildCount = myBill.Child_Count,
                    TotCount = myBill.Tot_Count,
                    CountsLocked = myBill.Counts_Locked,
                },
                ChangeBlockedReason = await BillLinks.CantLeaveReasonAsync(_context, seat),
                OpenBills = bills.Where(b => b.Status == BillStatus.Open).Select(b => Choice(b, PeopleOn(b))).ToList(),
            });
        }

        /// <summary>
        /// Puts you on one of your table's open bills.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     PUT /api/sessionparticipant/123/bill
        ///     { "bill_id": 45 }
        ///
        /// You can switch bills until you've ordered on one; after that, ask staff to move you.
        /// Bills that have asked for the bill can't be joined.
        /// </remarks>
        /// <response code="200">You're on the bill; returns the same as GET.</response>
        /// <response code="400">The bill can't be joined, or you've already ordered on another bill.</response>
        /// <response code="403">You aren't seated at this table.</response>
        [Authorize]
        [HttpPut("{sessionId:int}/bill")]
        [ProducesResponseType(typeof(MyBillResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> ChooseBill(int sessionId, ChooseBillDto request)
        {
            var seat = await MySeatAsync(sessionId);
            if (seat is null)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You aren't seated at this table." });
            }

            if (seat.Bill_Id == request.BillId)
            {
                return await GetMyBill(sessionId);
            }

            var bill = await _context.Bills.FirstOrDefaultAsync(b => b.Bill_Id == request.BillId);
            var cantJoin = BillLinks.CantJoinReason(bill, sessionId)
                ?? await BillLinks.CantLeaveReasonAsync(_context, seat);
            if (cantJoin != null)
            {
                return BadRequest(new { message = cantJoin });
            }

            seat.Bill_Id = request.BillId;
            await _context.SaveChangesAsync();
            return await GetMyBill(sessionId);
        }

        /// <summary>
        /// Changes the guest counts on your bill, e.g. when more of your party arrives.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     PUT /api/sessionparticipant/123/bill/guests
        ///     { "adult_count": 3, "senior_count": 0, "child_count": 1, "tot_count": 0 }
        ///
        /// Anyone on the bill can change it while it's open and nobody has asked for the bill.
        /// The table's open bills together can't seat more guests than the table has seats,
        /// though they can always lower their count (staff may have gone past the seats).
        /// A change marks the bill Counts_Changed_By_Guest so staff check the counts.
        /// Once staff lock the counts (Counts_Locked), only staff can change them.
        /// </remarks>
        /// <response code="200">The counts are saved; returns the same as GET.</response>
        /// <response code="400">You aren't on a bill, it can't be changed or staff locked its counts, or the counts aren't valid.</response>
        /// <response code="403">You aren't seated at this table.</response>
        [Authorize]
        [HttpPut("{sessionId:int}/bill/guests")]
        [ProducesResponseType(typeof(MyBillResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateGuestCounts(int sessionId, UpdateGuestCountsDto request)
        {
            var seat = await MySeatAsync(sessionId);
            if (seat is null)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You aren't seated at this table." });
            }
            if (seat.Bill_Id is null)
            {
                return BadRequest(new { message = "Choose your bill first." });
            }

            // So staff can't close the bill, or change its count, while this one saves
            await using var billLock = await BillLock.LockAsync(_context, seat.Bill_Id.Value);
            var bill = await _context.Bills
                .Include(b => b.DiningSession).ThenInclude(ds => ds.Table)
                .Include(b => b.DiningSession).ThenInclude(ds => ds.TableGroup).ThenInclude(tg => tg!.Tables)
                .FirstAsync(b => b.Bill_Id == seat.Bill_Id);
            if (bill.Status != BillStatus.Open)
            {
                return BadRequest(new { message = $"{bill.Bill_Name} is already {bill.Status.ToString().ToLower()}." });
            }
            if (bill.Requested_At != null)
            {
                return BadRequest(new { message = $"{bill.Bill_Name} has already asked for the bill. Ask your server to change the guest count." });
            }
            if (bill.Counts_Locked)
            {
                return BadRequest(new { message = "Your server has confirmed your party. Ask your server if you need your party adjusted." });
            }

            int total = request.AdultCount + request.SeniorCount + request.ChildCount + request.TotCount;
            var problem = await BillLinks.GuestCountProblemAsync(_context, bill, total);
            if (problem != null)
            {
                return BadRequest(new { message = total == 0 ? problem : $"{problem} Ask your server if more of your party is coming." });
            }

            if (BillLinks.SetCounts(bill, request.AdultCount, request.SeniorCount, request.ChildCount, request.TotCount))
            {
                bill.Counts_Changed_By_Guest = true;
                await _context.SaveChangesAsync();
            }
            if (billLock != null) await billLock.CommitAsync();
            return await GetMyBill(sessionId);
        }

        /// <summary>
        /// Staff: moves someone at the table to another of its open bills, or onto one if they have none,
        /// or onto a new bill split off their old one.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     PUT /api/sessionparticipant/123/participants/45/bill
        ///     { "bill_id": 67, "move_orders": true, "guest_type": "adult", "guest_count": 1 }
        /// 
        ///     PUT /api/sessionparticipant/123/participants/45/bill
        ///     { "new_bill": true, "move_orders": true, "guest_type": "adult", "guest_count": 1 }
        ///
        /// With move_orders, the orders they placed on their old bill go with them (cancelled ones
        /// stay), so their extras are charged on the new bill. With guest_type, guest_count guests of
        /// that kind also move from the old bill's count to the new one's; the old bill keeps at least one.
        /// Moving everyone who ordered something onto a new bill this way splits a bill by person.
        /// Orders can only move off a bill that's still open, since a closed bill's total is final.
        /// Staff can move someone even after they've ordered, and onto a bill that has asked for the bill.
        /// 
        /// With new_bill instead of bill_id, the bill they move to is started for them. It's name after 
        /// them or {old bill} 1, {old bill} 2, if they have no name, it's guest are the guest_count guests taken
        /// off the old bill, so the table's counts remain accurate. This will split the bill in one step, with no
        /// counts being entered twice. The table's left over fee is then shared evenly across its open bills 
        /// again, the new one included. new_bill needs guest_type, and someone already on an open bill.
        /// </remarks>
        /// <response code="200">They're on the bill.</response>
        /// <response code="400">The bill isn't open, isn't at the table, or their orders can't move.</response>
        /// <response code="403">The table is at another location.</response>
        /// <response code="404">Nobody with that seat is at the table.</response>
        [Authorize(Policy = "staffOnly")]
        [HttpPut("{sessionId:int}/participants/{participantId:int}/bill")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> MoveGuest(int sessionId, int participantId, MoveGuestDto request)
        {
            if (request.NewBill == (request.BillId != null))
            {
                return BadRequest(new { message = "Pick a bill to move them to, or a new bill, but not both." });
            }
            if (request.NewBill && request.GuestType == null)
            {
                return BadRequest(new { message = "Say which guests go on the new bill, so it isn't empty." });
            }

            if (!await SessionAccess.CanViewSessionAsync(_context, User, sessionId))
            {
                return LocationResults.OtherLocation();
            }

            var seat = await _context.SessionParticipants.FirstOrDefaultAsync(sp =>
                sp.Participant_Id == participantId && sp.Session_Id == sessionId && sp.Left_At == null);
            if (seat is null)
            {
                return NotFound(new { message = "That person isn't at this table." });
            }
            if (!request.NewBill && seat.Bill_Id == request.BillId)
            {
                return Ok(new { bill_id = seat.Bill_Id, new_bill = false, orders_moved = 0, counts_moved = false });
            }

            var fromBillId = seat.Bill_Id;
            if (request.NewBill && fromBillId == null)
            {
                return BadRequest(new { message = "They aren't on a bill yet, so there are no guests to split off. Start a New Bill for them instead." });
            }

            // Nobody can close either bill, or order onto the old one, while their orders move
            var lockIds = new List<int>();
            if (fromBillId != null) lockIds.Add(fromBillId.Value);
            if (request.BillId != null) lockIds.Add(request.BillId.Value);

            await using var billLock = await BillLock.LockAsync(_context, lockIds.ToArray());
            var from = fromBillId is int id ? await _context.Bills.FirstAsync(b => b.Bill_Id == id) : null;

            Billing to;
            if (request.NewBill)
            {
                // A new bill takes its guests off the old one, so the old one has to be open to change
                if (from!.Status != BillStatus.Open)
                {
                    return BadRequest(new { message = $"{from.Bill_Name} is already {from.Status.ToString().ToLower()}, so no guests can split off it." });
                }
                to = new Billing
                {
                    Session_Id = sessionId,
                    Bill_Name = await NewBillNameAsync(sessionId, seat, from),
                    Status = BillStatus.Open,
                };
            }
            else
            {
                var existing = await _context.Bills.FirstOrDefaultAsync(b => b.Bill_Id == request.BillId);
                if (existing == null || existing.Session_Id != sessionId)
                {
                    return BadRequest(new { message = "That bill isn't at this table." });
                }
                if (existing.Status != BillStatus.Open)
                {
                    return BadRequest(new { message = $"{existing.Bill_Name} is already {existing.Status.ToString().ToLower()}." });
                }
                to = existing;
            }

            var orders = new List<SessionOrder>();
            if (request.MoveOrders && from != null && seat.User_Id != null)
            {
                orders = await _context.SessionOrders
                    .Where(o => o.Bill_Id == from.Bill_Id && o.User_Id == seat.User_Id && o.Status != OrderStatus.Cancelled)
                    .ToListAsync();
                if (orders.Count > 0 && from.Status != BillStatus.Open)
                {
                    return BadRequest(new { message = $"{from.Bill_Name} is already {from.Status.ToString().ToLower()}, so its orders can't move." });
                }
            }

            // The guests leave the old bill's count and join the new one's, table's total stays the same
            var countsMoved = false;
            if (request.GuestType != null && from != null)
            {
                if (from.Status != BillStatus.Open)
                {
                    return BadRequest(new { message = $"{from.Bill_Name} is already {from.Status.ToString().ToLower()}, so its guest count can't change." });
                }
                var n = request.GuestCount;
                var (adults, seniors, children, toddlers) = request.GuestType switch
                {
                    "adult" => (n, 0, 0, 0),
                    "senior" => (0, n, 0, 0),
                    "child" => (0, 0, n, 0),
                    _ => (0, 0, 0, n),
                };
                if (from.Adult_Count < adults || from.Senior_Count < seniors ||
                    from.Child_Count < children || from.Tot_Count < toddlers)
                {
                    return BadRequest(new { message = $"{from.Bill_Name} doesn't have {n} {GuestTypePlural(request.GuestType, n)} to move." });
                }
                if (from.Total_Count - n < 1)
                {
                    return BadRequest(new
                    {
                        message = request.NewBill
                            ? $"That would leave {from.Bill_Name} with no guests. Split off fewer guests."
                            : $"That would leave {from.Bill_Name} with no guests. Move fewer guests, move them without changing the guest counts, or cancel {from.Bill_Name}."
                    });
                }

                BillLinks.SetCounts(from, from.Adult_Count - adults, from.Senior_Count - seniors,
                    from.Child_Count - children, from.Tot_Count - toddlers);
                BillLinks.SetCounts(to, to.Adult_Count + adults, to.Senior_Count + seniors,
                    to.Child_Count + children, to.Tot_Count + toddlers);
                countsMoved = true;
            }

            // Every check has passed, so the new bill only exists if everything else moves too
            if (request.NewBill)
            {
                await ReshareLeftoverFeeAsync(sessionId, to);
                _context.Bills.Add(to);
            }
            // Through the navigation, so a new bill's id is filled in when it's saved
            orders.ForEach(o => o.Bill = to);
            seat.Bill = to;
            await _context.SaveChangesAsync();
            if (billLock != null) await billLock.CommitAsync();

            return Ok(new { bill_id = to.Bill_Id, new_bill = request.NewBill, orders_moved = orders.Count, counts_moved = countsMoved });
        }

        // The new bill's name.  Either the person's, or "{old bill} 1", "{old bill} 2"... if they have none,
        // skipping names already used at the table
        private async Task<string> NewBillNameAsync(int sessionId, SessionParticipant seat, Billing from)
        {
            var email = seat.User_Id == null ? null : await _context.Users
                .Where(u => u.User_id == seat.User_Id)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();
            if (!GuestAccounts.IsGuest(email) && !string.IsNullOrWhiteSpace(seat.User_Name))
            {
                return seat.User_Name.Trim();
            }

            var taken = await _context.Bills
                .Where(b => b.Session_Id == sessionId)
                .Select(b => b.Bill_Name)
                .ToListAsync();
            // Bill_Name is at most 255 characters, leave room for the number
            var stem = from.Bill_Name.Length > 250 ? from.Bill_Name[..250] : from.Bill_Name;
            var number = 1;
            while (taken.Contains($"{stem} {number}")) number++;
            return $"{stem} {number}";
        }

        // The same as BillController.ApplyLeftoverFee. The table's fee is shared evenly across its
        // open bills, oldest first, so a new bill takes its share.
        private async Task ReshareLeftoverFeeAsync(int sessionId, Billing newBill)
        {
            var openBills = await _context.Bills
                .Where(b => b.Session_Id == sessionId && b.Status == BillStatus.Open)
                .OrderBy(b => b.Bill_Id)
                .ToListAsync();
            var fee = openBills.Sum(b => b.Leftover_Charge);
            if (fee == 0m) return;

            openBills.Add(newBill);
            var shares = LeftoverFee.SplitEvenly(fee, openBills.Count);
            for (var i = 0; i < openBills.Count; i++)
            {
                openBills[i].Leftover_Charge = shares[i];
            }
        }

        private static string GuestTypePlural(string guestType, int count) => count == 1 ? guestType : guestType switch
        {
            "child" => "children",
            _ => guestType + "s",
        };

        // The caller's seat at this table, if they're still seated
        private async Task<SessionParticipant?> MySeatAsync(int sessionId)
        {
            if (!int.TryParse(ClaimsHelpers.GetUserId(User), out var userId)) return null;
            return await _context.SessionParticipants
                .CurrentSeats(userId)
                .FirstOrDefaultAsync(sp => sp.Session_Id == sessionId);
        }

        private static TableBillChoiceDto Choice(Billing bill, int peopleCount) => new()
        {
            BillId = bill.Bill_Id,
            BillName = bill.Bill_Name,
            TotalCount = bill.Total_Count,
            Status = bill.Status.ToString(),
            RequestedAt = bill.Requested_At,
            PeopleCount = peopleCount,
        };

    }
}