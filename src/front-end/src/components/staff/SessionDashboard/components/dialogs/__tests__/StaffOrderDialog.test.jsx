import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import StaffOrderDialog from "../StaffOrderDialog";
import api from "@/config/api";

jest.mock("@/config/api", () => {
  const mockApi = jest.fn();
  mockApi.get = jest.fn();
  mockApi.post = jest.fn();
  return { __esModule: true, default: mockApi, api: mockApi };
});

const mockNotifySuccess = jest.fn();
jest.mock("@/contexts/NotificationContext", () => ({
  useNotification: () => ({ notifySuccess: mockNotifySuccess, notifyError: jest.fn() }),
}));

let mockSessions = [];
const mockRefreshData = jest.fn();
jest.mock("../../../context/SessionContext", () => ({
  useSession: () => ({ sessions: mockSessions, refreshData: mockRefreshData }),
}));

const openBill = { bill_Id: 5, bill_Name: "Window Booth", status: "Open", requested_At: null };

const session = (overrides = {}) => ({
  session_Id: 8,
  menu_Id: 1,
  location_Id: 2,
  table_Numbers: [3],
  bills: [openBill],
  ...overrides,
});

const ITEMS = [
  { item_id: 10, name: "Salmon Roll", price: 0, categoryID: 1, is_add_on: false },
  { item_id: 11, name: "Tuna Roll", price: 0, categoryID: 1, is_add_on: false },
  { item_id: 66, name: "Hot Stone", price: 5, categoryID: 2, is_add_on: false },
];

const mockMenu = (limits = { items: [], categories: [] }) => {
  api.get.mockImplementation((url) => {
    if (url === "/Category")
      return Promise.resolve({ data: [{ category_id: 1, name: "Rolls" }, { category_id: 2, name: "Grill" }] });
    if (url === "/Menu/1/item") return Promise.resolve({ data: ITEMS });
    if (url === "/Order/session/8/limits") return Promise.resolve({ data: limits });
    return Promise.reject(new Error(`Unexpected GET ${url}`));
  });
};

const renderDialog = () =>
  render(<StaffOrderDialog open sessionId={8} billId={5} onClose={jest.fn()} />);

const placeOrderButton = () => screen.getByRole("button", { name: "Place Order" });

describe("Staff order dialog", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockSessions = [session()];
    mockMenu();
    api.post.mockResolvedValue({ data: { order_Id: 1, orderItems: [] } });
  });

  it("loads the session's menu priced for its location", async () => {
    renderDialog();

    expect(await screen.findByText("Salmon Roll")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Rolls" })).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith("/Menu/1/item", { params: { locationId: 2 } });
  });

  it("can't place an order until an item is picked", async () => {
    renderDialog();
    await screen.findByText("Salmon Roll");

    expect(placeOrderButton()).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: "Add one Salmon Roll" }));
    expect(placeOrderButton()).toBeEnabled();
    expect(screen.getByRole("status")).toHaveTextContent("1 item");
  });

  it("places the order on the bill under the staff member", async () => {
    renderDialog();
    await screen.findByText("Salmon Roll");

    fireEvent.click(screen.getByRole("button", { name: "Add one Salmon Roll" }));
    fireEvent.click(placeOrderButton());

    await waitFor(() => expect(api.post).toHaveBeenCalled());
    expect(api.post).toHaveBeenCalledWith("/Order", {
      session_Id: 8,
      bill_Id: 5,
      items: [expect.objectContaining({ item_id: 10, quantity: 1 })],
    });
    await waitFor(() =>
      expect(mockNotifySuccess).toHaveBeenCalledWith("Order placed on Window Booth"),
    );
    expect(mockRefreshData).toHaveBeenCalled();
  });

  it("stops adding at the same limits the guests have", async () => {
    mockMenu({ items: [{ item_id: 10, category_id: 1, per_order: 1, session_left: null }], categories: [] });
    renderDialog();
    await screen.findByText("Salmon Roll");

    fireEvent.click(screen.getByRole("button", { name: "Add one Salmon Roll" }));

    expect(screen.queryByRole("button", { name: "Add one Salmon Roll" })).not.toBeInTheDocument();
  });

  it("shows the server's reason when the order is refused", async () => {
    api.post.mockRejectedValue({ response: { data: { message: "That guest isn't seated at this table." } } });
    renderDialog();
    await screen.findByText("Salmon Roll");

    fireEvent.click(screen.getByRole("button", { name: "Add one Salmon Roll" }));
    fireEvent.click(placeOrderButton());

    expect(await screen.findByText("That guest isn't seated at this table.")).toBeInTheDocument();
    expect(mockNotifySuccess).not.toHaveBeenCalled();
  });

  it("filters the menu by search but keeps picked items", async () => {
    renderDialog();
    await screen.findByText("Salmon Roll");

    fireEvent.click(screen.getByRole("button", { name: "Add one Hot Stone" }));
    fireEvent.change(screen.getByRole("searchbox", { name: "Search the menu" }), { target: { value: "tuna" } });

    expect(screen.getByText("Tuna Roll")).toBeInTheDocument();
    expect(screen.getByText("Hot Stone")).toBeInTheDocument();
    expect(screen.queryByText("Salmon Roll")).not.toBeInTheDocument();
  });

  it("won't order on a closed bill", async () => {
    mockSessions = [session({ bills: [{ ...openBill, status: "Closed" }] })];
    renderDialog();
    await screen.findByText("Salmon Roll");

    fireEvent.click(screen.getByRole("button", { name: "Add one Salmon Roll" }));

    expect(screen.getByText(/This bill is closed/)).toBeInTheDocument();
    expect(placeOrderButton()).toBeDisabled();
  });
});
