import { fireEvent, render, screen } from "@testing-library/react";
import EditGuestsDialog from "../EditGuestsDialog";

const mockUpdateGuestCounts = jest.fn();
// Table seats 8: Lee's bill has 2, Park's has 3, and a closed bill doesn't count
const lee = { bill_id: 1, bill_name: "Lee", status: "OPEN", total_guests: 2, adult_count: 2, senior_count: 0, child_count: 0, tot_count: 0 };
const mockSessions = [
  {
    session_id: 8,
    seat_count: 8,
    bills: [lee, { bill_id: 2, status: "OPEN", total_guests: 3 }, { bill_id: 3, status: "CLOSED", total_guests: 6 }],
  },
];

jest.mock("../../../context/SessionContext", () => ({
  useSession: () => ({ sessions: mockSessions, updateGuestCounts: mockUpdateGuestCounts, isSaving: false }),
}));

const renderDialog = () => render(<EditGuestsDialog open sessionId={8} bill={lee} onClose={jest.fn()} />);
const enterAdults = (count) =>
  fireEvent.change(screen.getByLabelText("Adults"), { target: { name: "adultCount", value: String(count) } });

describe("Edit guests dialog seats", () => {
  beforeEach(() => mockUpdateGuestCounts.mockReset().mockResolvedValue(true));

  it("doesn't warn while the guests fit, counting this bill's new total in place of its old one", () => {
    renderDialog();

    enterAdults(5);
    expect(screen.queryByText(/This table seats/)).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Save Guests" })).toBeInTheDocument();
  });

  it("warns before going past the seats, and saves anyway when staff confirm", () => {
    renderDialog();

    enterAdults(7);
    expect(screen.getByText("This table seats 8, and the other bills have 3. That makes 10 guests at the table.")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Save Anyway" }));
    expect(mockUpdateGuestCounts).toHaveBeenCalledWith(8, 1, expect.objectContaining({ adultCount: "7" }));
  });
});
