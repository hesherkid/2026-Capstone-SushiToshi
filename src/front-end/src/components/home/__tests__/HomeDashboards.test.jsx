import { render, screen, within } from "@testing-library/react";
import HomePage from "@/app/page";
import api from "@/config/api";
import useAuth from "@/hooks/useAuth";

jest.mock("next/navigation", () => ({
  useRouter: jest.fn(() => ({ push: jest.fn(), replace: jest.fn() })),
  usePathname: jest.fn(() => "/"),
}));

jest.mock("@/hooks/useAuth", () => ({
  __esModule: true,
  default: jest.fn(),
  useAuth: jest.fn(),
}));

jest.mock("@/config/api", () => {
  const mockApi = { get: jest.fn() };
  return { __esModule: true, default: mockApi, api: mockApi };
});

jest.mock("@/utils/auth", () => ({ getAuthToken: jest.fn(() => "token") }));
jest.mock("@/config/auth", () => ({
  decodeToken: jest.fn(() => ({
    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier": "7",
  })),
  isTokenExpired: jest.fn(() => false),
}));

const sessions = [
  { session_Id: 1, table_Numbers: [1, 2] },
  { session_Id: 2, table_Numbers: [5] },
];
const pendingRequests = [
  { request_id: 11, session_id: 1, table_number: 2, notes: "Need more soy sauce", created_at: "2026-10-14T20:00:00Z" },
  // Belongs to another location's session, so it must not be shown
  { request_id: 99, session_id: 42, table_number: 9, notes: "Other location", created_at: "2026-10-14T20:01:00Z" },
];

const orders = [
  { orderId: 501, sessionId: 1, status: "Pending", tableNumbers: [1, 2], createdAt: "2026-10-14T20:05:00Z", items: [{ quantity: 2 }, { quantity: 1 }] },
  { orderId: 502, sessionId: 2, status: "Processing", tableNumbers: [5], createdAt: "2026-10-14T20:02:00Z", items: [{ quantity: 1 }] },
  { orderId: 503, sessionId: 2, status: "Delivered", tableNumbers: [5], createdAt: "2026-10-14T19:50:00Z", items: [{ quantity: 4 }] },
];

const mockApiData = ({ requests = pendingRequests, allOrders = orders } = {}) => {
  api.get.mockImplementation((url) => {
    if (url === "/Dashboard/sessions") return Promise.resolve({ data: sessions });
    if (url.includes("ServiceRequest/pending")) return Promise.resolve({ data: requests });
    if (url === "/staff/7") return Promise.resolve({ data: { first_name: "Arlyssa" } });
    if (url === "/Order/all_orders") return Promise.resolve({ data: allOrders });
    return Promise.resolve({ data: [] });
  });
};

const signInAs = (userRole) => {
  const auth = { isAuthenticated: true, userRole, userEmail: "arlyssa@sushi.com", loading: false };
  useAuth.mockReturnValue(auth);
  require("@/hooks/useAuth").useAuth.mockReturnValue(auth);
};

