using back_end.domain.Entities;
using back_end.domain.enums;

namespace back_end.Services.Holidays
{
    // Admin management of holidays. Pricing reads approved holidays directly (see PricingService)
    public interface IHolidayService
    {
        HolidayYearRange GetAllowedYears();
        Task<IReadOnlyList<Holiday>> ListAsync(string provinceCode, int? year, HolidayStatus? status, CancellationToken ct = default);

        // Load button: fetch from the holiday service and store new dates as Pending
        Task<HolidayImportResult> ImportAsync(string provinceCode, int year, bool? includeOptional, CancellationToken ct = default);

        Task<int> SetStatusAsync(IReadOnlyCollection<int> ids, HolidayStatus status, int reviewerId, CancellationToken ct = default);
        Task<Holiday> AddManualAsync(string provinceCode, DateOnly date, string name, int reviewerId, CancellationToken ct = default);
        Task<Holiday?> UpdateAsync(int holidayId, DateOnly date, string name, CancellationToken ct = default);
        Task<bool> DeleteAsync(int holidayId, CancellationToken ct = default);
    }

    public sealed record HolidayYearRange(int From, int To);
    public sealed record HolidayImportResult(int Added, int Skipped, IReadOnlyList<Holiday> AddedHolidays);

    // A holiday already exists for that province and date (shown as 409)
    public class HolidayConflictException : Exception
    {
        public HolidayConflictException(string message) : base(message) { }
    }
}
