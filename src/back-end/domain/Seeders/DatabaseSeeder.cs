using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.domain.Entities;
using back_end.domain.DbContexts;
using System.IO;
using System.Collections.Generic;

namespace back_end.domain.Seeders
{
  /// <summary>
  /// Main database seeder orchestrator that coordinates all seeding operations.
  /// Handles seeding all tables with initial data in the correct dependency order.
  /// </summary>
  public class DatabaseSeeder
  {
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DatabaseSeeder> _logger;

    private readonly CategorySeeder _categorySeeder;
    private readonly TagSeeder _tagSeeder;
    private readonly MenuSeeder _menuSeeder;
    private readonly MenuItemSeeder _menuItemSeeder;
    private readonly TableSeeder _tableSeeder;
    private readonly TableGroupSeeder _tableGroupSeeder;
    private readonly LocationSeeder _locationSeeder;
    private readonly UserSeeder _userSeeder;
    private readonly DiningSessionSeeder _diningSessionSeeder;
    private readonly SessionParticipantSeeder _sessionParticipantSeeder;
    private readonly BillSeeder _billSeeder;
    private readonly SessionOrderSeeder _sessionOrderSeeder;
    private readonly OrderItemSeeder _orderItemSeeder;
    private readonly ServiceRequestSeeder _serviceRequestSeeder;
    private readonly MenuItemViewSeeder _menuItemViewSeeder;

    private readonly Dictionary<string, (string DisplayName, string GivenName, string Surname)> _userSeedData;
    private Dictionary<int, (string DisplayName, string GivenName, string Surname)> _userIdSeedData;

    public DatabaseSeeder(
        ApplicationDbContext context,
        ILogger<DatabaseSeeder> logger,
        CategorySeeder categorySeeder,
        TagSeeder tagSeeder,
        MenuSeeder menuSeeder,
        MenuItemSeeder menuItemSeeder,
        TableSeeder tableSeeder,
        TableGroupSeeder tableGroupSeeder,
        LocationSeeder locationSeeder,
        UserSeeder userSeeder,
        DiningSessionSeeder diningSessionSeeder,
        SessionParticipantSeeder sessionParticipantSeeder,
        BillSeeder billSeeder,
        SessionOrderSeeder sessionOrderSeeder,
        OrderItemSeeder orderItemSeeder,
        ServiceRequestSeeder serviceRequestSeeder,
        MenuItemViewSeeder menuItemViewSeeder)
    {
      _context = context;
      _logger = logger;

      _categorySeeder = categorySeeder;
      _tagSeeder = tagSeeder;
      _menuSeeder = menuSeeder;
      _menuItemSeeder = menuItemSeeder;
      _tableSeeder = tableSeeder;
      _tableGroupSeeder = tableGroupSeeder;
      _locationSeeder = locationSeeder;
      _userSeeder = userSeeder;
      _diningSessionSeeder = diningSessionSeeder;
      _sessionParticipantSeeder = sessionParticipantSeeder;
      _billSeeder = billSeeder;
      _sessionOrderSeeder = sessionOrderSeeder;
      _orderItemSeeder = orderItemSeeder;
      _serviceRequestSeeder = serviceRequestSeeder;
      _menuItemViewSeeder = menuItemViewSeeder;

      _userSeedData = LoadUserSeedData();
    }

    private Dictionary<string, (string DisplayName, string GivenName, string Surname)> LoadUserSeedData()
    {
      var csvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "domain", "Seeders", "userSeedData.csv");
      var rootPath = AppDomain.CurrentDomain.BaseDirectory;
      var userSeedMap = new Dictionary<string, (string, string, string)>();
      if (File.Exists(csvPath))
      {
        var lines = File.ReadAllLines(csvPath);
        foreach (var line in lines.Skip(1))
        {
          var cols = line.Split(',');
          if (cols.Length >= 6)
          {
            var displayName = cols[1].Trim();
            var surname = cols[2].Trim();
            var given = cols[4].Trim();
            var idStr = cols[5].Trim();

            // If given and surname are both blank, parse from displayName
            if (string.IsNullOrWhiteSpace(given) && string.IsNullOrWhiteSpace(surname))
            {
              var nameParts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
              if (nameParts.Length >= 2)
              {
                given = nameParts[0];
                surname = string.Join(" ", nameParts.Skip(1));
              }
              else
              {
                // Single name - use as both
                given = displayName;
                surname = displayName;
              }
              _logger.LogInformation($"Line {given}: Parsed '{displayName}' into GivenName='{given}', Surname='{surname}'");
            }
            // If only givenName is blank, use displayName or surname
            else if (string.IsNullOrWhiteSpace(given))
            {
              given = !string.IsNullOrWhiteSpace(surname) ? surname : displayName;
              _logger.LogInformation($"Line {line}: GivenName was blank, using '{given}'");
            }
            // If only surname is blank, use displayName or givenName
            else if (string.IsNullOrWhiteSpace(surname))
            {
              surname = !string.IsNullOrWhiteSpace(line) ? given : displayName;
              _logger.LogInformation($"Line {line}: Surname was blank, using '{surname}'");
            }

            userSeedMap[idStr] = (displayName, given, surname);
          }
          else
          {
            _logger.LogWarning($"Line {line}: Invalid format (only {cols.Length} columns)");
          }
        }
      }
      return userSeedMap;
    }

