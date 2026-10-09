using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.DTO.UserDTOs;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.StaffDTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace back_end.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StaffController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<StaffController> _logger;

        public StaffController(ApplicationDbContext context, ILogger<StaffController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // POST: /api/staff?role=Admin
        // Create a new staff or admin user by Admin only.
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<UserResponseDTO>> CreateStaff(
            [FromBody] CreateStaffDTO user_data
        // [FromQuery] UserRoles role
        )
        {
            Locations location = new();
            UserRoles? role = user_data.Role;

            if (role != UserRoles.Staff && role != UserRoles.Admin)
                return BadRequest("Role must be either staff or admin");

            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            string? email = user_data.Email?.Trim().ToLowerInvariant();
            bool emailExists = await _context.Users.AnyAsync(u => u.Email.Equals(email, StringComparison.CurrentCultureIgnoreCase));

            if (string.IsNullOrWhiteSpace(user_data.First_name) || string.IsNullOrWhiteSpace(user_data.Last_name))
                return BadRequest("First name and last name are required");

            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Email is required");
            else if (emailExists)
                return Conflict("Email already registered");

            if (string.IsNullOrWhiteSpace(user_data.Password))
                return BadRequest("Password is required");

            DateTime now = DateTime.UtcNow;

            User entity = new()
            {
                Email = email,
                Password_hash = BCrypt.Net.BCrypt.HashPassword(user_data.Password, workFactor: 12),
                First_name = user_data.First_name.Trim(),
                Last_name = user_data.Last_name.Trim(),
                Role = (UserRoles)role,
                Status = user_data.Status,
                Is_email_confirmed = true,
                Created_at = now,
                Last_Interaction_at = now,
                Location_id = user_data.Location_Id
            };

            _context.Users.Add(entity);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetStaff),
                new { user_id = entity.User_id },
                ToResponse(entity)
            );
        }

        // GET: /api/staff?role=Staff&skip=0&limit=100
        // List all staff and/or admin users with pagination.
        [HttpGet]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<ActionResult<IEnumerable<UserResponseDTO>>> ListStaff(
            [FromQuery] UserRoles? role = null,
            [FromQuery] int skip = 0,
            [FromQuery] int limit = 100)
        {
            if (skip < 0) return BadRequest("skip must be >= 0");
            if (limit < 1 || limit > 100) return BadRequest("limit must be between 1 and 100");

            var q = _context.Users.AsNoTracking()
                .Where(u => u.Role == UserRoles.Staff || u.Role == UserRoles.Admin);

            if (role.HasValue)
            {
                if (role != UserRoles.Staff && role != UserRoles.Admin)
                    return BadRequest("Role must be either staff or admin");
                q = q.Where(u => u.Role == role);
            }

            var users = await q
                .OrderByDescending(u => u.Created_at)
                .Skip(skip)
                .Take(limit)
                .Select(u => new StaffResponseDTO
                {
                    Created_At = u.Created_at,
                    Email = u.Email,
                    IsEmailConfirmed = u.Is_email_confirmed,
                    LastTransactionAt = u.Last_Interaction_at ?? u.Created_at,
                    LastName = u.Last_name,
                    FirstName = u.First_name,
                    Role = u.Role,
                    Status = u.Status,
                    User_Id = u.User_id,
                    Location = u.PrimaryLocation != null ? u.PrimaryLocation.Name : "Not Assigned"
                })
                .ToListAsync();


            return Ok(users);
        }

        [HttpGet("customers")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<ActionResult<IEnumerable<UserResponseDTO>>> ListCustomers(
            [FromQuery] int skip = 0,
            [FromQuery] int limit = 100)
        {
            if (skip < 0) return BadRequest("skip must be >= 0");
            if (limit < 1 || limit > 100) return BadRequest("limit must be between 1 and 100");

            var q = _context.Users.AsNoTracking()
                .Where(u => u.Role == UserRoles.Customer);

            var users = await q
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

        // GET: /api/staff/5
        // Get details of a specific staff/admin user.
        [HttpGet("{user_id:int}")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<ActionResult<UserResponseDTO>> GetStaff(int user_id)
        {
            var u = await _context.Users.AsNoTracking()
                .Where(x => x.User_id == user_id &&
                            (x.Role == UserRoles.Staff || x.Role == UserRoles.Admin))
                .FirstOrDefaultAsync();

            if (u == null) return NotFound("Staff user not found");
            return Ok(ToResponse(u));
        }

        // PUT: /api/staff/5
        // Update a staff/admin user's details.
        [HttpPut("{user_id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<UserResponseDTO>> UpdateStaff(int user_id, [FromBody] StaffMemberUpdateDTO user_data)
        {
            // var user = await _context.Users
            //     .Where(x => x.User_id == user_id &&
            //                 (x.Role == UserRoles.Staff || x.Role == UserRoles.Admin))
            //     .FirstOrDefaultAsync();
            var currentUserId = User.FindFirst(ClaimTypes.Role)?.Value;

            var user = await _context.Users
                .Where(x => x.User_id == user_id)
                .FirstOrDefaultAsync();

            if (user == null) return NotFound("Staff user not found");

            if (!string.IsNullOrWhiteSpace(user_data.Email))
            {
                var newEmail = user_data.Email.Trim().ToLowerInvariant();
                if (!newEmail.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
                {
                    var exists = await _context.Users.AnyAsync(u => u.Email.ToLower() == newEmail && u.User_id != user_id);
                    if (exists) return Conflict("Email already registered");
                    user.Email = newEmail;
                }
            }

            if (!string.IsNullOrWhiteSpace(user_data.FirstName)) user.First_name = user_data.FirstName.Trim();
            if (!string.IsNullOrWhiteSpace(user_data.LastName)) user.Last_name = user_data.LastName.Trim();


            if (user_data.Role != UserRoles.Staff && user_data.Role != UserRoles.Admin)
            {
                return BadRequest("Role must be either staff or admin");
            }

            user.Role = user_data.Role;
            user.Status = user_data.Status;

            await _context.SaveChangesAsync();
            return Ok(ToResponse(user));
        }

        // POST: /api/staff/5/change-password
        // Change a staff user's password by Admin only.
        [HttpPost("{user_id:int}/change-password")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<UserResponseDTO>> ChangeStaffPassword(int user_id, [FromBody] StaffMemberChangePasswordDTO password_data)
        {
            var user = await _context.Users
                .Where(x => x.User_id == user_id &&
                            (x.Role == UserRoles.Staff || x.Role == UserRoles.Admin))
                .FirstOrDefaultAsync();

            if (user == null) return NotFound("Staff user not found");

            var ok = BCrypt.Net.BCrypt.Verify(password_data.Current_password, user.Password_hash);
            if (!ok) return BadRequest("Incorrect current password");

            user.Password_hash = BCrypt.Net.BCrypt.HashPassword(password_data.New_password, workFactor: 12);
            user.Last_Interaction_at = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(ToResponse(user));
        }

        // POST: /api/staff/5/reset-password
        // Reset a staff user's password by Admin only.
        [HttpPost("{user_id:int}/reset-password")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<UserResponseDTO>> ResetStaffPassword(int user_id, [FromBody] ResetPasswordRequestDTO request)
        {
            var user = await _context.Users
                .Where(x => x.User_id == user_id &&
                            (x.Role == UserRoles.Staff || x.Role == UserRoles.Admin))
                .FirstOrDefaultAsync();

            if (user == null) return NotFound("Staff user not found");

            user.Password_hash = BCrypt.Net.BCrypt.HashPassword(request.New_password, workFactor: 12);
            user.Last_Interaction_at = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(ToResponse(user));
        }


        [HttpPut("update-user-location/{location_id}")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> UpdateStaffLocation(
            int location_id
        )
        {
            try
            {
                string? nameIdentifier = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int userId = 0;
                if (string.IsNullOrEmpty(nameIdentifier) || !int.TryParse(nameIdentifier, out userId))
                {
                    return BadRequest("Issue in processing a User location request.");
                }
                var user = await _context.Users.FirstOrDefaultAsync(u => u.User_id == userId);
                if (user is null)
                {
                    return BadRequest("Issue in Processing a user request for location change");
                }
                user.Location_id = location_id;
                _context.Update(user);
                await _context.SaveChangesAsync();
                return Ok("User Location was updated. ");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in updating user locations. ");
                return BadRequest("Unhandled Exception occurred in processing request.");
            }


        }


        [HttpGet("get-initial-staff-location")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> GetStaffStartingLocation(
        )
        {
            try
            {
                string? nameIdentifier = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int userId = 0;
                if (string.IsNullOrEmpty(nameIdentifier) || !int.TryParse(nameIdentifier, out userId))
                {
                    return BadRequest("Issue in processing a User location request.");
                }
                var user = await _context.Users.Include(u => u.PrimaryLocation).FirstOrDefaultAsync(u => u.User_id == userId);
                if (user is null)
                {
                    return BadRequest("Issue in Processing a user request for location change");
                }

                return Ok(user.PrimaryLocation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in updating user locations. ");
                return BadRequest("Unhandled Exception occurred in processing request.");
            }


        }

        private static UserResponseDTO ToResponse(User u) => new UserResponseDTO
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
        };
    }
}