using System.Globalization;

namespace back_end.Helpers
{
    public enum AnalyticsRangePreset
    {
        Today,
        Yesterday,
        Last7,
        Last30,
        Custom
    }

    /// <summary>
    /// A resolved reporting window.
    /// FromLocal / ToLocal are inclusive restaurant-local calendar dates (what the UI shows)
    /// StartUtc is inclusive, EndUtc is exclusive (what the database query uses)
    /// </summary>
    public sealed record ResolvedDateRange(
        AnalyticsRangePreset Preset,
        DateOnly FromLocal,
        DateOnly ToLocal,
        DateTime StartUtc,
        DateTime EndUtc,
        string TimeZoneId);

    /// <summary>
    /// Turns a range preset (or a custom from/to) into UTC query bounds based on the
    /// restaurant's local day. Pure and side-effect free so it can be unit tested by
    /// passing a fixed "now".
    /// </summary>
    public static class AnalyticsDateRange
    {
        /// <summary>Mountain Time (MST/MDT). Override with appsettings "Analytics:TimeZoneId".</summary>
        public const string DefaultTimeZoneId = "America/Edmonton";

        public const int MaxCustomRangeDays = 366;

        private const string DateFormat = "yyyy-MM-dd";

        public static bool TryResolve(
            string? range,
            string? from,
            string? to,
            DateTime utcNow,
            TimeZoneInfo timeZone,
            out ResolvedDateRange? resolved,
            out string errorKey,
            out string errorMessage)
        {
            resolved = null;
            errorKey = string.Empty;
            errorMessage = string.Empty;

            utcNow = utcNow.Kind switch
            {
                DateTimeKind.Utc => utcNow,
                DateTimeKind.Local => utcNow.ToUniversalTime(),
                _ => DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)
            };

            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone));
            var presetText = string.IsNullOrWhiteSpace(range) ? "last7" : range.Trim().ToLowerInvariant();

            AnalyticsRangePreset preset;
            DateOnly fromLocal;
            DateOnly toLocal;

            switch (presetText)
            {
                case "today":
                    preset = AnalyticsRangePreset.Today;
                    fromLocal = today;
                    toLocal = today;
                    break;

                case "yesterday":
                    preset = AnalyticsRangePreset.Yesterday;
                    fromLocal = today.AddDays(-1);
                    toLocal = today.AddDays(-1);
                    break;

                // "Last N full days" = the N complete days before today. Today is excluded.
                case "last7":
                    preset = AnalyticsRangePreset.Last7;
                    fromLocal = today.AddDays(-7);
                    toLocal = today.AddDays(-1);
                    break;

                case "last30":
                    preset = AnalyticsRangePreset.Last30;
                    fromLocal = today.AddDays(-30);
                    toLocal = today.AddDays(-1);
                    break;

                case "custom":
                    preset = AnalyticsRangePreset.Custom;

                    if (!TryParseDate(from, out fromLocal))
                    {
                        errorKey = "from";
                        errorMessage = "Enter a start date in yyyy-MM-dd format.";
                        return false;
                    }

                    if (!TryParseDate(to, out toLocal))
                    {
                        errorKey = "to";
                        errorMessage = "Enter an end date in yyyy-MM-dd format.";
                        return false;
                    }

                    if (fromLocal > toLocal)
                    {
                        errorKey = "from";
                        errorMessage = "The start date must be on or before the end date.";
                        return false;
                    }

                    if (toLocal > today)
                    {
                        errorKey = "to";
                        errorMessage = "The end date can't be in the future.";
                        return false;
                    }

                    if (toLocal.DayNumber - fromLocal.DayNumber + 1 > MaxCustomRangeDays)
                    {
                        errorKey = "to";
                        errorMessage = $"Choose a range of {MaxCustomRangeDays} days or fewer.";
                        return false;
                    }
                    break;

                default:
                    errorKey = "range";
                    errorMessage = "Range must be one of: today, yesterday, last7, last30, custom.";
                    return false;
            }

            resolved = new ResolvedDateRange(
                preset,
                fromLocal,
                toLocal,
                LocalMidnightToUtc(fromLocal, timeZone),
                LocalMidnightToUtc(toLocal.AddDays(1), timeZone),
                timeZone.Id);

            return true;
        }

        private static bool TryParseDate(string? value, out DateOnly date) =>
            DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

        private static DateTime LocalMidnightToUtc(DateOnly localDate, TimeZoneInfo timeZone)
        {
            var localMidnight = localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

            // Mountain Time changes clocks at 02:00, so midnight is always valid there.
            // This guard only matters if the configured zone shifts at midnight.
            if (timeZone.IsInvalidTime(localMidnight))
            {
                localMidnight = localMidnight.AddHours(1);
            }

            return TimeZoneInfo.ConvertTimeToUtc(localMidnight, timeZone);
        }
    }
}