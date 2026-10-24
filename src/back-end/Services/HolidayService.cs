using back_end.Configurations;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace back_end.Services.Holidays
{
    public sealed class HolidayService : IHolidayService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHolidayProvider _provider;
        private readonly HolidayApiSettings _settings;
        private readonly TimeProvider _time;
        private readonly ILogger<HolidayService> _logger;

        public HolidayService(
            ApplicationDbContext context,
            IHolidayProvider provider,
            IOptions<HolidayApiSettings> settings,
            TimeProvider time,
            ILogger<HolidayService> logger)
        {
            _context = context;
            _provider = provider;
            _settings = settings.Value;
            _time = time;
            _logger = logger;
        }

        public HolidayYearRange GetAllowedYears()
        {
            var thisYear = PricingService.ToRestaurantTime(_time.GetUtcNow().UtcDateTime).Year;
            return new HolidayYearRange(thisYear - _settings.YearsBack, thisYear + _settings.YearsAhead);
        }

        public async Task<IReadOnlyList<Holiday>> ListAsync(
            string provinceCode, int? year, HolidayStatus? status, CancellationToken ct = default)
        {
            var code = Provinces.Normalize(provinceCode);
            var query = _context.Holidays.AsNoTracking().Where(h => h.Province_Code == code);

            if (year is int y)
            {
                var (from, to) = YearBounds(y);
                query = query.Where(h => h.Date >= from && h.Date <= to);
            }
            if (status is HolidayStatus s)
            {
                query = query.Where(h => h.Status == s);
            }

            return await query.OrderBy(h => h.Date).ToListAsync(ct);
        }

        public async Task<HolidayImportResult> ImportAsync(
            string provinceCode, int year, bool? includeOptional, CancellationToken ct = default)
        {
            var code = Provinces.Normalize(provinceCode);
            var allowed = GetAllowedYears();
            if (year < allowed.From || year > allowed.To)
            {
                throw new ArgumentException($"Holidays can only be loaded for {allowed.From} to {allowed.To}.", nameof(year));
            }

            var fetched = await _provider.GetHolidaysAsync(code, year, includeOptional ?? _settings.IncludeOptional, ct);

            // Dates already stored with any status are skipped, so loading again never
            // undoes an approval or brings back a rejected holiday
            var (from, to) = YearBounds(year);
            var taken = (await _context.Holidays
                    .Where(h => h.Province_Code == code && h.Date >= from && h.Date <= to)
                    .Select(h => h.Date)
                    .ToListAsync(ct))
                .ToHashSet();

            var now = _time.GetUtcNow().UtcDateTime;
            var added = new List<Holiday>();
            foreach (var holiday in fetched)
            {
                if (!taken.Add(holiday.Date)) continue;   // already stored, or listed twice

                added.Add(new Holiday
                {
                    Province_Code = code,
                    Date = holiday.Date,
                    Observed_Date = holiday.ObservedDate,
                    Name = holiday.Name,
                    Source = HolidaySource.Imported,
                    Status = HolidayStatus.Pending,
                    Imported_At = now,
                    Created_At = now,
                });
            }

            _context.Holidays.AddRange(added);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Holiday import {Province} {Year}: {Added} added, {Skipped} already stored",
                code, year, added.Count, fetched.Count - added.Count);
            return new HolidayImportResult(added.Count, fetched.Count - added.Count, added);
        }

        public async Task<int> SetStatusAsync(
            IReadOnlyCollection<int> ids, HolidayStatus status, int reviewerId, CancellationToken ct = default)
        {
            if (status == HolidayStatus.Pending)
            {
                throw new ArgumentException("A holiday can only be approved or rejected.", nameof(status));
            }

            var holidays = await _context.Holidays.Where(h => ids.Contains(h.Holiday_Id)).ToListAsync(ct);
            var now = _time.GetUtcNow().UtcDateTime;
            foreach (var holiday in holidays)
            {
                holiday.Status = status;
                holiday.Reviewed_By = reviewerId;
                holiday.Reviewed_At = now;
            }
            await _context.SaveChangesAsync(ct);
            return holidays.Count;
        }

        public async Task<Holiday> AddManualAsync(
            string provinceCode, DateOnly date, string name, int reviewerId, CancellationToken ct = default)
        {
            var code = Provinces.Normalize(provinceCode);
            await EnsureDateFreeAsync(code, date, exceptId: null, ct);

            // Added by an admin, so it's approved straight away
            var now = _time.GetUtcNow().UtcDateTime;
            var holiday = new Holiday
            {
                Province_Code = code,
                Date = date,
                Name = CleanName(name),
                Source = HolidaySource.Manual,
                Status = HolidayStatus.Approved,
                Reviewed_By = reviewerId,
                Reviewed_At = now,
                Created_At = now,
            };
            _context.Holidays.Add(holiday);
            await _context.SaveChangesAsync(ct);
            return holiday;
        }

        public async Task<Holiday?> UpdateAsync(int holidayId, DateOnly date, string name, CancellationToken ct = default)
        {
            var holiday = await _context.Holidays.FindAsync(new object[] { holidayId }, ct);
            if (holiday == null) return null;

            await EnsureDateFreeAsync(holiday.Province_Code, date, exceptId: holidayId, ct);
            holiday.Date = date;
            holiday.Name = CleanName(name);
            await _context.SaveChangesAsync(ct);
            return holiday;
        }

        public async Task<bool> DeleteAsync(int holidayId, CancellationToken ct = default)
        {
            var holiday = await _context.Holidays.FindAsync(new object[] { holidayId }, ct);
            if (holiday == null) return false;

            _context.Holidays.Remove(holiday);
            await _context.SaveChangesAsync(ct);
            return true;
        }

        private async Task EnsureDateFreeAsync(string code, DateOnly date, int? exceptId, CancellationToken ct)
        {
            if (await _context.Holidays.AnyAsync(
                    h => h.Province_Code == code && h.Date == date && h.Holiday_Id != exceptId, ct))
            {
                throw new HolidayConflictException($"{code} already has a holiday on {date:yyyy-MM-dd}.");
            }
        }

        private static string CleanName(string name)
        {
            var trimmed = name?.Trim() ?? string.Empty;
            if (trimmed.Length is 0 or > 100)
            {
                throw new ArgumentException("Holiday name must be 1 to 100 characters.", nameof(name));
            }
            return trimmed;
        }

        private static (DateOnly From, DateOnly To) YearBounds(int year) =>
            (new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
    }
}
