using System.Text.Json;

namespace back_end.Services.Holidays
{
    /// <summary>
    /// Loads holidays from canada-holidays.ca, e.g. GET provinces/AB?year=2026.
    /// Base URL and timeout come from the HolidayApi settings (see Program.cs).
    /// </summary>
    public sealed class CanadaHolidaysClient : IHolidayProvider
    {
        private readonly HttpClient _http;
        private readonly ILogger<CanadaHolidaysClient> _logger;

        public CanadaHolidaysClient(HttpClient http, ILogger<CanadaHolidaysClient> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task<IReadOnlyList<ExternalHoliday>> GetHolidaysAsync(
            string provinceCode, int year, bool includeOptional, CancellationToken ct = default)
        {
            // optional=true adds holidays like Boxing Day that aren't general holidays in the province
            var path = $"provinces/{Uri.EscapeDataString(provinceCode)}?year={year}"
                + (includeOptional ? "&optional=true" : "");

            try
            {
                var response = await _http.GetFromJsonAsync<ProvinceResponse>(path, ct);
                var holidays = response?.Province?.Holidays
                    ?? throw new HolidayProviderException($"No holidays returned for {provinceCode} {year}.");

                _logger.LogInformation("Loaded {Count} holidays for {Province} {Year}", holidays.Count, provinceCode, year);
                return holidays
                    .Select(h => new ExternalHoliday(h.Date, h.ObservedDate, h.NameEn))
                    .ToList();
            }
            catch (HttpRequestException ex)
            {
                throw new HolidayProviderException($"The holiday service returned an error ({ex.StatusCode}).", ex);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                throw new HolidayProviderException("The holiday service didn't respond in time.", ex);
            }
            catch (JsonException ex)
            {
                throw new HolidayProviderException("The holiday service sent a response that couldn't be read.", ex);
            }
        }

        // Only the fields we use from {"province":{"holidays":[{"date","observedDate","nameEn"}]}}
        private sealed record ProvinceResponse(ProvinceDto? Province);
        private sealed record ProvinceDto(List<HolidayDto>? Holidays);
        private sealed record HolidayDto(DateOnly Date, DateOnly? ObservedDate, string NameEn);
    }
}
