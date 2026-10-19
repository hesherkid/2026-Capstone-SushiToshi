import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import OrdersPage from "./page";
import api from "@/config/api";

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
  { orderId: 900, userId: 21, userName: "Guest K7P2", billName: "Lee", status: "Pending", items: [item(1, "Salmon Nigiri")] },
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

  it("shows the whole table's orders, including ones someone else placed", async () => {
    render(<OrdersPage />);

    expect(await screen.findByText("Order #900")).toBeInTheDocument();
    expect(screen.getByText("Order #901")).toBeInTheDocument();
    expect(screen.getByText("Guest K7P2 · Lee")).toBeInTheDocument();
    expect(screen.getByText("You · Lee")).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith("/Order/session/269", { params: {} });
  });

  it("can switch to just the orders this person placed", async () => {
    render(<OrdersPage />);
    await screen.findByText("Order #900");

    fireEvent.click(screen.getByRole("button", { name: "My orders" }));

    await waitFor(() => expect(screen.queryByText("Order #900")).not.toBeInTheDocument());
    expect(screen.getByText("Order #901")).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith("/Order/active-session/orders", { params: {} });
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
