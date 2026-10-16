import { render, screen, within, fireEvent, waitFor } from "@testing-library/react";
import OrderDashboard from "@/components/staff/Order";
import api from "@/config/api";
import { useOrder } from "@/contexts/OrderContext";

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
    within(rowFor(`#${orderId}`)).getByRole("button", { name: "View order" })
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

    expect(screen.getByRole("tab", { name: "Pending (1)" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Processing (1)" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Delivered (1)" })).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith("/Order/all_orders", {
      params: { locationId: "1" },
    });
  });

  it("lists approved orders under Processing", async () => {
    render(<OrderDashboard />);
    await waitFor(() => rowFor("#501"));

    fireEvent.click(screen.getByRole("tab", { name: "Processing (1)" }));
    const row = await waitFor(() => rowFor("#502"));
    expect(within(row).getByText("Processing")).toBeInTheDocument();
    expect(
      within(row).getByRole("button", { name: "Mark all delivered" })
    ).toBeInTheDocument();
  });

  it("approves a pending order from its row", async () => {
    render(<OrderDashboard />);
    const row = await waitFor(() => rowFor("#501"));

    fireEvent.click(within(row).getByRole("button", { name: "Approve order" }));
    await waitFor(() => expect(orderActions.approveOrder).toHaveBeenCalledWith(501));
    await waitFor(() => expect(api.get).toHaveBeenCalledTimes(2));
  });

  it("saves edited quantities before approving", async () => {
    render(<OrderDashboard />);
    await waitFor(() => rowFor("#501"));

    const dialog = await openOrder(501);
    fireEvent.click(within(dialog).getByRole("button", { name: "Edit Order" }));
    fireEvent.click(
      within(dialog).getAllByRole("button", { name: "Increase quantity" })[0]
    );
    fireEvent.click(within(dialog).getByRole("button", { name: "Save & Approve" }));

    await waitFor(() => expect(orderActions.approveOrder).toHaveBeenCalledWith(501));
    expect(orderActions.updateOrderItem).toHaveBeenCalledTimes(1);
    expect(orderActions.updateOrderItem).toHaveBeenCalledWith(501, 1, { quantity: 3 });
  });

  it("asks before removing an item from a pending order", async () => {
    render(<OrderDashboard />);
    await waitFor(() => rowFor("#501"));

    const dialog = await openOrder(501);
    fireEvent.click(within(dialog).getByRole("button", { name: "Edit Order" }));
    fireEvent.click(within(dialog).getAllByRole("button", { name: "Remove item" })[1]);

    const confirm = await screen.findByText(/Remove Miso Soup\?/);
    fireEvent.click(
      within(confirm.closest('[role="dialog"]')).getByRole("button", { name: "Remove Item" })
    );

    await waitFor(() =>
      expect(orderActions.updateOrderItem).toHaveBeenCalledWith(501, 2, {
        status: "Cancelled",
      })
    );
  });
});
