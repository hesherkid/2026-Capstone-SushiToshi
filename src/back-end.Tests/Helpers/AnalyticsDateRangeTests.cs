using Xunit;
using FluentAssertions;
using back_end.Helpers;

namespace back_end.Tests.Helpers;

/// <summary>
/// AnalyticsDateRange turns a range preset (or custom dates) into UTC query bounds using
/// restaurant-local (Mountain Time) days. "now" is passed in, so every test is deterministic.
/// Expected UTC values: local midnight is 06:00 UTC during MDT and 07:00 UTC during MST.
/// </summary>
public class AnalyticsDateRangeTests
{
    private static readonly TimeZoneInfo MountainTime =
        TimeZoneInfo.FindSystemTimeZoneById(AnalyticsDateRange.DefaultTimeZoneId);

    // Wednesday 2026-10-07 at 12:00 MDT (18:00 UTC)
    private static readonly DateTime TestNow = new DateTime(2026, 10, 7, 18, 0, 0, DateTimeKind.Utc);

    private static DateTime Utc(int year, int month, int day, int hour) =>
        new(year, month, day, hour, 0, 0, DateTimeKind.Utc);

    private static ResolvedDateRange ResolveOk(string? range, string? from = null, string? to = null, DateTime? utcNow = null)
    {
        var ok = AnalyticsDateRange.TryResolve(range, from, to, utcNow ?? TestNow, MountainTime,
            out var resolved, out var errorKey, out var errorMessage);

        ok.Should().BeTrue($"expected a valid range but got '{errorKey}': {errorMessage}");
        resolved.Should().NotBeNull();
        return resolved!;
    }

