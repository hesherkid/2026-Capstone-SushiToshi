import { render, screen, waitFor, fireEvent, within } from "@testing-library/react";
import BillSummaryDialog from "../BillSummaryDialog";
import api from "@/config/api";

const mockCloseBill = jest.fn();

jest.mock("../../../context/SessionContext", () => ({
  useSession: () => ({ closeBill: mockCloseBill }),
}));

jest.mock("@/config/api", () => {
  const mockApi = { get: jest.fn(), post: jest.fn() };
  return { __esModule: true, default: mockApi, api: mockApi };
});

const openBill = {
  bill_id: 80, bill_name: "Lee", status: "Open", adult_count: 2, adult_base_price: 42.95,
  base_charges_subtotal: 85.9, addon_items: [], subtotal: 85.9, tax_rate: 0.05, tax_amount: 4.3, total_amount: 90.2,
};

const mockApi = ({ summary = openBill, requests = [] } = {}) => {
  api.get.mockImplementation((url) => {
    if (url === "/Bill/summary/80") return Promise.resolve({ data: summary });
    if (url === "/ServiceRequest/by-session/8") return Promise.resolve({ data: requests });
    return Promise.resolve({ data: [] });
  });
};

const mockOnClose = jest.fn();

const renderSummary = () =>
  render(<BillSummaryDialog open sessionId={8} billId={80} onClose={mockOnClose} />);

describe("Staff bill summary", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockApi();
  });

  it("shows the bill's total, closes it after confirming, then closes the summary", async () => {
    mockCloseBill.mockResolvedValue(true);
    renderSummary();

    expect(await screen.findByText("$90.20")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Ready for Bill/i })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Close Bill" }));

    const confirm = await screen.findByText(/Close Lee\?/);
    fireEvent.click(within(confirm.closest('[role="dialog"]')).getByRole("button", { name: "Close Bill" }));

    await waitFor(() => expect(mockCloseBill).toHaveBeenCalledWith(8, 80));
    await waitFor(() => expect(mockOnClose).toHaveBeenCalledTimes(1));
  });

  it("stays open with an error when closing the bill fails", async () => {
    mockCloseBill.mockResolvedValue(false);
    renderSummary();

    fireEvent.click(await screen.findByRole("button", { name: "Close Bill" }));
    const confirm = await screen.findByText(/Close Lee\?/);
    fireEvent.click(within(confirm.closest('[role="dialog"]')).getByRole("button", { name: "Close Bill" }));

    expect(await screen.findByText(/Failed to close the bill/)).toBeInTheDocument();
    expect(mockOnClose).not.toHaveBeenCalled();
  });

  it("tells staff when the guests asked for the bill", async () => {
    mockApi({ summary: { ...openBill, requested_at: "2026-10-18T04:30:00Z" } });
    renderSummary();

    expect(await screen.findByText(/The guests asked for this bill at 10:30/)).toBeInTheDocument();
    // Staff can still close it
    expect(screen.getByRole("button", { name: "Close Bill" })).toBeEnabled();
  });

  it("offers no actions once the bill is closed", async () => {
    mockApi({ summary: { ...openBill, status: "Closed" } });
    renderSummary();

    await screen.findByText("Lee");
    expect(screen.queryByRole("button", { name: "Close Bill" })).not.toBeInTheDocument();
  });
});
