import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import HolidayManagement from "../HolidayManagement";
import * as holidayService from "@/services/holidayService";

jest.mock("@/services/holidayService");

const christmas = {
  holiday_id: 1, province_code: "AB", date: "2026-12-25", observed_date: "2026-12-25",
  name: "Christmas Day", source: "Imported", status: "Pending",
};
const canadaDay = {
  holiday_id: 2, province_code: "AB", date: "2026-07-01", observed_date: "2026-07-01",
  name: "Canada Day", source: "Imported", status: "Approved",
};

beforeEach(() => {
  jest.clearAllMocks();
  holidayService.getHolidayOptions.mockResolvedValue({
    provinces: ["AB", "ON"], default_province: "AB", year_from: 2025, year_to: 2028, include_optional: false,
  });
  holidayService.listHolidays.mockResolvedValue([canadaDay, christmas]);
});

describe("HolidayManagement", () => {
  it("lists the province's holidays with dates as written", async () => {
    render(<HolidayManagement />);

    expect(await screen.findByText("Christmas Day")).toBeInTheDocument();
    // Not "Thu, Dec 24": the date isn't shifted by the time zone
    expect(screen.getByText("Fri, Dec 25, 2026")).toBeInTheDocument();
    expect(holidayService.listHolidays).toHaveBeenCalledWith({ province: "AB", year: expect.any(Number) });
  });

  it("Load holidays fetches them and shows the new ones to review", async () => {
    holidayService.importHolidays.mockResolvedValue({ added: 9, skipped: 0, holidays: [] });
    render(<HolidayManagement />);
    await screen.findByText("Christmas Day");

    fireEvent.click(screen.getByRole("button", { name: "Load holidays" }));

    expect(await screen.findByText(/Loaded 9 new holidays for AB/)).toBeInTheDocument();
    expect(holidayService.importHolidays).toHaveBeenCalledWith({
      province: "AB", year: expect.any(Number), includeOptional: false,
    });
    expect(screen.getByRole("button", { name: "Pending" })).toHaveAttribute("aria-pressed", "true");
  });

  it("shows why loading failed", async () => {
    holidayService.importHolidays.mockRejectedValue({
      response: { status: 502, data: { message: "The holiday service didn't respond in time." } },
    });
    render(<HolidayManagement />);
    await screen.findByText("Christmas Day");

    fireEvent.click(screen.getByRole("button", { name: "Load holidays" }));

    expect(await screen.findByText("The holiday service didn't respond in time.")).toBeInTheDocument();
  });

  it("approves the selected holidays", async () => {
    holidayService.approveHolidays.mockResolvedValue({ updated: 1 });
    render(<HolidayManagement />);
    await screen.findByText("Christmas Day");

    fireEvent.click(screen.getByLabelText("Select Christmas Day"));
    fireEvent.click(screen.getByRole("button", { name: "Approve selected (1)" }));

    await waitFor(() => expect(holidayService.approveHolidays).toHaveBeenCalledWith([1]));
    expect(await screen.findByText("Approved 1 holiday.")).toBeInTheDocument();
  });

  it("filters by status", async () => {
    render(<HolidayManagement />);
    await screen.findByText("Christmas Day");

    fireEvent.click(screen.getByRole("button", { name: "Approved" }));

    expect(screen.getByText("Canada Day")).toBeInTheDocument();
    expect(screen.queryByText("Christmas Day")).not.toBeInTheDocument();
  });
});