    private static (string Key, string Message) ResolveError(string? range, string? from = null, string? to = null)
    {
        var ok = AnalyticsDateRange.TryResolve(range, from, to, TestNow, MountainTime,
            out var resolved, out var errorKey, out var errorMessage);

        ok.Should().BeFalse();
        resolved.Should().BeNull();
        return (errorKey, errorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryResolve_NoRange_DefaultsToLast7(string? range)
    {
        // Act
        var result = ResolveOk(range);

        //Assert
        result.Preset.Should().Be(AnalyticsRangePreset.Last7);
    }

    [Fact]
    public void TryResolve_Today_CoversTodayFromLocalMidnight()
    {
        // Act
        var result = ResolveOk("today");

        // Assert
        result.FromLocal.Should().Be(new DateOnly(2026, 10, 7));
        result.ToLocal.Should().Be(new DateOnly(2026, 10, 7));
        result.StartUtc.Should().Be(Utc(2026, 10, 7, 6));
        result.EndUtc.Should().Be(Utc(2026, 10, 8, 6));
    }

    [Fact]
    public void TryResolve_Yesterday_CoversOneFullLocalDay()
    {
        // Act
        var result = ResolveOk("yesterday");

        // Assert
        result.FromLocal.Should().Be(new DateOnly(2026, 10, 6));
        result.ToLocal.Should().Be(new DateOnly(2026, 10, 6));
        result.StartUtc.Should().Be(Utc(2026, 10, 6, 6));
        result.EndUtc.Should().Be(Utc(2026, 10, 7, 6));
    }

    [Fact]
    public void TryResolve_Last7_IsSevenFullDaysExcludingToday()
    {
        // Act
        var result = ResolveOk("last7");

        // Assert
        result.FromLocal.Should().Be(new DateOnly(2026, 9, 30));
        result.ToLocal.Should().Be(new DateOnly(2026, 10, 6));
        result.StartUtc.Should().Be(Utc(2026, 9, 30, 6));
        result.EndUtc.Should().Be(Utc(2026, 10, 7, 6), "the window ends at the start of today");
    }

    [Fact]
    public void TryResolve_Last30_IsThirtyFullDaysExcludingToday()
    {
        // Act
        var result = ResolveOk("last30");

        // Assert
        result.FromLocal.Should().Be(new DateOnly(2026, 9, 7));
        result.ToLocal.Should().Be(new DateOnly(2026, 10, 6));
        (result.ToLocal.DayNumber - result.FromLocal.DayNumber + 1).Should().Be(30);
        result.EndUtc.Should().Be(Utc(2026, 10, 7, 6));
    }

    [Theory]
    [InlineData("LAST7", AnalyticsRangePreset.Last7)]
    [InlineData("  Yesterday ", AnalyticsRangePreset.Yesterday)]
    [InlineData("Today", AnalyticsRangePreset.Today)]
    public void TryResolve_PresetCaseAndWhitespace_AreIgnored(string range, AnalyticsRangePreset expected)
    {
        // Act & Assert
        ResolveOk(range).Preset.Should().Be(expected);
    }

    [Fact]
    public void TryResolve_LateEveningLocalTime_StillUsesTheLocalDate()
    {
        // Arrange - 2026-10-08 05:30 UTC is 2026-10-07 23:30 MDT
        var lateEveningUtc = new DateTime(2026, 10, 8, 5, 30, 0, DateTimeKind.Utc);

        // Act
        var result = ResolveOk("today", utcNow: lateEveningUtc);

        // Assert
        result.FromLocal.Should().Be(new DateOnly(2026, 10, 7), "it is still Oct 7 in Mountain Time");
    }

    [Fact]
    public void TryResolve_UnspecifiedKindNow_IsTreatedAsUtc()
    {
        // Arrange
        var unspecified = DateTime.SpecifyKind(TestNow, DateTimeKind.Unspecified);

        // Act
        var result = ResolveOk("today", utcNow: unspecified);

        // Assert
        result.FromLocal.Should().Be(new DateOnly(2026, 10, 7));
    }

    [Fact]
    public void TryResolve_Always_EchoesTimeZoneId()
    {
        // Act & Assert
        ResolveOk("today").TimeZoneId.Should().Be(MountainTime.Id);
    }

    [Fact]
    public void TryResolve_CustomRange_IncludesBothDates()
    {
        // Act
        var result = ResolveOk("custom", "2026-10-01", "2026-10-03");

        // Assert
        result.Preset.Should().Be(AnalyticsRangePreset.Custom);
        result.FromLocal.Should().Be(new DateOnly(2026, 10, 1));
        result.ToLocal.Should().Be(new DateOnly(2026, 10, 3));
        result.StartUtc.Should().Be(Utc(2026, 10, 1, 6));
        result.EndUtc.Should().Be(Utc(2026, 10, 4, 6), "the end date is included, so the window ends the next midnight");
    }

    [Fact]
    public void TryResolve_CustomSingleDay_IsAllowed()
    {
        // Act
        var result = ResolveOk("custom", "2026-10-05", "2026-10-05");

        // Assert
        result.StartUtc.Should().Be(Utc(2026, 10, 5, 6));
        result.EndUtc.Should().Be(Utc(2026, 10, 6, 6));
    }

    [Fact]
    public void TryResolve_CustomEndingToday_IsAllowed()
    {
        // Act & Assert
        ResolveOk("custom", "2026-10-01", "2026-10-07").ToLocal.Should().Be(new DateOnly(2026, 10, 7));
    }

    [Fact]
    public void TryResolve_CustomRangeOf366Days_IsAllowed()
    {
        // Act
        var result = ResolveOk("custom", "2025-10-07", "2026-10-07");

        // Assert
        (result.ToLocal.DayNumber - result.FromLocal.DayNumber + 1).Should().Be(AnalyticsDateRange.MaxCustomRangeDays);
    }

    [Theory]
    [InlineData("weekly")]
    [InlineData("last14")]
    [InlineData("all")]
    public void TryResolve_UnknownRange_ReturnsRangeError(string range)
    {
        // Act
        var (key, message) = ResolveError(range);

        // Assert
        key.Should().Be("range");
        message.Should().Contain("today, yesterday, last7, last30, custom");
    }

    [Theory]
    [InlineData(null, "2026-10-03", "from")]
    [InlineData("10/01/2026", "2026-10-03", "from")]
    [InlineData("2026-10-01", null, "to")]
    [InlineData("2026-10-01", "2026-13-01", "to")]
    [InlineData("2026-10-01", "Oct 3", "to")]
    public void TryResolve_CustomWithMissingOrBadDate_ReturnsFieldError(string? from, string? to, string expectedKey)
    {
        // Act
        var (key, _) = ResolveError("custom", from, to);

        // Assert
        key.Should().Be(expectedKey);
    }

    [Fact]
    public void TryResolve_CustomStartAfterEnd_ReturnsFromError()
    {
        // Act
        var (key, message) = ResolveError("custom", "2026-10-05", "2026-10-01");

        // Assert
        key.Should().Be("from");
        message.Should().Contain("on or before");
    }

    [Fact]
    public void TryResolve_CustomEndInFuture_ReturnsToError()
    {
        // Act
        var (key, message) = ResolveError("custom", "2026-10-01", "2026-10-08");

        // Assert
        key.Should().Be("to");
        message.Should().Contain("future");
    }

    [Fact]
    public void TryResolve_CustomRangeOver366Days_ReturnsToError()
    {
        // Act - 367 days inclusive
        var (key, message) = ResolveError("custom", "2025-10-06", "2026-10-07");

        // Assert
        key.Should().Be("to");
        message.Should().Contain("366");
    }

    [Fact]
    public void TryResolve_PresetIgnoresFromAndTo()
    {
        // Act - from/to are only read for "custom"
        var result = ResolveOk("yesterday", "not-a-date", "also-not-a-date");

        // Assert
        result.Preset.Should().Be(AnalyticsRangePreset.Yesterday);
    }

}