describe("Home page dashboards", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    localStorage.clear();
    mockApiData();
  });

  it("counts sessions at the location picked in the app bar, like the Sessions page", async () => {
    // Admins have no location on their account; the app bar picker decides
    localStorage.setItem("branch-location", "2");
    signInAs("admin");
    render(<HomePage />);

    expect(await screen.findByRole("link", { name: "2 Active sessions" })).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith("/Dashboard/sessions", { params: { locationId: "2" } });
    expect(api.get).not.toHaveBeenCalledWith("/DiningSession/get_list_dining_sessions", expect.anything());
  });

  it("shows staff a welcome, live counts and only this location's table requests", async () => {
    signInAs("staff");
    render(<HomePage />);

    expect(await screen.findByRole("heading", { name: "Welcome, Arlyssa" })).toBeInTheDocument();
    // 3 tables across 2 active sessions, 1 request at this location
    expect(await screen.findByText("Tables active")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "3 Tables active" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "2 Active sessions" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "1 Pending requests" })).toBeInTheDocument();

    const alert = await screen.findByRole("alert");
    expect(within(alert).getByText("1 table needs help")).toBeInTheDocument();
    expect(within(alert).getByText("Table 2")).toBeInTheDocument();
    expect(within(alert).getByText("Need more soy sauce")).toBeInTheDocument();
    expect(within(alert).queryByText("Table 9")).not.toBeInTheDocument();

    for (const name of ["Sessions", "Tables", "Orders"]) {
      expect(screen.getByRole("link", { name: new RegExp(`^${name}`) })).toBeInTheDocument();
    }
    // Bills are managed from the session dialog, not a separate page
    expect(screen.queryByRole("link", { name: /^Bills/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /Analytics Overview/ })).not.toBeInTheDocument();
  });

  it("notifies staff about pending and processing orders, oldest first", async () => {
    signInAs("staff");
    render(<HomePage />);

    const ordersAlert = await screen.findByRole("status");
    expect(within(ordersAlert).getByText("2 open orders")).toBeInTheDocument();
    expect(within(ordersAlert).getByText("1 pending · 1 processing")).toBeInTheDocument();
    expect(within(ordersAlert).getByText("Tables 1, 2")).toBeInTheDocument();
    expect(within(ordersAlert).getByText("Order #501 · 3 items")).toBeInTheDocument();
    expect(within(ordersAlert).queryByText(/Order #503/)).not.toBeInTheDocument();

    // Processing order (20:02) has waited longer than the pending one (20:05)
    const rows = within(ordersAlert).getAllByText(/^Order #/);
    expect(rows.map((row) => row.textContent)).toEqual(["Order #502 · 1 item", "Order #501 · 3 items"]);

    expect(screen.getByRole("link", { name: /^Orders\s*\(2 pending\)/ })).toHaveAttribute("href", "/staff/dashboard/orders");
  });

  it("previews at most 5 requests and orders, linking to the rest", async () => {
    signInAs("staff");
    const manyRequests = Array.from({ length: 7 }, (_, i) => ({
      request_id: 100 + i, session_id: 1, table_number: i + 1, notes: `Request ${i + 1}`,
      created_at: `2026-10-14T20:0${i}:00Z`,
    }));
    const manyOrders = Array.from({ length: 6 }, (_, i) => ({
      orderId: 600 + i, sessionId: 1, status: "Pending", tableNumbers: [i + 1],
      createdAt: `2026-10-14T20:0${i}:00Z`, items: [{ quantity: 1 }],
    }));
    mockApiData({ requests: manyRequests, allOrders: manyOrders });
    render(<HomePage />);

    const requestsAlert = await screen.findByRole("alert");
    expect(within(requestsAlert).getByText("7 tables need help")).toBeInTheDocument();
    expect(within(requestsAlert).getAllByText(/^Request \d$/)).toHaveLength(5);
    expect(within(requestsAlert).queryByText("Request 6")).not.toBeInTheDocument();
    expect(within(requestsAlert).getByRole("link", { name: "+2 more requests" }))
      .toHaveAttribute("href", "/dashboard/sessions");

    const ordersAlert = screen.getByRole("status");
    expect(within(ordersAlert).getByText("6 open orders")).toBeInTheDocument();
    expect(within(ordersAlert).getAllByText(/^Order #/)).toHaveLength(5);
    expect(within(ordersAlert).getByRole("link", { name: "+1 more order" }))
      .toHaveAttribute("href", "/staff/dashboard/orders");
  });

  it("tells staff when the kitchen has no open orders", async () => {
    signInAs("staff");
    mockApiData({ allOrders: [] });
    render(<HomePage />);

    expect(await screen.findByText("No open orders. The kitchen is all caught up.")).toBeInTheDocument();
  });

  it("does not load orders on the admin home", async () => {
    signInAs("admin");
    render(<HomePage />);

    await screen.findByRole("heading", { name: "Welcome back, Arlyssa" });
    expect(api.get).not.toHaveBeenCalledWith("/Order/all_orders", expect.anything());
  });

  it("tells staff when no tables are waiting", async () => {
    signInAs("staff");
    mockApiData({ requests: [] });
    render(<HomePage />);

    expect(await screen.findByText("No tables are waiting for help right now.")).toBeInTheDocument();
  });

  it("shows admins an overview with shortcuts to every management area", async () => {
    signInAs("admin");
    render(<HomePage />);

    expect(await screen.findByRole("heading", { name: "Welcome back, Arlyssa" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /QR Codes/ })).toHaveAttribute("href", "/admin/qr-codes");
    expect(screen.getByRole("link", { name: /Analytics Overview/ })).toHaveAttribute("href", "/analytics/analytic-page");
    expect(screen.getByRole("link", { name: /Menu Items/ })).toHaveAttribute("href", "/admin/menu-items");
    expect(screen.getByRole("link", { name: /^Staff/ })).toHaveAttribute("href", "/admin/staff");
    expect(screen.getByRole("link", { name: /^Customers/ })).toHaveAttribute("href", "/admin/customers");
    expect(await screen.findByText("1 table needs help")).toBeInTheDocument();
  });
});
