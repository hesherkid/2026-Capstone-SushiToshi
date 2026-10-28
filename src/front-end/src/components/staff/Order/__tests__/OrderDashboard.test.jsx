import {
  render,
  screen,
  within,
  fireEvent,
  waitFor,
} from "@testing-library/react";
import OrderDashboard from "@/components/staff/Order";
import api from "@/config/api";
import { useOrder } from "@/contexts/OrderContext";
import { toDateInput } from "@/components/staff/Order/hooks/useOrderHistory";

jest.mock("@/config/api", () => {
  const mockApi = { get: jest.fn() };
  return { __esModule: true, default: mockApi, api: mockApi };
});

jest.mock("@/utils/storage", () => ({
  __esModule: true,
  default: { get: jest.fn(() => "1") },
}));

jest.mock("@/contexts/OrderContext", () => ({ useOrder: jest.fn() }));

jest.mock("@mui/x-data-grid", () => {
  if (typeof global.TextEncoder === "undefined") {
    const { TextEncoder, TextDecoder } = jest.requireActual("util");
    Object.assign(global, { TextEncoder, TextDecoder });
  }
  const actual = jest.requireActual("@mui/x-data-grid");
  const React = jest.requireActual("react");
  return {
    ...actual,
    DataGrid: (props) =>
      React.createElement(actual.DataGrid, {
        ...props,
        disableVirtualization: true,
      }),
  };
});

if (typeof window.ResizeObserver === "undefined") {
  window.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
}

const item = (orderItemId, name, quantity, status = "Pending") => ({
  orderItemId,
  itemId: orderItemId * 10,
  name,
  quantity,
  priceAtTime: 5,
  status,
});

const orders = [
  {
    orderId: 501,
    sessionId: 1,
    status: "Pending",
    tableNumbers: [1, 2],
    createdAt: "2026-10-14T20:05:00Z",
    items: [item(1, "Salmon Nigiri", 2), item(2, "Miso Soup", 1)],
  },
  {
    orderId: 502,
    sessionId: 2,
    status: "Approved",
    tableNumbers: [5],
    createdAt: "2026-10-14T20:02:00Z",
    items: [item(3, "Tuna Roll", 1, "Processing")],
  },
  {
    orderId: 503,
    sessionId: 2,
    status: "Delivered",
    tableNumbers: [5],
    createdAt: "2026-10-14T19:50:00Z",
    items: [item(4, "Edamame", 4, "Delivered")],
  },
];

const orderActions = {
  approveOrder: jest.fn(() => Promise.resolve({})),
  completeOrder: jest.fn(() => Promise.resolve({})),
  updateOrderItem: jest.fn(() => Promise.resolve({})),
  completeOrderItem: jest.fn(() => Promise.resolve({})),
  isLoading: false,
  actionError: null,
  clearActionError: jest.fn(),
};

const rowFor = (text) => screen.getByText(text).closest('[role="row"]');

const openOrder = async (orderId) => {
  fireEvent.click(
    within(rowFor(`#${orderId}`)).getByRole("button", { name: "View order" }),
  );
  return screen.findByRole("dialog");
};

