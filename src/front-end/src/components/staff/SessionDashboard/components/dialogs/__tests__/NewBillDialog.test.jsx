import { fireEvent, render, screen } from "@testing-library/react";
import NewBillDialog from "../NewBillDialog";

let mockSessions = [];

jest.mock("../../../context/SessionContext", () => ({
  useSession: () => ({ createBill: jest.fn(), sessions: mockSessions }),
}));

const renderDialog = (sessionId = 8) =>
  render(<NewBillDialog open sessionId={sessionId} onClose={jest.fn()} />);

describe("New bill dialog title", () => {
  beforeEach(() => {
    localStorage.clear();
    mockSessions = [];
  });

  it("names the session's table for staff", () => {
    mockSessions = [{ session_id: 8, table_numbers: [3] }];
    renderDialog();

    expect(screen.getByText("Create New Bill for Table 3")).toBeInTheDocument();
  });

  it("names every table in a table group", () => {
    mockSessions = [{ session_id: 8, table_numbers: [1, 2] }];
    renderDialog();

    expect(screen.getByText("Create New Bill for Tables 1, 2")).toBeInTheDocument();
  });

  it("uses the guest's QR table when the session list isn't loaded", () => {
    localStorage.setItem("tableNumber", "5");
    renderDialog();

    expect(screen.getByText("Create New Bill for Table 5")).toBeInTheDocument();
  });

  it("falls back to a plain title when the table isn't known", () => {
    renderDialog();

    expect(screen.getByText("Create New Bill")).toBeInTheDocument();
  });
});

describe("New bill dialog seats", () => {
  beforeEach(() => {
    localStorage.clear();
    mockSessions = [];
  });

  const enterAdults = (count) =>
    fireEvent.change(screen.getByLabelText("Adults"), { target: { name: "adultCount", value: String(count) } });

  it("warns staff before seating more than the table has, but still lets them", () => {
    mockSessions = [{ session_id: 8, table_numbers: [3], seat_count: 4, bills: [{ bill_id: 1, status: "OPEN", total_guests: 2 }] }];
    renderDialog();

    enterAdults(2);
    expect(screen.queryByText(/This table seats/)).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Create Bill" })).toBeInTheDocument();

    enterAdults(3);
    expect(screen.getByText("This table seats 4, and the other bills have 2. That makes 5 guests at the table.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Create Anyway" })).toBeEnabled();
  });

  it("doesn't count closed bills against the seats", () => {
    mockSessions = [{ session_id: 8, seat_count: 4, bills: [{ bill_id: 1, status: "CLOSED", total_guests: 2 }] }];
    renderDialog();

    enterAdults(4);
    expect(screen.queryByText(/This table seats/)).not.toBeInTheDocument();
  });

  it("doesn't warn guests, who don't have the seat count", () => {
    renderDialog();

    enterAdults(12);
    expect(screen.queryByText(/This table seats/)).not.toBeInTheDocument();
  });
});
