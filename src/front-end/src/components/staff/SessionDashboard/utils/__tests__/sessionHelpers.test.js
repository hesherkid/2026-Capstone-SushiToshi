import {
  formatTime,
  formatDate,
  formatDateTime,
  getSessionDuration,
  getSessionMinutes,
  isLongSeating,
  parseUtcDate,
} from "../sessionHelpers";

// Session 278 started at 04:25 UTC on 18 Oct 2026, which is 10:25 pm on 17 Oct in Edmonton (MDT, UTC-6)
const STARTED_UTC_NO_ZONE = "2026-10-18T04:25:59.741708";
const STARTED_UTC = "2026-10-18T04:25:59Z";

describe("session time helpers (Edmonton)", () => {
  afterEach(() => jest.useRealTimers());

  it("reads timestamps without a time zone as UTC", () => {
    expect(parseUtcDate(STARTED_UTC_NO_ZONE).toISOString()).toBe("2026-10-18T04:25:59.741Z");
    expect(parseUtcDate(STARTED_UTC).toISOString()).toBe("2026-10-18T04:25:59.000Z");
    expect(parseUtcDate(null)).toBeNull();
  });

  it.each([STARTED_UTC_NO_ZONE, STARTED_UTC])("shows %s as Edmonton time", (value) => {
    expect(formatTime(value)).toMatch(/^10:25\s?p\.?m\.?$/i);
  });

  it("uses the Edmonton date, not the UTC date", () => {
    expect(formatDate(STARTED_UTC)).toMatch(/Oct\.? 17, 2026/);
    expect(formatDateTime(STARTED_UTC)).toMatch(/Oct\.? 17, 2026.*10:25/);
  });

  it("follows daylight saving: winter 2025-26 was UTC-7", () => {
    // 1 Jan 2026 03:00 UTC = 31 Dec 8:00 pm MST. Alberta stays on UTC-6 year-round from
    // 1 Nov 2026, so later winters depend on how new the time zone data is; don't test those.
    expect(formatTime("2026-01-01T03:00:00")).toMatch(/^8:00\s?p\.?m\.?$/i);
  });

  it("counts how long a session has been seated", () => {
    jest.useFakeTimers().setSystemTime(new Date("2026-10-18T05:10:00Z"));

    expect(getSessionDuration(STARTED_UTC_NO_ZONE)).toBe("44m");
    expect(getSessionDuration("2026-10-18T03:00:00")).toBe("2h 10m");
  });

  it("never shows a negative duration", () => {
    jest.useFakeTimers().setSystemTime(new Date("2026-10-18T04:00:00Z"));

    expect(getSessionDuration(STARTED_UTC)).toBe("0m");
  });

  it("flags tables seated longer than 1.5 hours", () => {
    jest.useFakeTimers().setSystemTime(new Date("2026-10-18T06:00:00Z"));

    expect(getSessionMinutes("2026-10-18T04:30:00")).toBe(90);
    expect(isLongSeating("2026-10-18T04:30:00")).toBe(false); // exactly 1.5 hours
    expect(isLongSeating("2026-10-18T04:29:00")).toBe(true);  // 1h 31m
    expect(isLongSeating(null)).toBe(false);
  });
});
