import { render, screen, act, fireEvent, waitFor, within } from "@testing-library/react";
import BillsPage from "./page";
import api from "@/config/api";
import { PAGE_LOADING_EVENT } from "@/utils/pageLoading";
import { AUTO_REFRESH_MS } from "@/hooks/useAutoRefresh";

jest.mock("@/config/api", () => {
  const mockApi = { get: jest.fn(), post: jest.fn() };
  return { __esModule: true, default: mockApi, api: mockApi };
});

jest.mock("@/contexts/NotificationContext", () => ({
  useNotification: () => ({ notifySuccess: jest.fn(), notifyError: jest.fn() }),
}));

const LEE = { bill_id: 8, bill_name: "Lee", status: "Open", total_count: 3 };
const PARK = { bill_id: 9, bill_name: "Park", status: "Open", total_count: 2 };

// GET /Bill/summary for each bill
const summaries = {
  8: {
    bill_id: 8, bill_name: "Lee", status: "Open", adult_count: 2, adult_base_price: 42.95,
    child_count: 1, child_base_price: 28.95, base_charges_subtotal: 114.85,
    addon_items: [{ order_item_id: 5, item_name: "Wagyu Nigiri", quantity: 2, price_per_unit: 9, line_total: 18 }],
    addon_subtotal: 18, subtotal: 132.85, tax_rate: 0.05, tax_amount: 6.64, total_amount: 139.49,
  },
  9: {
    bill_id: 9, bill_name: "Park", status: "Open", adult_count: 2, adult_base_price: 42.95,
    base_charges_subtotal: 85.9, subtotal: 85.9, tax_rate: 0.05, tax_amount: 4.3, total_amount: 90.2,
  },
};

const mockTable = ({ bills = [LEE], seated = true } = {}) => {
  api.get.mockImplementation((url) => {
    if (url === "/DiningSession/participants/active-session-id") {
      return seated
        ? Promise.resolve({ data: { session_id: 42 } })
        : Promise.reject({ response: { status: 404 } });
    }
    if (url === "/Bill/get_bills/42") return Promise.resolve({ status: 200, data: bills });
    const summary = url.match(/^\/Bill\/summary\/(\d+)$/);
    if (summary) return Promise.resolve({ data: summaries[summary[1]] });
    return Promise.resolve({ data: [] });
  });
};

