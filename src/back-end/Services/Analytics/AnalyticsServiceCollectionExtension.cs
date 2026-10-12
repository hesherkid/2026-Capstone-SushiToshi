using Microsoft.Extensions.DependencyInjection.Extensions;

namespace back_end.Services.Analytics
{
    public static class AnalyticsServiceCollectionExtensions
    {
        public static IServiceCollection AddAnalyticsServices(this IServiceCollection services)
        {
            // System clock; tests can pass a fixed TimeProvider instead.
            services.TryAddSingleton(TimeProvider.System);

            // Scoped, because they use the scoped ApplicationDbContext.
            services.AddScoped<IItemPerformanceService, ItemPerformanceService>();
            services.AddScoped<IBrowsingBehaviorService, BrowsingBehaviorService>();

            return services;
        }
    }
}