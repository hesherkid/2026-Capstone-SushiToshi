import { render, screen, waitFor, fireEvent } from "@testing-library/react";
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

    const tabs = screen.getAllByRole("button", { name: /My orders|Whole table/ });
    expect(tabs.map((t) => t.textContent)).toEqual(["My orders", "Whole table"]);
    expect(tabs[0]).toHaveAttribute("aria-pressed", "true");
  });

  it("shows the whole table's orders, including ones someone else placed", async () => {
    render(<OrdersPage />);
    await screen.findByText("Order #901");
    fireEvent.click(screen.getByRole("button", { name: "Whole table" }));

    expect(await screen.findByText("Order #900")).toBeInTheDocument();
    expect(screen.getByText("Order #901")).toBeInTheDocument();
    // Someone else's order shows its bill but not who placed it
    expect(screen.getByText("Lee")).toBeInTheDocument();
    expect(screen.queryByText(/guest\.sushitoshi\.invalid/)).not.toBeInTheDocument();
    expect(screen.getByText("You · Lee")).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith("/Order/session/269");
  });

  it("can switch back to just the orders this person placed", async () => {
    render(<OrdersPage />);
    await screen.findByText("Order #901");
    fireEvent.click(screen.getByRole("button", { name: "Whole table" }));
    await screen.findByText("Order #900");

    fireEvent.click(screen.getByRole("button", { name: "My orders" }));

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
    expect(screen.getByRole("heading", { name: "Table Orders" })).toBeInTheDocument();
    expect(screen.queryByText(/hasn't ordered|haven't ordered/)).not.toBeInTheDocument();

    await screen.findByText("Order #901");
    expect(screen.queryByRole("progressbar")).not.toBeInTheDocument();
    expect(loadingStates[0]).toBe(true);
    expect(loadingStates.at(-1)).toBe(false);

    unmount();
    window.removeEventListener(PAGE_LOADING_EVENT, record);
  });

  it("has no bill filter, and says when this person hasn't ordered yet", async () => {
    api.get.mockImplementation((url) =>
      url === "/DiningSession/participants/active-session-id"
        ? Promise.resolve({ data: { session_id: 269 } })
        : Promise.resolve({ data: [] })
    );
    render(<OrdersPage />);

    expect(await screen.findByText("You haven't ordered anything yet.")).toBeInTheDocument();
    expect(screen.queryByRole("combobox")).not.toBeInTheDocument();
    expect(screen.queryByText(/Select Bill/)).not.toBeInTheDocument();
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
