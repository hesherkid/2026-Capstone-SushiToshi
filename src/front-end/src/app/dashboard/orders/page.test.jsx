import { render, screen, waitFor, fireEvent, within } from "@testing-library/react";
import OrdersPage from "./page";
import api from "@/config/api";
import { PAGE_LOADING_EVENT } from "@/utils/pageLoading";

jest.mock("@/config/api", () => {
  const mockApi = { get: jest.fn() };
  return { __esModule: true, default: mockApi, api: mockApi };
});

jest.mock("next/navigation", () => ({ useRouter: () => ({ push: jest.fn() }) }));

// Signed in as account 22, a friend who didn't place the orders
jest.mock("@/config/auth", () => ({
  decodeToken: () => ({ "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier": "22" }),
}));

const item = (id, name, status = "Pending") => ({ orderItemId: id, itemId: id, name, quantity: 1, priceAtTime: 0, status });

const tableOrders = [
  { orderId: 900, userId: 21, userName: "guest-3f9c2a@guest.sushitoshi.invalid", billName: "Lee", status: "Pending", items: [item(1, "Salmon Nigiri")] },
  { orderId: 901, userId: 22, userName: "Guest 4MXA", billName: "Lee", status: "Delivered", items: [item(2, "Miso Soup", "Delivered")] },
];

