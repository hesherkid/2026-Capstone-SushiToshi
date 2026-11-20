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
    expect(api.get).toHaveBeenCalledWith("/Bill/summary/80", { params: { session_id: 8 } });
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

    // Free ice cream comes first: no asking until the guest answers
    const askForBill = screen.getByRole("button", { name: "Ask for Bill" });
    expect(askForBill).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: "No, thanks" }));
    fireEvent.click(askForBill);

    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith("/Bill/request-bill/80", null, {
        params: { session_id: 8, ice_cream: false },
      })
    );
    expect(await screen.findByText(/Staff will bring your bill shortly/)).toBeInTheDocument();
    expect(await screen.findByRole("button", { name: "Bill Requested" })).toBeDisabled();
  });

  it("tells staff when the guest wants the free ice cream", async () => {
    renderDialog();

    fireEvent.click(await screen.findByRole("button", { name: "Ready for Bill" }));
    expect(await screen.findByText("Would you like free ice cream?")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Yes, please" }));
    expect(screen.getByRole("button", { name: "Yes, please" })).toHaveAttribute("aria-pressed", "true");
    // Flavours are for the server to ask at the table
    expect(screen.getByText(/Your server will ask for your flavour/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Ask for Bill" }));

    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith("/Bill/request-bill/80", null, {
        params: { session_id: 8, ice_cream: true },
      })
    );
  });

  it("switching to No sends no ice cream", async () => {
    renderDialog();

    fireEvent.click(await screen.findByRole("button", { name: "Ready for Bill" }));
    fireEvent.click(await screen.findByRole("button", { name: "Yes, please" }));
    fireEvent.click(screen.getByRole("button", { name: "No, thanks" }));
    expect(screen.queryByText(/Your server will ask for your flavour/)).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Ask for Bill" }));

    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith("/Bill/request-bill/80", null, {
        params: { session_id: 8, ice_cream: false },
      })
    );
  });

  it("asks about ice cream again each time the confirm opens", async () => {
    renderDialog();

    fireEvent.click(await screen.findByRole("button", { name: "Ready for Bill" }));
    fireEvent.click(await screen.findByRole("button", { name: "Yes, please" }));
    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
    await waitFor(() => expect(screen.queryByText("Would you like free ice cream?")).not.toBeInTheDocument());

    fireEvent.click(screen.getByRole("button", { name: "Ready for Bill" }));
    expect(await screen.findByRole("button", { name: "Yes, please" })).toHaveAttribute("aria-pressed", "false");
    expect(screen.getByRole("button", { name: "Ask for Bill" })).toBeDisabled();
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
