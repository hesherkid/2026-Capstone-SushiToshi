using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using Microsoft.AspNetCore.Mvc;
using back_end.DTO.Analytics;
using back_end.Helpers;

//TODO: once table turn over front end is complete this controller can be removed
namespace back_end.controllers
{
    [ApiController]
    [Route("api/analytics")]
    [Authorize(Roles = "Admin")]
    public class AnalyticsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AnalyticsController> _logger;

        public AnalyticsController(ApplicationDbContext context, ILogger<AnalyticsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("max-party-size")]
        public async Task<IActionResult> GetMaxPartySize()
        {
            var maxPartySize = _context.Bills.Max(b => b.Child_Count + b.Adult_Count + b.Senior_Count);
            return Ok(maxPartySize);
        }
    }
}