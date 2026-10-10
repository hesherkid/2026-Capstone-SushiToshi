using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using back_end.domain.Entities;
using back_end.domain.DbContexts;
using back_end.domain.enums;

namespace back_end.domain.Seeders
{
    /// <summary>
    /// Seeder for the service_request table with initial data.
    /// </summary>
    public class ServiceRequestSeeder : ISeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ServiceRequestSeeder> _logger;
        private Dictionary<int, (string DisplayName, string GivenName, string Surname)> _userSeedData;

        public ServiceRequestSeeder(ApplicationDbContext context, ILogger<ServiceRequestSeeder> logger)
        {
            _context = context;
            _logger = logger;
        }

        public void SetUserSeedData(Dictionary<int, (string DisplayName, string GivenName, string Surname)> userSeedData)
        {
            _userSeedData = userSeedData;
        }

        public void Seed()
        {
            // Fetch some existing dining sessions, tables, and users to relate to
            var diningSession = _context.DiningSessions.FirstOrDefault();
            var table = _context.Tables.FirstOrDefault();
            var userRequester = _context.Users.FirstOrDefault(u => u.Role == UserRoles.Customer);
            var userClaimer = _context.Users.FirstOrDefault(u => u.Role != UserRoles.Customer);

            if (diningSession == null || table == null || userRequester == null)
            {
                _logger.LogWarning("Cannot seed ServiceRequest - missing DiningSession, TableEntity, or User");
                return;
            }

            var serviceRequests = new List<ServiceRequest>
            {
                new ServiceRequest
                {
                    Session_Id = diningSession.Session_Id,
                    Table_Id = table.Table_Id,
                    Request_By = userRequester.User_id,
                    Claimed_By = userClaimer?.User_id,
                    Notes = "Need extra napkins",
                    Status = ServiceRequestStatus.Pending,
                    Created_At = DateTime.UtcNow,
                    Claimed_At = null,
                    Completed_At = null
                },
                new ServiceRequest
                {
                    Session_Id = diningSession.Session_Id,
                    Table_Id = table.Table_Id,
                    Request_By = userRequester.User_id,
                    Claimed_By = userClaimer?.User_id,
                    Notes = "Requesting water refill",
                    Status = ServiceRequestStatus.Claimed,
                    Created_At = DateTime.UtcNow.AddMinutes(-15),
                    Claimed_At = DateTime.UtcNow.AddMinutes(-10),
                    Completed_At = null
                },
                new ServiceRequest
                {
                    Session_Id = diningSession.Session_Id,
                    Table_Id = table.Table_Id,
                    Request_By = userRequester.User_id,
                    Claimed_By = userClaimer?.User_id,
                    Notes = "Bill requested",
                    Status = ServiceRequestStatus.Completed,
                    Created_At = DateTime.UtcNow.AddHours(-2),
                    Claimed_At = DateTime.UtcNow.AddHours(-1).AddMinutes(-30),
                    Completed_At = DateTime.UtcNow.AddHours(-1)
                }
            };

            _context.ServiceRequests.AddRange(serviceRequests);
            // _context.SaveChanges();

            _logger.LogInformation($"Added {serviceRequests.Count} service requests");
        }
    }
}