describe("Bill page", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    localStorage.clear();
    mockTable();
    api.post.mockResolvedValue({ status: 200, data: {} });
  });

  it("leaves the loading spinner to the app bar and never flashes 'No bills yet'", async () => {
    const loadingStates = [];
    const record = (event) => loadingStates.push(event.detail.loading);
    window.addEventListener(PAGE_LOADING_EVENT, record);

    render(<BillsPage />);

    expect(screen.getByRole("heading", { name: "Your bill" })).toBeInTheDocument();
    expect(screen.queryByRole("progressbar")).not.toBeInTheDocument();
    expect(screen.queryByText(/No bills yet/)).not.toBeInTheDocument();

    expect(await screen.findByRole("heading", { name: "Lee" })).toBeInTheDocument();
    expect(screen.queryByText(/No bills yet/)).not.toBeInTheDocument();
    expect(loadingStates[0]).toBe(true);
    expect(loadingStates.at(-1)).toBe(false);
    // Loading stays on from the session check through the bills fetch
    expect(loadingStates.filter((loading) => !loading)).toHaveLength(1);

    window.removeEventListener(PAGE_LOADING_EVENT, record);
  });

  it("shows the total, guests and receipt right on the page", async () => {
    render(<BillsPage />);

    expect(await screen.findByText("2 adults · 1 child")).toBeInTheDocument();
    // Up top and as the receipt's last line
    expect(screen.getAllByText("$139.49")).toHaveLength(2);
    expect(screen.getByText("Wagyu Nigiri")).toBeInTheDocument();
    expect(screen.getByText("Open")).toBeInTheDocument();
    expect(screen.getByText("Total, including GST")).toBeInTheDocument();
    expect(screen.getByText("Additional leftover fees may apply.")).toBeInTheDocument();
    // No staff details
    expect(screen.queryByText(/Created:/)).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /View Summary/i })).not.toBeInTheDocument();
  });

  it("asks before requesting the bill, then shows it's on its way", async () => {
    api.post.mockImplementation(() => {
      summaries[8] = { ...summaries[8], requested_at: "2026-10-18T04:30:00Z" };
      return Promise.resolve({ status: 200, data: {} });
    });
    render(<BillsPage />);

    fireEvent.click(await screen.findByRole("button", { name: "Ready for bill" }));
    expect(api.post).not.toHaveBeenCalled();

    const confirm = await screen.findByRole("dialog");
    fireEvent.click(within(confirm).getByRole("button", { name: "Ask for bill" }));

    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith("/Bill/request_bill/8", null, { params: { _session_id: 42 } }),
    );
    expect(await screen.findByText(/Your server will bring your bill shortly/)).toBeInTheDocument();
    expect(await screen.findByRole("button", { name: "Bill requested" })).toBeDisabled();

    delete summaries[8].requested_at;
  });

  it("has nothing to ask for once the bill is closed", async () => {
    mockTable({ bills: [{ ...LEE, status: "Closed" }] });
    summaries[8] = { ...summaries[8], status: "Closed" };
    render(<BillsPage />);

    expect(await screen.findByText(/This bill is closed/)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Ready for bill/i })).not.toBeInTheDocument();
    // The total is final by now
    expect(screen.queryByText(/leftover fees may apply/)).not.toBeInTheDocument();

    summaries[8] = { ...summaries[8], status: "Open" };
  });

  it("puts several bills in tabs, starting on the one this phone orders on", async () => {
    localStorage.setItem("currentBillId", "9");
    mockTable({ bills: [LEE, PARK] });
    render(<BillsPage />);

    expect(await screen.findByRole("heading", { name: "Your table's bills" })).toBeInTheDocument();
    expect(await screen.findByRole("tab", { name: "Park (yours)", selected: true })).toBeInTheDocument();
    expect(await screen.findAllByText("$90.20")).toHaveLength(2);
    expect(screen.getByText(/Orders from this phone go on this bill/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("tab", { name: "Lee" }));

    expect(await screen.findAllByText("$139.49")).toHaveLength(2);
    expect(screen.queryByText(/Orders from this phone go on this bill/)).not.toBeInTheDocument();
  });

  it("leaves out cancelled bills", async () => {
    mockTable({ bills: [LEE, { ...PARK, status: "Cancelled" }] });
    render(<BillsPage />);

    expect(await screen.findByRole("heading", { name: "Lee" })).toBeInTheDocument();
    expect(screen.queryByRole("tab")).not.toBeInTheDocument();
    expect(screen.queryByText("Park")).not.toBeInTheDocument();
  });

  it("says so when the guest isn't seated", async () => {
    mockTable({ seated: false });
    render(<BillsPage />);

    expect(await screen.findByText(/You're not seated at a table yet/)).toBeInTheDocument();
  });

  it("tells guests their server opens the bill, then shows it without a reload", async () => {
    jest.useFakeTimers();
    let bills = [];
    api.get.mockImplementation((url) => {
      if (url === "/DiningSession/participants/active-session-id") return Promise.resolve({ data: { session_id: 42 } });
      if (url === "/Bill/get_bills/42") return Promise.resolve({ status: 200, data: bills });
      if (url === "/Bill/summary/8") return Promise.resolve({ data: summaries[8] });
      return Promise.resolve({ data: [] });
    });

    render(<BillsPage />);

    expect(await screen.findByText(/Your server will open a bill for your table/)).toBeInTheDocument();
    expect(screen.queryByText(/Create your first bill/)).not.toBeInTheDocument();

    // Staff open a bill; the next refresh picks it up
    bills = [LEE];
    await act(async () => jest.advanceTimersByTime(AUTO_REFRESH_MS));

    expect(await screen.findByRole("heading", { name: "Lee" })).toBeInTheDocument();
    jest.useRealTimers();
  });
});