    private void CreateUserIdMapping()
    {
      // Create a simple mapping using existing users
      var users = _context.Users.Take(20).ToList(); // Get first 20 users
      _userIdSeedData = new Dictionary<int, (string DisplayName, string GivenName, string Surname)>();

      foreach (var user in users)
      {
        var displayName = $"{user.First_name} {user.Last_name}".Trim();
        _userIdSeedData[user.User_id] = (displayName, user.First_name, user.Last_name);
      }

      _logger.LogInformation($"Created user ID mapping for {_userIdSeedData.Count} users");
    }

    /// <summary>Seeds all tables with initial data.</summary>
    public async Task SeedDatabase(bool reset = false)
    {
      try
      {
        if (reset)
        {
          ClearExistingData();
        }

        // Base data in dependency order
        _locationSeeder.Seed(); _logger.LogInformation("Locations seeded successfully");
        await _context.SaveChangesAsync();
        _categorySeeder.Seed(); _logger.LogInformation("Categories seeded successfully");
        await _context.SaveChangesAsync();
        _tagSeeder.Seed(); _logger.LogInformation("Tags seeded successfully");
        await _context.SaveChangesAsync();
        _menuSeeder.Seed(); _logger.LogInformation("Menus seeded successfully");
        await _context.SaveChangesAsync();
        _menuItemSeeder.Seed(); _logger.LogInformation("Menu items and assignments seeded successfully");
        await _context.SaveChangesAsync();

        // IMPORTANT: Tables must be seeded before TableGroups
        _tableSeeder.Seed(); _logger.LogInformation("Tables seeded successfully");
        await _context.SaveChangesAsync();
        _tableGroupSeeder.Seed(); _logger.LogInformation("Table groups seeded successfully");
        await _context.SaveChangesAsync();

        // Users must be seeded before session-driven data
        _userSeeder.Seed(); _logger.LogInformation("Users seeded successfully");
        await _context.SaveChangesAsync();

        // Create user ID mapping after users are seeded
        CreateUserIdMapping();

        // Pass userSeedData to seeders that need it
        _billSeeder.SetUserSeedData(_userIdSeedData);
        _sessionParticipantSeeder.SetUserSeedData(_userIdSeedData);
        _sessionOrderSeeder.SetUserSeedData(_userIdSeedData);
        _serviceRequestSeeder.SetUserSeedData(_userIdSeedData);

        // Sessions then session-driven data
        _diningSessionSeeder.Seed(); _logger.LogInformation("Dining sessions seeded successfully");
        await _context.SaveChangesAsync();
        _sessionParticipantSeeder.Seed(); _logger.LogInformation("Session participants seeded successfully");
        await _context.SaveChangesAsync();
        _billSeeder.Seed(); _logger.LogInformation("Bills seeded successfully");
        await _context.SaveChangesAsync();
        _sessionOrderSeeder.Seed(); _logger.LogInformation("Session orders seeded successfully");
        await _context.SaveChangesAsync();
        _orderItemSeeder.Seed(); _logger.LogInformation("Order items seeded successfully");
        await _context.SaveChangesAsync();
        _menuItemViewSeeder.Seed(); _logger.LogInformation("Menu item views seeded successfully");
        await _context.SaveChangesAsync();
        _serviceRequestSeeder.Seed(); _logger.LogInformation("Service requests seeded successfully");

        await _context.SaveChangesAsync();
        _logger.LogInformation("All data seeding completed successfully");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error during seeding");
        throw;
      }
    }

