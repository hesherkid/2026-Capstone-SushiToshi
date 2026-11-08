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
using back_end.Helpers;
using back_end.Services;

namespace back_end.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StaffController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<StaffController> _logger;
        private readonly ILocationAccessService _locationAccess;

        public StaffController(ApplicationDbContext context, ILogger<StaffController> logger, ILocationAccessService locationAccess)
        {
            _context = context;
            _logger = logger;
            _locationAccess = locationAccess;
        }

        // POST: /api/staff?role=Admin
        // Create a new staff or admin user by Admin only.
        [HttpPost]
        [Authorize(Policy = "adminOnly")]
        public async Task<ActionResult<UserResponseDTO>> CreateStaff(
            [FromBody] CreateStaffDTO userData
        )
        {
            Locations location = new();
            UserRoles? role = userData.Role;

            if (role != UserRoles.Staff && role != UserRoles.Admin)
                return BadRequest("Role must be either staff or admin");

            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            string? email = userData.Email?.Trim().ToLowerInvariant();
            bool emailExists = email != null && await _context.Users.AnyAsync(u => u.Email.ToLower() == email);

            if (string.IsNullOrWhiteSpace(userData.First_name) || string.IsNullOrWhiteSpace(userData.Last_name))
                return BadRequest("First name and last name are required");

            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Email is required");
            else if (emailExists)
                return Conflict("Email already registered");

            if (string.IsNullOrWhiteSpace(userData.Password))
                return BadRequest("Password is required");

            if (userData.Location_Id is int locationId
                && !await _context.Locations.AnyAsync(l => l.Location_Id == locationId))
                return BadRequest("Location not found");

            DateTime now = DateTime.UtcNow;

            User entity = new()
            {
                Email = email,
                Password_hash = BCrypt.Net.BCrypt.HashPassword(userData.Password, workFactor: 12),
                First_name = userData.First_name.Trim(),
                Last_name = userData.Last_name.Trim(),
                Role = (UserRoles)role,
                Status = userData.Status,
                Is_email_confirmed = true,
                Created_at = now,
                Last_Interaction_at = now,
                Location_id = userData.Location_Id
            };

            // Staff can switch only between assigned locations, so assign the one they start at too
            if (entity.Role == UserRoles.Staff)
            {
                var locationIds = userData.Location_Ids ?? [];
                if (userData.Location_Id is int startingLocationId) locationIds = [.. locationIds, startingLocationId];

                var error = await AssignLocationsAsync(entity, locationIds, userData.Location_Id);
                if (error != null) return BadRequest(error);
            }

            _context.Users.Add(entity);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetStaff),
                new { userId = entity.User_id },
                ToResponse(entity)
            );
        }

        // GET: /api/staff?role=Staff&skip=0&limit=100
        // List all staff and/or admin users with pagination.
        [HttpGet]
        [Authorize(Policy = "staffOnly")]
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
                    Location = u.PrimaryLocation != null ? u.PrimaryLocation.Name : "Not Assigned",
                    Location_Id = u.Location_id,
                    Location_Ids = u.AssignedLocations.OrderBy(a => a.Location_Id).Select(a => a.Location_Id).ToList(),
                    Locations = u.AssignedLocations.OrderBy(a => a.Location_Id).Select(a => a.Location.Name).ToList()
                })
                .ToListAsync();


            return Ok(users);
        }

        // GET: /api/staff/5
        // Get details of a specific staff/admin user.
        [HttpGet("{userId:int}")]
        [Authorize(Policy = "staffOnly")]
        public async Task<ActionResult<UserResponseDTO>> GetStaff(int userId)
        {
            var u = await _context.Users.AsNoTracking()
                .Where(x => x.User_id == userId &&
                            (x.Role == UserRoles.Staff || x.Role == UserRoles.Admin))
                .FirstOrDefaultAsync();

            if (u == null) return NotFound("Staff user not found");
            return Ok(ToResponse(u));
        }

        // PUT: /api/staff/5
        // Update a staff/admin user's details.
        [HttpPut("{userId:int}")]
        [Authorize(Policy = "adminOnly")]
        public async Task<ActionResult<UserResponseDTO>> UpdateStaff(int userId, [FromBody] StaffMemberUpdateDTO userData)
        {
            // Staff and admin accounts only, so this can't turn a customer into staff
            var user = await FindStaff(userId);

            if (user == null) return NotFound("Staff user not found");

            // Stops an admin locking themselves out by demoting or deactivating their own account
            bool isSelf = ClaimsHelpers.GetUserId(User) == userId.ToString();
            if (isSelf && (userData.Role != user.Role || userData.Status != user.Status))
            {
                return BadRequest("You can't change the role or status of your own account");
            }

            if (!string.IsNullOrWhiteSpace(userData.Email))
            {
                var newEmail = userData.Email.Trim().ToLowerInvariant();
                if (!newEmail.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
                {
                    var exists = await _context.Users.AnyAsync(u => u.Email.ToLower() == newEmail && u.User_id != userId);
                    if (exists) return Conflict("Email already registered");
                    user.Email = newEmail;
                }
            }

            if (!string.IsNullOrWhiteSpace(userData.FirstName)) user.First_name = userData.FirstName.Trim();
            if (!string.IsNullOrWhiteSpace(userData.LastName)) user.Last_name = userData.LastName.Trim();


            if (userData.Role != UserRoles.Staff && userData.Role != UserRoles.Admin)
            {
                return BadRequest("Role must be either staff or admin");
            }

            user.Role = userData.Role;
            user.Status = userData.Status;

            // Admins can work at every location, so only staff have assigned locations
            if (user.Role == UserRoles.Staff && userData.Location_Ids != null)
            {
                var error = await AssignLocationsAsync(user, userData.Location_Ids);
                if (error != null) return BadRequest(error);
            }

            await _context.SaveChangesAsync();
            return Ok(ToResponse(user));
        }

        // PUT: /api/staff/5/status
        // Deactivate or reactivate a staff/admin account by Admin only. Inactive accounts can't log in.
        [HttpPut("{userId:int}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<UserResponseDTO>> UpdateStaffStatus(int userId, [FromBody] AccountStatusUpdateDTO request)
        {
            if (request.Status != UserStatus.Active && request.Status != UserStatus.Inactive)
                return BadRequest("Status must be Active or Inactive");

            // Stops an admin locking themselves out
            if (ClaimsHelpers.GetUserId(User) == userId.ToString())
                return BadRequest("You can't change the status of your own account");

            var user = await FindStaff(userId);
            if (user == null) return NotFound("Staff user not found");

            user.Status = request.Status;
            await _context.SaveChangesAsync();
            return Ok(ToResponse(user));
        }

        private Task<User?> FindStaff(int userId) =>
            _context.Users
                .Include(u => u.AssignedLocations)
                .FirstOrDefaultAsync(u =>
                    u.User_id == userId &&
                    (u.Role == UserRoles.Staff || u.Role == UserRoles.Admin));

        // Replaces a staff member's assigned locations (AssignedLocations must be loaded).
        // They start at startAt if it's one of them; otherwise, if their current location is no
        // longer assigned, they move to another assigned location, or to none if none are left.
        // Returns an error message when a location doesn't exist
        private async Task<string?> AssignLocationsAsync(User user, IEnumerable<int> locationIds, int? startAt = null)
        {
            var ids = locationIds.Distinct().ToList();
            if (await _context.Locations.CountAsync(l => ids.Contains(l.Location_Id)) != ids.Count)
                return "Location not found";

            foreach (var removed in user.AssignedLocations.Where(a => !ids.Contains(a.Location_Id)).ToList())
            {
                user.AssignedLocations.Remove(removed);
                _context.StaffLocations.Remove(removed);
            }
            foreach (var added in ids.Where(id => user.AssignedLocations.All(a => a.Location_Id != id)))
            {
                user.AssignedLocations.Add(new StaffLocation { Location_Id = added });
            }

            if (startAt is int start && ids.Contains(start))
                user.Location_id = start;
            else if (user.Location_id is not int current || !ids.Contains(current))
                user.Location_id = ids.Count > 0 ? ids[0] : null;

            return null;
        }

        // POST: /api/staff/5/change-password
        // Change a staff user's password by Admin only.
        [HttpPost("{userId:int}/change-password")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<UserResponseDTO>> ChangeStaffPassword(int userId, [FromBody] StaffMemberChangePasswordDTO passwordData)
        {
            var user = await _context.Users
                .Where(x => x.User_id == userId &&
                            (x.Role == UserRoles.Staff || x.Role == UserRoles.Admin))
                .FirstOrDefaultAsync();

            if (user == null) return NotFound("Staff user not found");

            var ok = BCrypt.Net.BCrypt.Verify(passwordData.Current_password, user.Password_hash);
            if (!ok) return BadRequest("Incorrect current password");

            user.Password_hash = BCrypt.Net.BCrypt.HashPassword(passwordData.New_password, workFactor: 12);
            user.Last_Interaction_at = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(ToResponse(user));
        }

        // POST: /api/staff/5/reset-password
        // Reset a staff user's password by Admin only.
        [HttpPost("{userId:int}/reset-password")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<UserResponseDTO>> ResetStaffPassword(int userId, [FromBody] ResetPasswordRequestDTO request)
        {
            var user = await _context.Users
                .Where(x => x.User_id == userId &&
                            (x.Role == UserRoles.Staff || x.Role == UserRoles.Admin))
                .FirstOrDefaultAsync();

            if (user == null) return NotFound("Staff user not found");

            user.Password_hash = BCrypt.Net.BCrypt.HashPassword(request.New_password, workFactor: 12);
            user.Last_Interaction_at = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(ToResponse(user));
        }


        [HttpPut("update-user-location/{locationId}")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> UpdateStaffLocation(
            int locationId
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
                if (!await _context.Locations.AnyAsync(l => l.Location_Id == locationId))
                {
                    return NotFound("Location not found");
                }
                // Staff can switch only between the locations an admin assigned them; admins anywhere
                if (!await _locationAccess.CanSwitchToLocationAsync(User, locationId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, ILocationAccessService.NotAssignedMessage);
                }
                user.Location_id = locationId;
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

        // GET: /api/staff/my-locations
        // The signed-in user's current location, and the locations they can switch between
        [HttpGet("my-locations")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<ActionResult<MyLocationsDTO>> GetMyLocations()
        {
            if (!int.TryParse(ClaimsHelpers.GetUserId(User), out var userId))
                return BadRequest("Issue in processing a User location request.");

            var user = await _context.Users.AsNoTracking()
                .Include(u => u.PrimaryLocation)
                .FirstOrDefaultAsync(u => u.User_id == userId);
            if (user is null) return NotFound("User not found");

            bool allLocations = !_locationAccess.IsLimitedToOneLocation(User);
            var locations = allLocations
                ? await _context.Locations
                    .OrderBy(l => l.Location_Id)
                    .Select(l => new LocationOptionDTO { Location_Id = l.Location_Id, Name = l.Name })
                    .ToListAsync()
                : await _context.StaffLocations
                    .Where(sl => sl.User_Id == userId)
                    .OrderBy(sl => sl.Location_Id)
                    .Select(sl => new LocationOptionDTO { Location_Id = sl.Location_Id, Name = sl.Location.Name })
                    .ToListAsync();

            return Ok(new MyLocationsDTO
            {
                Current_Location = user.PrimaryLocation is null
                    ? null
                    : new LocationOptionDTO { Location_Id = user.PrimaryLocation.Location_Id, Name = user.PrimaryLocation.Name },
                Locations = locations,
                All_Locations = allLocations,
            });
        }

        private static UserResponseDTO ToResponse(User u) => UserResponseDTO.From(u);
    }
}