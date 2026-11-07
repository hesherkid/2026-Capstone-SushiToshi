using back_end.Configurations;
using Microsoft.Extensions.Options;

namespace back_end.Services
{
    // Runs GuestCleanup once a day at GuestCleanup:RunAtHourUtc
    public class GuestCleanupWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly GuestCleanupSettings _settings;
        private readonly ILogger<GuestCleanupWorker> _logger;

        public GuestCleanupWorker(IServiceScopeFactory scopes, IOptions<GuestCleanupSettings> settings, ILogger<GuestCleanupWorker> logger)
        {
            _scopes = scopes;
            _settings = settings.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                if (_settings.RunOnStartup)
                {
                    // Give startup (migrations, seeding) time to finish first
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    await RunOnceAsync(stoppingToken);
                }

                while (!stoppingToken.IsCancellationRequested)
                {
                    var delay = DelayUntilNextRun(DateTime.UtcNow, _settings.RunAtHourUtc);
                    _logger.LogInformation("Next guest cleanup in {Delay}", delay);
                    await Task.Delay(delay, stoppingToken);
                    await RunOnceAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // App is shutting down
            }
        }

        // Time from now until the next RunAtHourUtc:00, today or tomorrow
        public static TimeSpan DelayUntilNextRun(DateTime utcNow, int runAtHourUtc)
        {
            var next = utcNow.Date.AddHours(runAtHourUtc);
            if (next <= utcNow) next = next.AddDays(1);
            return next - utcNow;
        }

        private async Task RunOnceAsync(CancellationToken ct)
        {
            try
            {
                // ApplicationDbContext is scoped, and a BackgroundService lives for the whole app
                using var scope = _scopes.CreateScope();
                var cleanup = scope.ServiceProvider.GetRequiredService<GuestCleanup>();
                await cleanup.RunAsync(DateTime.UtcNow, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A failed night shouldn't stop tomorrow's run or crash the app
                _logger.LogError(ex, "Guest cleanup failed");
            }
        }
    }
}
