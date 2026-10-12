using Microsoft.Extensions.DependencyInjection.Extensions;

namespace back_end.Services.Analytics
{
    public static class AnalyticsServiceCollectionExtensions
    {
        public static IServiceCollection AddAnalyticsServices(this IServiceCollection services)
        {
            services.TryAddSingleton(TimeProvider.System);

            services.AddScoped<IItemPerformanceService, ItemPerformanceService>();
            services.AddScoped<IBrowsingBehaviorService, BrowsingBehaviorService>();
            services.AddScoped<IOrderTimingService, OrderTimingService>();
            services.AddScoped<ITableTurnoverService, TableTurnoverService>();

            return services;
        }
    }
}