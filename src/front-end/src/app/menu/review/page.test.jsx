import React from "react";
import { render, screen, waitFor, fireEvent, within } from "@testing-library/react";
import ReviewOrderPage from "./page";
import { TableOrderProvider } from "@/contexts/TableOrderContext";
import { tableApi, myBillResponse, MISO_SOUP, SALMON_ROLL } from "../_testing/tableApiMock";

const mockPush = jest.fn();
const mockBack = jest.fn();
jest.mock("next/navigation", () => ({
  useRouter: () => ({ push: mockPush, back: mockBack }),
}));

const mockCreateOrder = jest.fn();
let mockOnSuccess;
// The server saves the order with its items, then the page's success callback runs
const ordersPlaced = (order = { order_Id: 500, orderItems: [] }) =>
  mockCreateOrder.mockImplementation(async () => {
    await mockOnSuccess();
    return order;
  });
jest.mock("@/hooks/useOrderActions", () => ({
  useOrderActions: (onSuccess) => {
    mockOnSuccess = onSuccess;
    return {
      createOrder: mockCreateOrder,
      actionError: null,
      isLoading: false,
      clearActionError: jest.fn(),
    };
  },
}));

jest.mock("@/config/api", () => ({
  __esModule: true,
  default: { get: jest.fn(), put: jest.fn(), post: jest.fn() },
}));

const api = require("@/config/api").default;

