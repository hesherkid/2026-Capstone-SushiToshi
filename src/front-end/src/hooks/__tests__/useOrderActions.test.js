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

describe("addOrderItems", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    orderService.addOrderItems.mockResolvedValue({});
  });

  // Review order shows its own "Order sent!" screen, so a toast would say it twice
  it("sends the items without a toast, then lets the page show its confirmation", async () => {
    const onSuccess = jest.fn();
    const items = [
      { item_id: 11, quantity: 1, name: "Miso Soup" },
      { item_id: 12, quantity: 2, name: "Salmon Roll" },
    ];
    const { result } = renderHook(() => useOrderActions(onSuccess));

    await act(() => result.current.addOrderItems(500, items));

    expect(orderService.addOrderItems).toHaveBeenCalledWith(500, items);
    expect(onSuccess).toHaveBeenCalled();
    expect(mockNotifySuccess).not.toHaveBeenCalled();
  });
});
