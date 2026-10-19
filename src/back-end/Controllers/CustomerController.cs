using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.DTO.UserDTOs;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Helpers;

namespace back_end.Controllers
{
    // Admin management of customer accounts. Customers sign themselves up, so there's no create.
    [ApiController]
    [Route("api/customers")]
    [Authorize(Roles = "Admin")]
    public class CustomerController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CustomerController> _logger;

        public CustomerController(ApplicationDbContext context, ILogger<CustomerController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: /api/customers?skip=0&limit=100
        // List customer accounts, newest first. Deleted accounts are left out.
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserResponseDTO>>> ListCustomers(
            [FromQuery] int skip = 0,
            [FromQuery] int limit = 100)
        {
            if (skip < 0) return BadRequest("skip must be >= 0");
            if (limit < 1 || limit > 100) return BadRequest("limit must be between 1 and 100");

            var users = await _context.Users.AsNoTracking()
                .Where(u => u.Role == UserRoles.Customer && u.Status != UserStatus.Deleted)
                // Throwaway "Continue as Guest" accounts aren't real customers
                .Where(u => !u.Email.EndsWith("@" + GuestAccounts.EmailDomain))
                .OrderByDescending(u => u.Created_at)
                .Skip(skip)
                .Take(limit)
                .Select(u => new UserResponseDTO
                {
                    User_id = u.User_id,
                    Email = u.Email,
                    First_name = u.First_name,
                    Last_name = u.Last_name,
                    Role = u.Role,
                    Status = u.Status,
                    Created_at = u.Created_at,
                    Last_Interaction_at = u.Last_Interaction_at,
                    Is_email_confirmed = u.Is_email_confirmed
                })
                .ToListAsync();

            return Ok(users);
        }

        // PUT: /api/customers/5/status
        // Deactivate or reactivate a customer. Inactive accounts can't log in.
        [HttpPut("{user_id:int}/status")]
        public async Task<ActionResult<UserResponseDTO>> UpdateCustomerStatus(
            int user_id,
            [FromBody] AccountStatusUpdateDTO request)
        {
            if (request.Status != UserStatus.Active && request.Status != UserStatus.Inactive)
                return BadRequest("Status must be Active or Inactive");

            var user = await FindCustomer(user_id);
            if (user == null) return NotFound("Customer not found");

            user.Status = request.Status;
            await _context.SaveChangesAsync();
            return Ok(UserResponseDTO.From(user));
        }

        // DELETE: /api/customers/5
        // Orders, session history and service requests still reference the user, so the row is kept
        // but anonymised: personal details and sign-in methods are removed and it's marked Deleted.
        [HttpDelete("{user_id:int}")]
        public async Task<IActionResult> DeleteCustomer(int user_id)
        {
            var user = await FindCustomer(user_id);
            if (user == null) return NotFound("Customer not found");

            user.Email = $"deleted-{user.User_id}@deleted.invalid";
            user.First_name = "Deleted";
            user.Last_name = "Customer";
            user.Password_hash = null;
            user.GoogleSocial = null;
            user.Is_email_confirmed = false;
            user.Location_id = null;
            user.Status = UserStatus.Deleted;

            // Outstanding email verification / password reset links stop working
            var tokens = _context.AuthActionTokens.Where(t => t.UserId == user_id);
            _context.AuthActionTokens.RemoveRange(tokens);

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // Customer accounts only, excluding ones already deleted
        private Task<User?> FindCustomer(int user_id) =>
            _context.Users.FirstOrDefaultAsync(u =>
                u.User_id == user_id &&
                u.Role == UserRoles.Customer &&
                u.Status != UserStatus.Deleted);
    }
}
