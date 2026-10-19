import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import CustomerBillDialog from "../CustomerBillDialog";
import api from "@/config/api";

jest.mock("@/config/api", () => {
  const mockApi = { get: jest.fn(), post: jest.fn() };
  return { __esModule: true, default: mockApi, api: mockApi };
});

const openBill = {
  bill_id: 80, bill_name: "Lee", status: "Open", adult_count: 2, adult_base_price: 42.95,
  child_count: 1, child_base_price: 28.95, base_charges_subtotal: 114.85,
  addon_items: [{ order_item_id: 5, item_name: "Wagyu Nigiri", quantity: 2, price_per_unit: 9, line_total: 18 }],
  addon_subtotal: 18, subtotal: 132.85, tax_rate: 0.05, tax_amount: 6.64, total_amount: 139.49,
};

const mockApi = ({ summary = openBill, requests = [] } = {}) => {
  api.get.mockImplementation((url) => {
    if (url === "/Bill/summary/80") return Promise.resolve({ data: summary });
    if (url === "/ServiceRequest/by-session/8") return Promise.resolve({ data: requests });
    return Promise.resolve({ data: [] });
  });
};

const renderDialog = () =>
  render(<CustomerBillDialog open sessionId={8} billId={80} onClose={jest.fn()} />);

describe("Customer bill dialog", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockApi();
    api.post.mockResolvedValue({ status: 200, data: {} });
  });

  it("shows the guests, add-ons and total", async () => {
    renderDialog();

    expect(await screen.findByText("Adults")).toBeInTheDocument();
    expect(screen.getByText("Children")).toBeInTheDocument();
    expect(screen.queryByText("Seniors")).not.toBeInTheDocument();
    expect(screen.getByText("Wagyu Nigiri")).toBeInTheDocument();
    expect(screen.getByText("$139.49")).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith("/Bill/summary/80", { params: { _session_id: 8 } });
  });

  it("asks before requesting, then locks the bill", async () => {
    let summary = openBill;
    api.get.mockImplementation((url) =>
      Promise.resolve({ data: url === "/Bill/summary/80" ? summary : [] })
    );
    api.post.mockImplementation(() => {
      summary = { ...openBill, requested_at: "2026-10-18T04:30:00Z" };
      return Promise.resolve({ status: 200, data: { already_requested: false } });
    });
    renderDialog();

    const callForBill = await screen.findByRole("button", { name: "Ready for Bill" });
    expect(callForBill).toHaveClass("MuiButton-fullWidth");
    fireEvent.click(callForBill);
    expect(await screen.findByText(/no more orders can be added to it/)).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole("button", { name: "Ask for Bill" }));

    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith("/Bill/request_bill/80", null, { params: { _session_id: 8 } })
    );
    expect(await screen.findByText(/Staff will bring your bill shortly/)).toBeInTheDocument();
    expect(await screen.findByRole("button", { name: "Bill Requested" })).toBeDisabled();
  });

  it("shows when the bill was already requested and can't be asked for again", async () => {
    mockApi({ summary: { ...openBill, requested_at: "2026-10-18T04:30:00Z" } });
    renderDialog();

    expect(await screen.findByText(/You asked for this bill at 10:30/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Bill Requested" })).toBeDisabled();
  });

  it("never offers staff actions", async () => {
    renderDialog();

    await screen.findByText("Lee");
    expect(screen.queryByRole("button", { name: "Close Bill" })).not.toBeInTheDocument();
  });

  it("offers no actions once the bill is closed", async () => {
    mockApi({ summary: { ...openBill, status: "Closed" } });
    renderDialog();

    await screen.findByText("Lee");
    expect(screen.queryByRole("button", { name: /Ready for Bill/i })).not.toBeInTheDocument();
  });
});