describe("Customer orders page", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    localStorage.setItem("authToken", "token");
    api.get.mockImplementation((url) => {
      if (url === "/DiningSession/participants/active-session-id") return Promise.resolve({ data: { session_id: 269 } });
      if (url === "/Order/session/269") return Promise.resolve({ data: tableOrders });
      if (url === "/Order/active-session/orders") return Promise.resolve({ data: [tableOrders[1]] });
      return Promise.resolve({ data: [] });
    });
  });

  it("starts on this person's own orders, listed first in the picker", async () => {
    render(<OrdersPage />);

    expect(await screen.findByText("Order #901")).toBeInTheDocument();
    expect(screen.queryByText("Order #900")).not.toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith("/Order/active-session/orders");

    const tabs = screen.getAllByRole("tab");
    expect(tabs.map((t) => t.textContent)).toEqual(["My orders", "Whole table"]);
    expect(tabs[0]).toHaveAttribute("aria-selected", "true");
  });

  it("shows the whole table's orders, including ones someone else placed", async () => {
    render(<OrdersPage />);
    await screen.findByText("Order #901");
    fireEvent.click(screen.getByRole("tab", { name: "Whole table" }));

    expect(await screen.findByText("Order #900")).toBeInTheDocument();
    expect(screen.getByText("Order #901")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Your table's orders" })).toBeInTheDocument();
    // Someone else's order shows its bill but not who placed it
    expect(screen.getByText("Lee")).toBeInTheDocument();
    expect(screen.queryByText(/guest\.sushitoshi\.invalid/)).not.toBeInTheDocument();
    expect(screen.getByText("You · Lee")).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith("/Order/session/269");
  });

  it("can switch back to just the orders this person placed", async () => {
    render(<OrdersPage />);
    await screen.findByText("Order #901");
    fireEvent.click(screen.getByRole("tab", { name: "Whole table" }));
    await screen.findByText("Order #900");

    fireEvent.click(screen.getByRole("tab", { name: "My orders" }));

    await waitFor(() => expect(screen.queryByText("Order #900")).not.toBeInTheDocument());
    expect(screen.getByText("Order #901")).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith("/Order/active-session/orders");
  });

  it("shows loading in the app bar only, never a spinner of its own", async () => {
    const loadingStates = [];
    const record = (event) => loadingStates.push(event.detail.loading);
    window.addEventListener(PAGE_LOADING_EVENT, record);

    const { unmount } = render(<OrdersPage />);

    expect(screen.queryByRole("progressbar")).not.toBeInTheDocument();
    // The header and controls are there while orders load, without a premature "No orders"
    expect(screen.getByRole("heading", { name: "Your orders" })).toBeInTheDocument();
    expect(screen.queryByText(/hasn't ordered|haven't ordered/)).not.toBeInTheDocument();

    await screen.findByText("Order #901");
    expect(screen.queryByRole("progressbar")).not.toBeInTheDocument();
    expect(loadingStates[0]).toBe(true);
    expect(loadingStates.at(-1)).toBe(false);

    unmount();
    window.removeEventListener(PAGE_LOADING_EVENT, record);
  });

  it("has no bill filter, and says when no one at the table has ordered yet", async () => {
    api.get.mockImplementation((url) =>
      url === "/DiningSession/participants/active-session-id"
        ? Promise.resolve({ data: { session_id: 269 } })
        : Promise.resolve({ data: [] })
    );
    render(<OrdersPage />);

    // Nothing of their own, so it opens on the table, which has nothing either
    expect(await screen.findByText("No one at your table has ordered yet.")).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Whole table" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByRole("button", { name: "Browse the menu" })).toBeInTheDocument();
    expect(screen.queryByText("You haven't ordered anything yet.")).not.toBeInTheDocument();
    expect(screen.queryByRole("combobox")).not.toBeInTheDocument();
    expect(screen.queryByText(/Select Bill/)).not.toBeInTheDocument();
  });

  it("opens on the whole table when someone else ordered for this person", async () => {
    api.get.mockImplementation((url) => {
      if (url === "/DiningSession/participants/active-session-id") return Promise.resolve({ data: { session_id: 269 } });
      if (url === "/Order/session/269") return Promise.resolve({ data: [tableOrders[0]] });
      return Promise.resolve({ data: [] });
    });
    render(<OrdersPage />);

    expect(await screen.findByText("Order #900")).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Whole table" })).toHaveAttribute("aria-selected", "true");
    // No "you haven't ordered" flash on the way
    expect(screen.queryByText("You haven't ordered anything yet.")).not.toBeInTheDocument();
  });

  it("stays on My orders once the guest picks it, with a way back to the table", async () => {
    api.get.mockImplementation((url) => {
      if (url === "/DiningSession/participants/active-session-id") return Promise.resolve({ data: { session_id: 269 } });
      if (url === "/Order/session/269") return Promise.resolve({ data: [tableOrders[0]] });
      return Promise.resolve({ data: [] });
    });
    render(<OrdersPage />);
    await screen.findByText("Order #900");

    fireEvent.click(screen.getByRole("tab", { name: "My orders" }));

    expect(await screen.findByText("You haven't ordered anything yet.")).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "My orders" })).toHaveAttribute("aria-selected", "true");

    fireEvent.click(screen.getByRole("button", { name: "See the whole table's orders" }));

    expect(await screen.findByText("Order #900")).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Whole table" })).toHaveAttribute("aria-selected", "true");
  });

  it("shows each item's status without expanding anything", async () => {
    render(<OrdersPage />);
    fireEvent.click(await screen.findByRole("tab", { name: "Whole table" }));

    const pending = await screen.findByRole("region", { name: "Order #900" });
    expect(within(pending).getByText("Salmon Nigiri")).toBeVisible();
    expect(within(pending).getByText("Ordered")).toBeVisible();
    const delivered = screen.getByRole("region", { name: "Order #901" });
    expect(within(delivered).getByText("Miso Soup")).toBeVisible();
    expect(within(delivered).getByText("Served")).toBeVisible();
    // Nothing to open: the orders aren't collapsed behind a toggle
    expect(screen.queryByRole("button", { name: /Order #/ })).not.toBeInTheDocument();
  });

  it("keeps the orders up when a background refresh fails", async () => {
    render(<OrdersPage />);
    await screen.findByText("Order #901");

    api.get.mockImplementation(() => Promise.reject({ response: { status: 500, data: "Server error" } }));
    // Coming back to the page triggers an immediate background refresh
    fireEvent(document, new Event("visibilitychange"));

    expect(await screen.findByText(/Couldn't refresh/)).toBeInTheDocument();
    expect(screen.getByText("Order #901")).toBeInTheDocument();
    expect(screen.getByText("Miso Soup")).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Whole table" })).toBeInTheDocument();
    expect(screen.queryByText("Server error")).not.toBeInTheDocument();
  });

  it("shows a failed load inline, with the page controls still there and a retry", async () => {
    api.get.mockImplementation((url) => {
      if (url === "/DiningSession/participants/active-session-id") return Promise.resolve({ data: { session_id: 269 } });
      return Promise.reject({ response: { status: 500, data: "Server error" } });
    });
    render(<OrdersPage />);

    expect(await screen.findByText("Server error")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Your orders" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Whole table" })).toBeInTheDocument();
    expect(screen.queryByText(/haven't ordered/)).not.toBeInTheDocument();

    api.get.mockResolvedValue({ data: [tableOrders[1]] });
    fireEvent.click(screen.getByRole("button", { name: "Retry" }));

    expect(await screen.findByText("Order #901")).toBeInTheDocument();
    expect(screen.queryByText("Server error")).not.toBeInTheDocument();
  });

  it("tells customers who aren't seated to scan their table's QR code", async () => {
    api.get.mockImplementation((url) =>
      url === "/DiningSession/participants/active-session-id"
        ? Promise.reject({ response: { status: 404 } })
        : Promise.resolve({ data: [] })
    );
    render(<OrdersPage />);

    expect(await screen.findByText(/You're not seated at a table yet/)).toBeInTheDocument();
  });
});