describe("Order dashboard", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    useOrder.mockReturnValue(orderActions);
    api.get.mockResolvedValue({ data: orders });
  });

  it("shows pending orders as rows with counts for each status", async () => {
    render(<OrderDashboard />);

    const row = await waitFor(() => rowFor("#501"));
    expect(within(row).getByText("Tables 1, 2")).toBeInTheDocument();
    expect(within(row).getByText("3")).toBeInTheDocument(); // 2 + 1 items
    expect(within(row).getByText("$15.00")).toBeInTheDocument();
    expect(screen.queryByText("#502")).not.toBeInTheDocument();

    expect(
      screen.getByRole("tab", { name: "Pending (1)" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("tab", { name: "Processing (1)" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("tab", { name: "Delivered (1)" }),
    ).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith("/Order/all_orders", {
      params: { locationId: "1", deliveredOpenSessionsOnly: true },
    });
  });

  it("lists approved orders under Processing", async () => {
    render(<OrderDashboard />);
    await waitFor(() => rowFor("#501"));

    fireEvent.click(screen.getByRole("tab", { name: "Processing (1)" }));
    const row = await waitFor(() => rowFor("#502"));
    expect(within(row).getByText("Processing")).toBeInTheDocument();
    expect(
      within(row).getByRole("button", { name: "Mark all delivered" }),
    ).toBeInTheDocument();
  });

  it("approves a pending order from its row", async () => {
    render(<OrderDashboard />);
    const row = await waitFor(() => rowFor("#501"));

    fireEvent.click(within(row).getByRole("button", { name: "Approve order" }));
    await waitFor(() =>
      expect(orderActions.approveOrder).toHaveBeenCalledWith(501),
    );
    await waitFor(() => expect(api.get).toHaveBeenCalledTimes(2));
  });

  it("saves edited quantities before approving", async () => {
    render(<OrderDashboard />);
    await waitFor(() => rowFor("#501"));

    const dialog = await openOrder(501);
    fireEvent.click(within(dialog).getByRole("button", { name: "Edit Order" }));
    fireEvent.click(
      within(dialog).getAllByRole("button", { name: "Increase quantity" })[0],
    );
    fireEvent.click(
      within(dialog).getByRole("button", { name: "Save & Approve" }),
    );

    await waitFor(() =>
      expect(orderActions.approveOrder).toHaveBeenCalledWith(501),
    );
    expect(orderActions.updateOrderItem).toHaveBeenCalledTimes(1);
    expect(orderActions.updateOrderItem).toHaveBeenCalledWith(501, 1, {
      quantity: 3,
    });
  });

  it("asks before removing an item from a pending order", async () => {
    render(<OrderDashboard />);
    await waitFor(() => rowFor("#501"));

    const dialog = await openOrder(501);
    fireEvent.click(within(dialog).getByRole("button", { name: "Edit Order" }));
    fireEvent.click(
      within(dialog).getAllByRole("button", { name: "Remove item" })[1],
    );

    const confirm = await screen.findByText(/Remove Miso Soup\?/);
    fireEvent.click(
      within(confirm.closest('[role="dialog"]')).getByRole("button", {
        name: "Remove Item",
      }),
    );

    await waitFor(() =>
      expect(orderActions.updateOrderItem).toHaveBeenCalledWith(501, 2, {
        status: "Cancelled",
      }),
    );
  });
});

const pastOrder = (
  orderId,
  sessionId,
  billId,
  billName,
  status,
  createdAt,
  quantity,
) => ({
  orderId,
  sessionId,
  billId,
  billName,
  status,
  tableNumbers: sessionId === 42 ? [1, 2] : [5],
  createdAt,
  items: [item(orderId * 10, `Item ${orderId}`, quantity, status)],
});

// Session 42 (Tables 1, 2): Lee has two orders, Park has one. Session 43 (Table 5): Kim
const pastOrders = [
  pastOrder(601, 42, 10, "Lee", "Delivered", "2026-10-14T19:00:00Z", 2),
  pastOrder(602, 42, 11, "Park", "Delivered", "2026-10-14T19:05:00Z", 1),
  pastOrder(603, 42, 10, "Lee", "Cancelled", "2026-10-14T19:30:00Z", 1),
  pastOrder(604, 43, 12, "Kim", "Delivered", "2026-10-14T20:00:00Z", 3),
];

const mockHistory = (history) =>
  api.get.mockImplementation((url) =>
    url === "/Order/history" ? history() : Promise.resolve({ data: orders }),
  );

const openPastOrders = async () => {
  render(<OrderDashboard />);
  await waitFor(() => rowFor("#501"));
  fireEvent.click(screen.getByRole("tab", { name: "Past Orders" }));
  return screen.getByRole("form", { name: "Search past orders" });
};

const searchPastOrders = (form) =>
  fireEvent.click(within(form).getByRole("button", { name: "Search" }));

describe("Past orders", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    useOrder.mockReturnValue(orderActions);
    mockHistory(() => Promise.resolve({ data: pastOrders }));
  });

  it("prefills dates to today by default and shows one row per dining session", async () => {
    const form = await openPastOrders();
    searchPastOrders(form);

    const today = toDateInput(new Date());
    await waitFor(() =>
      expect(api.get).toHaveBeenCalledWith("/Order/history", {
        params: { from: today, to: today, locationId: "1" },
      }),
    );

    const row = await waitFor(() => rowFor("#42"));
    expect(within(row).getByText("Tables 1, 2")).toBeInTheDocument();
    expect(within(row).getByText("Lee, Park")).toBeInTheDocument();
    expect(within(row).getByText("3")).toBeInTheDocument(); // orders
    expect(within(row).getByText("4")).toBeInTheDocument(); // 2 + 1 + 1 items
    expect(within(row).getByText("$20.00")).toBeInTheDocument();
    expect(rowFor("#43")).toBeInTheDocument();
  });

  it("opens a table's orders split by bill, read only", async () => {
    const form = await openPastOrders();
    searchPastOrders(form);
    const row = await waitFor(() => rowFor("#42"));

    fireEvent.click(
      within(row).getByRole("button", { name: "View table orders" }),
    );
    const dialog = await screen.findByRole("dialog");

    const lee = within(dialog).getByRole("region", { name: "Bill Lee" });
    expect(within(lee).getByText("Order #601")).toBeInTheDocument();
    expect(within(lee).getByText("Order #603")).toBeInTheDocument();
    expect(within(lee).getAllByText("Cancelled").length).toBeGreaterThan(0);

    const park = within(dialog).getByRole("region", { name: "Bill Park" });
    expect(within(park).getByText("Order #602")).toBeInTheDocument();
    expect(within(park).queryByText("Order #601")).not.toBeInTheDocument();

    expect(
      within(dialog).queryByRole("button", {
        name: /approve|deliver|edit|undo/i,
      }),
    ).not.toBeInTheDocument();
  });

  it("sends the table number and name filters", async () => {
    const form = await openPastOrders();
    fireEvent.change(within(form).getByLabelText("Table #"), {
      target: { value: "14" },
    });
    fireEvent.change(within(form).getByLabelText("Guest or bill name"), {
      target: { value: "  lee " },
    });
    searchPastOrders(form);

    await waitFor(() =>
      expect(api.get).toHaveBeenCalledWith(
        "/Order/history",
        expect.objectContaining({
          params: expect.objectContaining({ tableNumber: "14", name: "lee" }),
        }),
      ),
    );
  });

  it("shows the range error without calling the API", async () => {
    const form = await openPastOrders();
    fireEvent.change(within(form).getByLabelText("From"), {
      target: { value: "2026-01-01" },
    });
    fireEvent.change(within(form).getByLabelText("To"), {
      target: { value: "2026-03-01" },
    });
    searchPastOrders(form);

    expect(
      await within(form).findByText("Choose a range of 31 days or fewer."),
    ).toBeInTheDocument();
    expect(api.get).not.toHaveBeenCalledWith(
      "/Order/history",
      expect.anything(),
    );
  });

  it("shows API validation errors under the matching field", async () => {
    mockHistory(() =>
      Promise.reject({
        response: {
          status: 400,
          data: { errors: { to: ["The end date can't be in the future."] } },
        },
      }),
    );
    const form = await openPastOrders();
    searchPastOrders(form);

    expect(
      await within(form).findByText("The end date can't be in the future."),
    ).toBeInTheDocument();
  });
});
