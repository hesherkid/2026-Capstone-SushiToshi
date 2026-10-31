import React from "react";
import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import ReviewOrderPage from "./page";
import { TableOrderProvider } from "@/contexts/TableOrderContext";
import { tableApi } from "../_testing/tableApiMock";

const mockPush = jest.fn();
const mockBack = jest.fn();
jest.mock("next/navigation", () => ({
  useRouter: () => ({ push: mockPush, back: mockBack }),
}));

const mockCreateOrder = jest.fn();
const mockAddOrderItems = jest.fn();
let mockOnSuccess;
jest.mock("@/hooks/useOrderActions", () => ({
  useOrderActions: (onSuccess) => {
    mockOnSuccess = onSuccess;
    return {
      createOrder: mockCreateOrder,
      addOrderItems: mockAddOrderItems,
      actionError: null,
      isLoading: false,
      clearActionError: jest.fn(),
    };
  },
}));

jest.mock("@/config/api", () => ({
  __esModule: true,
  default: { get: jest.fn() },
}));

const api = require("@/config/api").default;

const LEE = {
  bill_id: 8,
  bill_name: "Lee",
  status: "Open",
  created_at: "2026-10-18T04:00:00Z",
};
const PARK = {
  bill_id: 9,
  bill_name: "Park",
  status: "Open",
  created_at: "2026-10-18T04:10:00Z",
};

// A cart saved by the menu page: 2 Miso Soup
const withCart = (quantities = { 1: { 11: 2 } }) =>
  localStorage.setItem(
    "menuCart",
    JSON.stringify({ sessionId: 42, quantities }),
  );

const renderPage = () =>
  render(
    <TableOrderProvider>
      <ReviewOrderPage />
    </TableOrderProvider>,
  );

