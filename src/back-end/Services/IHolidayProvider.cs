namespace back_end.Services.Holidays
{
    // Where holidays are loaded from. Only fetches; never writes to the database
    public interface IHolidayProvider
    {
        Task<IReadOnlyList<ExternalHoliday>> GetHolidaysAsync(
            string provinceCode, int year, bool includeOptional, CancellationToken ct = default);
    }

    public sealed record ExternalHoliday(DateOnly Date, DateOnly? ObservedDate, string Name);

    // The holiday service couldn't be reached or sent something unreadable (shown to admins as 502)
    public class HolidayProviderException : Exception
    {
        public HolidayProviderException(string message, Exception? inner = null) : base(message, inner) { }
    }
}