    /// <summary>Verifies that all tables have been seeded correctly.</summary>
    public bool VerifySeeding()
    {
      try
      {
        var locations = _context.Locations.Count();
        var categories = _context.Categories.Count();
        var tags = _context.Tags.Count();
        var menus = _context.Menus.Count();
        var menuItems = _context.MenuItems.Count();
        var assignments = _context.MenuItemAssignments.Count();
        var tables = _context.Tables.Count();
        var tableGroups = _context.TableGroups.Count();
        var diningSessions = _context.DiningSessions.Count();
        var activeSessions = _context.DiningSessions.Count(ds => ds.Ended_At == null);
        var participants = _context.SessionParticipants.Count();
        var bills = _context.Bills.Count();
        var sessionOrders = _context.SessionOrders.Count();
        var orderItems = _context.OrderItems.Count();
        var serviceRequests = _context.ServiceRequests.Count();
        var menuItemViews = _context.MenuItemViews.Count();

        // Log counts
        _logger.LogInformation($"Locations: {locations}");
        _logger.LogInformation($"Categories: {categories}");
        _logger.LogInformation($"Tags: {tags}");
        _logger.LogInformation($"Menus: {menus}");
        _logger.LogInformation($"Menu Items: {menuItems}");
        _logger.LogInformation($"Menu Item Assignments: {assignments}");
        _logger.LogInformation($"Tables: {tables}");
        _logger.LogInformation($"Table Groups: {tableGroups}");
        _logger.LogInformation($"Dining Sessions: {diningSessions} (Active: {activeSessions})");
        _logger.LogInformation($"SessionParticipants: {participants}");
        _logger.LogInformation($"Bills: {bills}");
        _logger.LogInformation($"SessionOrders: {sessionOrders}");
        _logger.LogInformation($"OrderItems: {orderItems}");
        _logger.LogInformation($"ServiceRequests: {serviceRequests}");
        _logger.LogInformation($"MenuItemViews: {menuItemViews}");

        var verification = new List<bool>
                {
                    locations == 2,                    // Exactly 2 locations
                    categories >= 6,                   // At least 6 categories
                    tags >= 10,                        // At least 10 tags
                    menus >= 2,                        // At least 2 menus
                    menuItems >= 90,                   // At least 15 items per category
                    assignments >= menuItems * 2,      // Each item should be in both menus
                    tables == 68,                      // Exactly 68 tables (34 per location × 2 locations)
                    tableGroups == 8,                  // Exactly 8 table groups (4 per location × 2 locations)
                    diningSessions >= 100,             // At least 100 sessions (50 per location × 2 locations)
                    activeSessions == 20,               // Exactly 10 active sessions (5 per location × 2 locations)
                    menuItemViews > 0                  // Browsing behavior has data
                };

        var allValid = verification.All(v => v);
        if (!allValid)
          _logger.LogWarning("Seeding verification failed. Some counts do not meet expected values.");

        return allValid;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error during verification");
        return false;
      }
    }

    /// <summary>Clears all existing data from the database.</summary>
    private void ClearExistingData()
    {
      _logger.LogInformation("Clearing existing data...");

      // Delete in reverse dependency order
      _context.MenuItemViews.RemoveRange(_context.MenuItemViews);
      _context.ServiceRequests.RemoveRange(_context.ServiceRequests);
      _context.OrderItems.RemoveRange(_context.OrderItems);
      _context.SessionOrders.RemoveRange(_context.SessionOrders);
      _context.Bills.RemoveRange(_context.Bills);
      _context.SessionParticipants.RemoveRange(_context.SessionParticipants);
      _context.DiningSessions.RemoveRange(_context.DiningSessions);
      _context.TableGroups.RemoveRange(_context.TableGroups);
      _context.Tables.RemoveRange(_context.Tables);
      _context.MenuItemAssignments.RemoveRange(_context.MenuItemAssignments);
      _context.MenuItems.RemoveRange(_context.MenuItems);
      _context.Menus.RemoveRange(_context.Menus);
      _context.Categories.RemoveRange(_context.Categories);
      _context.Tags.RemoveRange(_context.Tags);
      _context.Locations.RemoveRange(_context.Locations);

      _context.SaveChanges();
      _logger.LogInformation("Existing data cleared");
    }
  }
}