const LEE = {
  bill_id: 8,
  bill_name: "Lee",
  status: "Open",
  total_count: 2,
  adult_count: 2,
  people_count: 1,
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
    ordersPlaced();
    renderPage();

    const placeOrder = await screen.findByRole("button", {
      name: "Place order · 2 items",
    });
    await waitFor(() => expect(placeOrder).toBeEnabled());
    fireEvent.click(placeOrder);

    // One request with the items, so a failure can't leave an empty order
    await waitFor(() =>
      expect(mockCreateOrder).toHaveBeenCalledWith(42, 8, [
        expect.objectContaining({
          item_id: 11,
          quantity: 2,
          name: "Miso Soup",
          menu_id: 7,
        }),
      ]),
    );
    expect(mockCreateOrder).toHaveBeenCalledTimes(1);

    expect(
      await screen.findByRole("heading", { name: "Order sent!" }),
    ).toBeInTheDocument();
    expect(screen.getByText("Miso Soup")).toBeInTheDocument();
    // The cart is emptied once the kitchen has it
    expect(JSON.parse(localStorage.getItem("menuCart")).quantities).toEqual({});

    fireEvent.click(screen.getByRole("button", { name: "Track my order" }));
    expect(mockPush).toHaveBeenCalledWith("/dashboard/orders");
  });

  it("tells the guest what the extras over a limit will cost once the order is sent", async () => {
    withCart();
    // The server split the 2 into 1 included and 1 charged extra
    ordersPlaced({
      order_Id: 500,
      orderItems: [
        { item_id: 11, quantity: 1, price_at_time: 0, charge_reason: "None" },
        { item_id: 11, quantity: 1, price_at_time: 5, charge_reason: "OverLimit" },
      ],
    });
    renderPage();

    const placeOrder = await screen.findByRole("button", {
      name: "Place order · 2 items",
    });
    await waitFor(() => expect(placeOrder).toBeEnabled());
    fireEvent.click(placeOrder);

    await screen.findByRole("heading", { name: "Order sent!" });
    expect(
      await screen.findByText(/\$5\.00 in extras will be added to your bill/),
    ).toBeInTheDocument();
    expect(screen.getByText("1 extra × $5.00")).toBeInTheDocument();
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
      await screen.findByText("Couldn't place your order. Please try again."),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("heading", { name: "Order sent!" }),
    ).not.toBeInTheDocument();
  });

  it("can't order before the guest is on a bill, and asks them which one", async () => {
    withCart();
    api.get.mockImplementation(tableApi({ bills: [LEE, PARK], myBillId: null }));
    api.put.mockResolvedValue({ data: myBillResponse({ bills: [LEE, PARK], myBillId: 8 }) });
    renderPage();

    const dialog = await screen.findByRole("dialog", { name: /^Which bill are you on\?/ });
    expect(screen.getByRole("button", { name: "Place order · 2 items", hidden: true })).toBeDisabled();

    fireEvent.click(within(dialog).getByRole("button", { name: /Lee/ }));
    fireEvent.click(await within(dialog).findByRole("button", { name: "Looks right" }));

    await waitFor(() =>
      expect(screen.getByRole("button", { name: "Place order · 2 items" })).toBeEnabled(),
    );
    expect(api.put).toHaveBeenCalledWith("/SessionParticipant/42/bill", { bill_id: 8 });
    expect(screen.getByText(/Order on/)).toHaveTextContent("Order on Lee");
  });

  it("shows the guest's bill with a way to change it", async () => {
    withCart();
    api.get.mockImplementation(tableApi({ bills: [PARK, LEE], myBillId: 8 }));
    renderPage();

    expect(await screen.findByText("Lee", { selector: "p" })).toBeInTheDocument();
    expect(screen.queryByText("Park")).not.toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Change bill from Lee" }));
    const dialog = await screen.findByRole("dialog", { name: /^Which bill are you on\?/ });
    expect(within(dialog).getByRole("button", { name: /Lee/ })).toHaveAttribute("aria-current", "true");
    expect(within(dialog).getByRole("button", { name: /Park/ })).toBeEnabled();
  });

  it("shows the bill's guest count, which the guest can change", async () => {
    withCart();
    api.get.mockImplementation(tableApi({ bills: [LEE], myBillId: 8 }));
    api.put.mockResolvedValue({
      data: myBillResponse({ bills: [{ ...LEE, total_count: 3, adult_count: 2, child_count: 1 }], myBillId: 8 }),
    });
    renderPage();

    expect(await screen.findByText("2 guests")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Edit guests on Lee" }));

    const dialog = await screen.findByRole("dialog", { name: /^How many in your party\?/ });
    expect(within(dialog).getByRole("status")).toHaveTextContent("2 guests on this bill");
    fireEvent.click(within(dialog).getByRole("button", { name: "More children" }));
    fireEvent.click(within(dialog).getByRole("button", { name: "Save guest count" }));

    await waitFor(() =>
      expect(api.put).toHaveBeenCalledWith("/SessionParticipant/42/bill/guests", {
        adult_count: 2,
        senior_count: 0,
        child_count: 1,
        tot_count: 0,
      }),
    );
    expect(await screen.findByText("3 guests")).toBeInTheDocument();
  });

  it("can't change the guest count once the guests have asked for their bill", async () => {
    withCart();
    api.get.mockImplementation(
      tableApi({ bills: [{ ...LEE, requested_at: "2026-10-18T04:30:00Z" }], myBillId: 8 }),
    );
    renderPage();

    expect(await screen.findByText("2 guests")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Edit guests/ })).not.toBeInTheDocument();
  });

  it("once a guest has ordered on their bill, only staff can move them", async () => {
    withCart();
    api.get.mockImplementation(
      tableApi({ bills: [PARK, LEE], myBillId: 8, blocked: "You've already ordered on Lee." }),
    );
    renderPage();

    expect(await screen.findByText("Lee", { selector: "p" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Change bill/ })).not.toBeInTheDocument();
  });

  it("charges for what's past a limit that charges, with the category's free units shared", async () => {
    // 2 Miso Soup and 1 Salmon Roll, both in a category with 2 free left, then $5 each
    const SALMON_IN_APPETIZERS = { ...SALMON_ROLL, category_id: 1 };
    api.get.mockImplementation(
      tableApi({
        bills: [LEE],
        items: [MISO_SOUP, SALMON_IN_APPETIZERS],
        limits: {
          items: [
            { item_id: 11, category_id: 1, free_left: null, extra_price: 5 },
            { item_id: 12, category_id: 1, free_left: null, extra_price: 5 },
          ],
          categories: [{ category_id: 1, free_left: 2 }],
        },
      }),
    );
    withCart({ 1: { 11: 2, 12: 1 } });
    renderPage();

    // Miso Soup uses both free ones, so the Salmon Roll is an extra
    expect(await screen.findByText(/^Extra/)).toHaveTextContent(
      "Extra $5.00 each",
    );
    expect(screen.getAllByText("Included")).toHaveLength(1);
    expect(screen.getByText("Add-ons and extras")).toBeInTheDocument();
    expect(screen.getByText("$5.00")).toBeInTheDocument();
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
    ordersPlaced();
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

  it("sends the whole order again on the next try after one fails", async () => {
    withCart();
    mockCreateOrder.mockResolvedValueOnce(null);
    renderPage();

    const placeOrder = await screen.findByRole("button", {
      name: "Place order · 2 items",
    });
    await waitFor(() => expect(placeOrder).toBeEnabled());
    fireEvent.click(placeOrder);
    await waitFor(() => expect(mockCreateOrder).toHaveBeenCalledTimes(1));

    ordersPlaced({ order_Id: 501, orderItems: [] });

    await waitFor(() =>
      expect(
        screen.getByRole("button", { name: "Place order · 2 items" }),
      ).toBeEnabled(),
    );
    fireEvent.click(
      screen.getByRole("button", { name: "Place order · 2 items" }),
    );

    await screen.findByRole("heading", { name: "Order sent!" });
    expect(mockCreateOrder).toHaveBeenCalledTimes(2);
    expect(mockCreateOrder).toHaveBeenLastCalledWith(42, 8, [
      expect.objectContaining({ item_id: 11, quantity: 2 }),
    ]);
    // No saved order to reuse
    expect(localStorage.getItem("pendingOrder")).toBeNull();
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
