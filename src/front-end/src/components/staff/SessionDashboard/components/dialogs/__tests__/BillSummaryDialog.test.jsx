import { render, screen, waitFor, fireEvent, within } from "@testing-library/react";
import BillSummaryDialog from "../BillSummaryDialog";
import api from "@/config/api";
import { useAuth } from "@/hooks/useAuth";

const mockGetBillSummary = jest.fn();
const mockCloseBill = jest.fn();

jest.mock("../../../context/SessionContext", () => ({
  useSession: () => ({ getBillSummary: mockGetBillSummary, closeBill: mockCloseBill }),
}));

jest.mock("@/hooks/useAuth", () => ({ useAuth: jest.fn() }));

jest.mock("@/config/api", () => {
  const mockApi = { get: jest.fn(), post: jest.fn() };
  return { __esModule: true, default: mockApi, api: mockApi };
});

const openBill = { bill_id: 80, bill_name: "Lee", status: "Open", addon_items: [], total_amount: 180.6 };

const renderSummary = () =>
  render(<BillSummaryDialog open sessionId={8} billId={80} onClose={jest.fn()} />);

describe("Bill summary actions", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockGetBillSummary.mockResolvedValue(openBill);
    api.get.mockResolvedValue({ data: [] });
  });

  it("lets customers ask for their bill", async () => {
    useAuth.mockReturnValue({ userRole: "customer" });
    renderSummary();

    expect(await screen.findByRole("button", { name: /Ready for Bill/i })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Close Bill" })).not.toBeInTheDocument();
  });

  it.each(["staff", "admin"])("lets %s close the bill after confirming", async (userRole) => {
    useAuth.mockReturnValue({ userRole });
    mockCloseBill.mockResolvedValue(true);
    renderSummary();

    fireEvent.click(await screen.findByRole("button", { name: "Close Bill" }));
    expect(screen.queryByRole("button", { name: /Ready for Bill/i })).not.toBeInTheDocument();

    const confirm = await screen.findByText(/Close Lee\?/);
    fireEvent.click(within(confirm.closest('[role="dialog"]')).getByRole("button", { name: "Close Bill" }));

    await waitFor(() => expect(mockCloseBill).toHaveBeenCalledWith(8, 80));
    // Reloads so the summary shows the bill as closed
    await waitFor(() => expect(mockGetBillSummary).toHaveBeenCalledTimes(2));
  });

  it("tells staff when the guests have asked for the bill", async () => {
    useAuth.mockReturnValue({ userRole: "staff" });
    api.get.mockResolvedValue({ data: [{ notes: "Ready for bill: Lee", status: "Pending" }] });
    renderSummary();

    expect(await screen.findByText("The guests have asked for this bill.")).toBeInTheDocument();
  });

  it("offers no actions once the bill is closed", async () => {
    useAuth.mockReturnValue({ userRole: "staff" });
    mockGetBillSummary.mockResolvedValue({ ...openBill, status: "Closed" });
    renderSummary();

    await screen.findByText("Lee");
    expect(screen.queryByRole("button", { name: "Close Bill" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Ready for Bill/i })).not.toBeInTheDocument();
  });
});
