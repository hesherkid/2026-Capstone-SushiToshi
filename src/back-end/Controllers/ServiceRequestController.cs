using back_end.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using Microsoft.AspNetCore.Authorization;
using back_end.DTO.ServiceRequestDTOs;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using back_end.domain.enums;

namespace back_end.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  public class ServiceRequestController : ControllerBase
  {
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ServiceRequestController> _logger;

    public ServiceRequestController(ApplicationDbContext context, ILogger<ServiceRequestController> logger)
    {
      _context = context;
      _logger = logger;
    }

    /// <summary>
    /// Creates a new service request for an active dining session.
    /// </summary>
    /// <param name="session_id">The unique identifier of the dining session</param>
    /// <param name="request_data">The service request data containing notes and other details</param>
    /// <returns>
    /// An <see cref="IActionResult"/> containing the created <see cref="ServiceRequestResponseDTO"/> object.
    /// Returns HTTP 200 (OK) with the created service request on success.
    /// Returns HTTP 400 (Bad Request) if the user is not authorized or not a participant in the session.
    /// Returns HTTP 404 (Not Found) if the session doesn't exist or has ended.
    /// Returns HTTP 500 (Internal Server Error) if an exception occurs during creation.
    /// </returns>
    /// <response code="200">Returns the newly created service request</response>
    /// <response code="400">If the user is not authorized or not a valid session participant</response>
    /// <response code="404">If the session is not found or has already ended</response>
    /// <response code="500">If an internal error occurs while creating the service request</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/servicerequest/123
    ///     {
    ///         "notes": "Need water refill"
    ///     }
    ///
    /// This endpoint requires authentication.
    /// The authenticated user must be an active participant in the specified dining session.
    /// Only active (not ended) sessions can receive new service requests.
    /// The service request is automatically created with 'Pending' status.
    /// </remarks>

    [Authorize]
    [HttpPost("{session_id}")]
    public async Task<IActionResult> CreateServiceRequest(
        int session_id,
        ServiceRequestCreateDTO request_data
    )
    {
      try
      {
        var session = await _context.DiningSessions
            .Include(ds => ds.Table)
            .Include(ds => ds.TableGroup)
              .ThenInclude(tg => tg!.Tables)
            .FirstOrDefaultAsync(ds => ds.Session_Id == session_id && ds.Ended_At == null);

        if (session is null)
        {
          return NotFound("The Session Id was not found");
        }

        if (!int.TryParse(ClaimsHelpers.GetUserId(User), out var userId))
        {
          return Unauthorized(new { message = "User ID not found in claims." });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.User_id == userId);
        if (user is null)
        {
          return BadRequest("Issue in processing your request");
        }

        var participant = await _context.SessionParticipants
            .FirstOrDefaultAsync(sp => sp.Session_Id == session_id &&
                                      sp.User_Id == user.User_id &&
                                      sp.Left_At == null);
        if (participant is null)
        {
          return BadRequest("Issue in processing your request for session");
        }

        // Determine the table ID for the service request
        int? tableId = null;
        if (session.Table_Id.HasValue)
        {
          // Session has a single table assigned
          tableId = session.Table_Id.Value;
        }
        else if (session.TableGroup is { Tables.Count: > 0 } group)
        {
          tableId = group.Tables.First().Table_Id;
        }

        if (!tableId.HasValue)
        {
          return BadRequest("Session does not have a table or table group assigned");
        }

        var serviceRequest = new ServiceRequest
        {
          Session_Id = session.Session_Id,
          Table_Id = tableId.Value,
          Request_By = user.User_id,
          Notes = request_data.Notes ?? string.Empty,
          Status = ServiceRequestStatus.Pending
        };

        _context.ServiceRequests.Add(serviceRequest);
        await _context.SaveChangesAsync();

        // Reload to get table navigation property
        await _context.Entry(serviceRequest).Reference(sr => sr.Table).LoadAsync();

        return Ok(new ServiceRequestResponseDTO
        {
          Request_Id = serviceRequest.request_id,
          Session_Id = serviceRequest.Session_Id,
          Table_Id = serviceRequest.Table_Id,
          Requested_By = serviceRequest.Request_By,
          Status = serviceRequest.Status,
          Notes = serviceRequest.Notes,
          Created_At = serviceRequest.Created_At,
          Table_Number = serviceRequest.Table.table_number,
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating service request");
        return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
      }
    }

    /// <summary>
    /// Marks a service request as completed and assigns it to the current user.
    /// </summary>
    /// <param name="request_id">The unique identifier of the service request to complete</param>
    /// <returns>
    /// An <see cref="IActionResult"/> containing the updated <see cref="ServiceRequest"/> object.
    /// Returns HTTP 200 (OK) with the completed service request on success.
    /// Returns HTTP 404 (Not Found) if the service request doesn't exist.
    /// Returns HTTP 500 (Internal Server Error) if an exception occurs during the operation.
    /// </returns>
    /// <response code="200">Returns the completed service request</response>
    /// <response code="404">If the service request is not found</response>
    /// <response code="500">If an internal error occurs while completing the service request</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/servicerequest/123/complete
    ///
    /// This endpoint requires Admin or Staff role authorization.
    /// The service request status will be updated to 'Completed' and the completion timestamp will be set.
    /// The current authenticated user will be assigned as the user who completed the request.
    /// </remarks>
    [Authorize(Roles = "Admin,Staff")]
    [HttpPost("{request_id}/complete")]
    public async Task<IActionResult> CompleteServiceRequest(int request_id)
    {
      try
      {
        var serviceRequest = await _context.ServiceRequests
            .Include(sr => sr.Table)
            .FirstOrDefaultAsync(sr => sr.request_id == request_id);

        if (serviceRequest is null)
        {
          return NotFound(new { mesage = "The service request was not found" });
        }

        if (serviceRequest is null)
        {
          return NotFound(new { message = "The service request was not found" });
        }

        if (serviceRequest.Status == ServiceRequestStatus.Completed)
        {
          return Conflict(new { message = "The service request is already completed" });
        }

        if (!int.TryParse(ClaimsHelpers.GetUserId(User), out var claimedByUserId))
        {
          return Unauthorized(new { message = "User ID not found in claims." });
        }

        var now = DateTime.UtcNow;
        serviceRequest.Claimed_By = claimedByUserId;
        serviceRequest.Claimed_At ??= now;
        serviceRequest.Completed_At = now;
        serviceRequest.Status = ServiceRequestStatus.Completed;

        await _context.SaveChangesAsync();

        return Ok(new ServiceRequestResponseDTO
        {
          Request_Id = serviceRequest.request_id,
          Session_Id = serviceRequest.Session_Id,
          Table_Id = serviceRequest.Table_Id,
          Requested_By = serviceRequest.Request_By,
          Claimed_By = serviceRequest.Claimed_By,
          Status = serviceRequest.Status,
          Notes = serviceRequest.Notes,
          Created_At = serviceRequest.Created_At,
          Claimed_At = serviceRequest.Claimed_At,
          Completed_at = serviceRequest.Completed_At,
          Table_Number = serviceRequest.Table?.table_number ?? 0
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error completing service request");
        return StatusCode(500, new { message = "An error occurred while processing your request." });
      }
    }

    /// <summary>
    /// Retrieves all pending service requests for a specific session.
    /// </summary>
    /// <param name="session_id">The unique identifier of the session</param>
    /// <returns>
    /// An <see cref="IActionResult"/> containing a collection of <see cref="ServiceRequestResponseDTO"/> objects.
    /// Returns HTTP 200 (OK) with the list of pending service requests on success.
    /// Returns HTTP 404 (Not Found) if the session doesn't exist or no pending requests are found.
    /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
    /// </returns>
    /// <response code="200">Returns the list of pending service requests for the session</response>
    /// <response code="404">If the session is not found or no pending requests exist</response>
    /// <response code="500">If an internal error occurs while retrieving service requests</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/servicerequest/by-session/123
    ///
    /// Staff and admins can read any session. Customers can read only a session they're seated in,
    /// so the bill summary can tell them a "Ready for bill" request is already waiting.
    /// Only returns service requests with 'Pending' status.
    /// </remarks>
    /// <response code="403">A customer asked about a session they aren't seated in</response>
    [Authorize]
    [HttpGet("by-session/{session_id}")]
    public async Task<IActionResult> GetSessionServiceRequest(int session_id)
    {
      try
      {
        var session = await _context.DiningSessions
            .FirstOrDefaultAsync(ds => ds.Session_Id == session_id);

        if (session is null)
        {
          return NotFound(new { message = "The session was not found" });
        }

        if (!await SessionAccess.CanViewSessionAsync(_context, User, session_id))
        {
          return Forbid();
        }

        var sessionRequestList = await _context.ServiceRequests
            .Include(sr => sr.Table)
            .Where(sr => sr.Session_Id == session_id && sr.Status == ServiceRequestStatus.Pending)
            .ToListAsync();

        var response = sessionRequestList.Select(sr => new ServiceRequestResponseDTO
        {
          Request_Id = sr.request_id,
          Session_Id = sr.Session_Id,
          Table_Id = sr.Table_Id,
          Status = sr.Status,
          Requested_By = sr.Request_By,
          Claimed_By = sr.Claimed_By,
          Notes = sr.Notes,
          Created_At = sr.Created_At,
          Table_Number = sr.Table?.table_number ?? 0
        }).ToList();

        return Ok(response);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting session service requests");
        return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
      }
    }

    /// <summary>
    /// Retrieves all service requests from the database.
    /// </summary>
    /// <returns>
    /// An <see cref="ActionResult"/> containing a collection of <see cref="ServiceRequest"/> objects.
    /// Returns HTTP 200 (OK) with the list of service requests on success.
    /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
    /// </returns>
    /// <response code="200">Returns the list of all service requests</response>
    /// <response code="500">If an internal error occurs while retrieving service requests</response>
    //GET api/servicerequest
    //Get all current service requests
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ServiceRequest>>> GetAllServiceRequest()
    {
      try
      {
        var request = await _context.ServiceRequests
            .Include(sr => sr.Table)
            .Include(sr => sr.RequestedByUser)
            .Include(sr => sr.ClaimedByUser)
            .ToListAsync();
        return Ok(request);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting service requests");
        return StatusCode(500, "Internal Server Error");
      }
    }

    //GET api/servicerequest/pending
    //Get all pending service requests
    [Authorize(Roles = "Admin,Staff")]
    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<ServiceRequestResponseDTO>>> GetAllPendingServiceRequests()
    {
      try
      {
        var pendingRequests = await _context.ServiceRequests
            .Include(sr => sr.Table)
            .Include(sr => sr.RequestedByUser)
            .Include(sr => sr.DiningSession)
            .Where(sr => sr.Status == ServiceRequestStatus.Pending)
            .OrderBy(sr => sr.Created_At)
            .ToListAsync();

        var response = pendingRequests.Select(sr => new ServiceRequestResponseDTO
        {
          Request_Id = sr.request_id,
          Session_Id = sr.Session_Id,
          Table_Id = sr.Table_Id,
          Status = sr.Status,
          Requested_By = sr.Request_By,
          Claimed_By = sr.Claimed_By,
          Notes = sr.Notes,
          Created_At = sr.Created_At,
          Table_Number = sr.Table?.table_number ?? 0
        }).ToList();

        return Ok(response);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting pending service requests");
        return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
      }
    }
  }
}