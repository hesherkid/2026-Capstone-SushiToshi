using System.ComponentModel.DataAnnotations;

namespace back_end.Configurations;

// Nightly removal of guest accounts that never ordered (see GuestCleanup)
public sealed class GuestCleanupSettings
{
    public const string SectionName = "GuestCleanup";

    public bool Enabled { get; set; } = true;

    // Hour of day the cleanup runs, in UTC. 10 = 3 AM MST / 4 AM MDT
    [Range(0, 23)]
    public int RunAtHourUtc { get; set; } = 10;

    // Only guests older than this are removed. Must be longer than the guest token
    // (Jwt:GuestExpiresInMinutes, default 6 hours) so a guest still at the table is never removed
    [Range(12, 720)]
    public int MinAgeHours { get; set; } = 24;

    // Dev only: also run once a few seconds after startup, for testing by hand
    public bool RunOnStartup { get; set; }
}
