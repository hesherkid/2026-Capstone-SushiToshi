using Microsoft.Extensions.DependencyInjection;

namespace back_end.domain.Seeders
{
  /// <summary>
  /// Extension methods for registering seeders with dependency injection.
  /// </summary>
  public static class ServiceCollectionExtensions
  {
    /// <summary>
    /// Registers all database seeders with the service collection.
    /// </summary>
    public static IServiceCollection AddDatabaseSeeders(this IServiceCollection services)
    {
      // Register individual seeders
      services.AddScoped<CategorySeeder>();
      services.AddScoped<TagSeeder>();
      services.AddScoped<MenuSeeder>();
      services.AddScoped<MenuItemSeeder>();
      services.AddScoped<TableSeeder>();
      services.AddScoped<DiningSessionSeeder>();
      services.AddScoped<TableGroupSeeder>();
      services.AddScoped<UserSeeder>();
      services.AddScoped<SessionParticipantSeeder>();
      services.AddScoped<BillSeeder>();
      services.AddScoped<SessionOrderSeeder>();
      services.AddScoped<OrderItemSeeder>();
      services.AddScoped<ServiceRequestSeeder>();
      services.AddScoped<MenuItemViewSeeder>();

      services.AddScoped<LocationSeeder>();

      // Register main seeder orchestrator
      services.AddScoped<DatabaseSeeder>();

      return services;
    }
  }
}