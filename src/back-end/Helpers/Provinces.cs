namespace back_end.Helpers;

/// <summary>
/// Canadian province and territory codes, as used by canada-holidays.ca.
/// </summary>
public static class Provinces
{
    public static readonly IReadOnlyList<string> Codes = new[]
    {
        "AB", "BC", "MB", "NB", "NL", "NS", "NT", "NU", "ON", "PE", "QC", "SK", "YT"
    };

    /// <summary>
    /// " ab" becomes "AB". Throws for anything that isn't a province or territory.
    /// </summary>
    public static string Normalize(string? code)
    {
        var normalized = code?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!Codes.Contains(normalized))
        {
            throw new ArgumentException($"'{code}' is not a Canadian province or territory code.", nameof(code));
        }
        return normalized;
    }
}
