using System.ComponentModel.DataAnnotations;

namespace back_end.Configurations;

public sealed class HolidayApiSettings
{
    public const string SectionName = "HolidayApi";

    // Must end with "/" so relative paths like "provinces/AB" are appended, not replaced
    [Required, Url]
    public string BaseUrl { get; set; } = "";

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 10;

    // Also load holidays a province lists as optional (e.g. Boxing Day in Alberta)
    public bool IncludeOptional { get; set; }

    // Province picked when the admin Holidays page opens
    [Required, RegularExpression("^(AB|BC|MB|NB|NL|NS|NT|NU|ON|PE|QC|SK|YT)$")]
    public string DefaultProvince { get; set; } = "";

    // Years the admin can load: this year minus YearsBack up to this year plus YearsAhead
    [Range(0, 5)]
    public int YearsBack { get; set; } = 1;

    [Range(0, 5)]
    public int YearsAhead { get; set; } = 2;
}
