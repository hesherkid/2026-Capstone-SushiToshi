import { renderHook, act } from "@testing-library/react";
import { useOrderActions } from "../useOrderActions";
import orderService from "@/services/orderService";

const mockNotifySuccess = jest.fn();

jest.mock("@/contexts/NotificationContext", () => ({
  useNotification: () => ({ notifySuccess: mockNotifySuccess, notifyError: jest.fn() }),
}));

jest.mock("@/services/orderService", () => ({
  __esModule: true,
  default: { addOrderItems: jest.fn() },
}));

const addItems = async (items) => {
  const { result } = renderHook(() => useOrderActions(jest.fn()));
  await act(() => result.current.addOrderItems(500, items));
};

describe("addOrderItems confirmation", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    orderService.addOrderItems.mockResolvedValue({});
  });

  it("names the items that were added", async () => {
    await addItems([
      { item_id: 11, quantity: 1, name: "Miso Soup" },
      { item_id: 12, quantity: 2, name: "Salmon Roll" },
      { item_id: 13, quantity: 1, name: "Edamame" },
    ]);

    expect(mockNotifySuccess).toHaveBeenCalledWith("1 × Miso Soup, 2 × Salmon Roll, and 1 × Edamame added to order");
  });

  it("shows quantities when more than three kinds of item were added", async () => {
    await addItems([
      { item_id: 11, quantity: 3, name: "Miso Soup" },
      { item_id: 12, quantity: 2, name: "Salmon Roll" },
      { item_id: 13, quantity: 1, name: "Edamame" },
      { item_id: 14, quantity: 1, name: "Gyoza" },
    ]);

    expect(mockNotifySuccess).toHaveBeenCalledWith("3 × Miso Soup, 2 × Salmon Roll, and 2 other items added to order");
  });

  it("counts the items instead of showing 'undefined' when names are missing", async () => {
    await addItems([
      { item_id: 11, quantity: 1 },
      { item_id: 12, quantity: 2 },
      { item_id: 13, quantity: 1 },
    ]);

    expect(mockNotifySuccess).toHaveBeenCalledWith("4 items added to order");
    expect(mockNotifySuccess.mock.calls[0][0]).not.toMatch(/undefined/);
  });
});
