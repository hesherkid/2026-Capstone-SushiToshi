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

    public sealed record ResolvedDateRange(
        AnalyticsRangePreset Preset,
        DateOnly FromLocal,
        DateOnly ToLocal,
        DateTime StartUtc,
        DateTime EndUtc,
        string TimeZoneId);

    public static class AnalyticsDateRange
    {
        public const string DefaultTimeZoneId = "America/Edmonton";

        public static readonly TimeZoneInfo RestaurantTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZoneId);

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

        private static DateTime LocalMidnightToUtc(DateOnly localDate, TimeZoneInfo timeZone) =>
                    TimeZoneInfo.ConvertTimeToUtc(
                        localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                        timeZone);
    }
}