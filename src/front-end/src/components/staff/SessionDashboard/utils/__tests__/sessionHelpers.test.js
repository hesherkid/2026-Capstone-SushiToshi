import {
  formatTime,
  formatDate,
  formatDateTime,
  getSessionDuration,
  getSessionMinutes,
  isLongSeating,
  parseUtcDate,
  sessionGuestCount,
  formatGuestBreakdown,
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

describe("sessionGuestCount", () => {
  it("adds up the guests on every bill, closed ones included", () => {
    const session = {
      active_participants: 1, // one phone scanned; not what staff mean by guests
      bills: [
        { status: "Open", total_guests: 3 },
        { status: "Closed", total_guests: 2 },
      ],
    };
    expect(sessionGuestCount(session)).toBe(5);
  });

  it("leaves out cancelled bills, so a merged bill's guests aren't counted twice", () => {
    const session = {
      bills: [
        { status: "Open", total_guests: 5 },
        { status: "Cancelled", total_guests: 2 },
      ],
    };
    expect(sessionGuestCount(session)).toBe(5);
  });

  it("is 0 before any bill is made", () => {
    expect(sessionGuestCount({ active_participants: 2, bills: [] })).toBe(0);
    expect(sessionGuestCount({})).toBe(0);
    expect(sessionGuestCount(null)).toBe(0);
  });
});

describe("formatGuestBreakdown", () => {
  it("lists each kind of guest on a dashboard bill", () => {
    expect(formatGuestBreakdown({ adult_count: 2, child_count: 1, total_guests: 3 })).toBe("2 Adults, 1 Child");
  });

  it("falls back to the bill's total_guests", () => {
    expect(formatGuestBreakdown({ total_guests: 1 })).toBe("1 guest");
    expect(formatGuestBreakdown({})).toBe("0 guests");
  });
});