describe("Review order page", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    // jsdom doesn't scroll; the confirmation screen scrolls to the top
    window.scrollTo = jest.fn();
    localStorage.clear();
    localStorage.setItem("guest", "true");
    api.get.mockImplementation(tableApi({ bills: [LEE] }));
  });

  it("lists the cart with quantity controls, the bill and the subtotal", async () => {
    withCart();
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "Miso Soup" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Add one Miso Soup" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Remove one Miso Soup" }),
    ).toBeInTheDocument();
    // The bill shows at the top and again right above the button
    expect(screen.getByText("Lee", { selector: "p" })).toBeInTheDocument();
    expect(screen.getByText(/Order on/)).toHaveTextContent("Order on Lee");
    expect(screen.getByText("2 items in your order")).toBeInTheDocument();
    expect(screen.getByText("$0.00")).toBeInTheDocument();
    expect(
      screen.getByText(/Additional leftover fees may apply/),
    ).toBeInTheDocument();
    expect(
      await screen.findByRole("button", { name: "Place order · 2 items" }),
    ).toBeEnabled();
  });

  it("places the order with each item's name and shows a confirmation", async () => {
    withCart();
    mockCreateOrder.mockResolvedValue({ order_Id: 500 });
    mockAddOrderItems.mockImplementation(async () => mockOnSuccess());
    renderPage();

    const placeOrder = await screen.findByRole("button", {
      name: "Place order · 2 items",
    });
    await waitFor(() => expect(placeOrder).toBeEnabled());
    fireEvent.click(placeOrder);

    await waitFor(() => expect(mockCreateOrder).toHaveBeenCalledWith(42, 8));
    expect(mockAddOrderItems).toHaveBeenCalledWith(500, [
      expect.objectContaining({
        item_id: 11,
        quantity: 2,
        name: "Miso Soup",
        menu_id: 7,
      }),
    ]);

    expect(
      await screen.findByRole("heading", { name: "Order sent!" }),
    ).toBeInTheDocument();
    expect(screen.getByText("Miso Soup")).toBeInTheDocument();
    // The cart is emptied once the kitchen has it
    expect(JSON.parse(localStorage.getItem("menuCart")).quantities).toEqual({});

    fireEvent.click(screen.getByRole("button", { name: "Track my order" }));
    expect(mockPush).toHaveBeenCalledWith("/dashboard/orders");
  });

  it("shows why an order couldn't be placed on the page itself", async () => {
    withCart();
    mockCreateOrder.mockResolvedValue(null);
    renderPage();

    const placeOrder = await screen.findByRole("button", {
      name: "Place order · 2 items",
    });
    await waitFor(() => expect(placeOrder).toBeEnabled());
    fireEvent.click(placeOrder);

    expect(
      await screen.findByText("Couldn't start your order. Please try again."),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("heading", { name: "Order sent!" }),
    ).not.toBeInTheDocument();
  });

  it("can't order without an open bill, and can check again", async () => {
    withCart();
    api.get.mockImplementation(tableApi({ bills: [] }));
    renderPage();

    expect(
      await screen.findByText(/doesn't have an open bill yet/),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Place order · 2 items" }),
    ).toBeDisabled();

    // Staff open a bill
    api.get.mockImplementation(tableApi({ bills: [LEE] }));
    fireEvent.click(screen.getByRole("button", { name: "Check again" }));

    await waitFor(() =>
      expect(
        screen.getByRole("button", { name: "Place order · 2 items" }),
      ).toBeEnabled(),
    );
  });

  it("lets the guest pick the bill when the table has several, starting on the last one used", async () => {
    withCart();
    localStorage.setItem("currentBillId", "8");
    api.get.mockImplementation(tableApi({ bills: [PARK, LEE] }));
    renderPage();

    expect(
      await screen.findByRole("combobox", { name: "Put this order on" }),
    ).toHaveTextContent("Lee");
    expect(screen.getByText(/Your table has 2 bills/)).toBeInTheDocument();
  });

  it("stops ordering once the guests have asked for their bill", async () => {
    withCart();
    api.get.mockImplementation(
      tableApi({ bills: [{ ...LEE, requested_at: "2026-10-18T04:30:00Z" }] }),
    );
    renderPage();

    expect(
      await screen.findByText(
        /You've asked for your bill, so no more orders can be added/,
      ),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Place order · 2 items" }),
    ).toBeDisabled();
  });

  it("won't place an order that's gone over the table's limit since the cart was filled", async () => {
    withCart(); // 2 Miso Soup
    api.get.mockImplementation(
      tableApi({
        bills: [LEE],
        limits: {
          items: [
            { item_id: 11, category_id: 1, per_order: null, session_left: 1 },
          ],
          categories: [],
        },
      }),
    );
    renderPage();

    expect(
      await screen.findByText(/Too many Miso Soup for your table's limit/),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Place order · 2 items" }),
    ).toBeDisabled();
    expect(
      screen.queryByRole("button", { name: "Add one Miso Soup" }),
    ).not.toBeInTheDocument();
  });

  it("reloads the table's limits after an order goes through, so the menu shows what's left", async () => {
    withCart();
    mockCreateOrder.mockResolvedValue({ order_Id: 500 });
    mockAddOrderItems.mockImplementation(async () => mockOnSuccess());
    renderPage();

    const placeOrder = await screen.findByRole("button", {
      name: "Place order · 2 items",
    });
    await waitFor(() => expect(placeOrder).toBeEnabled());
    const limitsLoads = () =>
      api.get.mock.calls.filter(([url]) => url === "/Order/session/42/limits")
        .length;
    const before = limitsLoads();

    fireEvent.click(placeOrder);

    await screen.findByRole("heading", { name: "Order sent!" });
    await waitFor(() => expect(limitsLoads()).toBeGreaterThan(before));
  });

  it("starts a new order on the next try instead of reusing one that failed", async () => {
    withCart();
    mockCreateOrder
      .mockResolvedValueOnce({ order_Id: 500 })
      .mockResolvedValueOnce({ order_Id: 501 });
    mockAddOrderItems
      .mockResolvedValueOnce(null)
      .mockImplementationOnce(async () => mockOnSuccess());
    renderPage();

    const placeOrder = await screen.findByRole("button", {
      name: "Place order · 2 items",
    });
    await waitFor(() => expect(placeOrder).toBeEnabled());
    fireEvent.click(placeOrder);
    await waitFor(() => expect(mockAddOrderItems).toHaveBeenCalledTimes(1));

    await waitFor(() =>
      expect(
        screen.getByRole("button", { name: "Place order · 2 items" }),
      ).toBeEnabled(),
    );
    fireEvent.click(
      screen.getByRole("button", { name: "Place order · 2 items" }),
    );

    await waitFor(() => expect(mockAddOrderItems).toHaveBeenCalledTimes(2));
    expect(mockCreateOrder).toHaveBeenCalledTimes(2);
    expect(mockAddOrderItems).toHaveBeenLastCalledWith(501, expect.any(Array));
    // No saved order to reuse, and no lookup of the old one
    expect(localStorage.getItem("pendingOrder")).toBeNull();
    expect(api.get).not.toHaveBeenCalledWith("/Order/500");
  });

  it("shows an empty bag with a way back to the menu", async () => {
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "Your bag is empty" }),
    ).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Browse the menu" }));
    expect(mockPush).toHaveBeenCalledWith("/menu/full-menu");
  });
});
