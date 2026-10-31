import { renderHook, act } from "@testing-library/react";
import { useTagMenuItems } from "../useTagMenuItems";
import * as tagApi from "@/services/tagApi";

jest.mock("@/services/tagApi", () => ({
  getTagMenuItems: jest.fn(),
  removeTagFromItem: jest.fn(),
}));

const TAG = { tag_id: 3, tag_name: "Raw", tag_color: "#a0c0e0" };
const ITEMS = [
  { item_id: 10, name: "Salmon Sashimi" },
  { item_id: 11, name: "Tuna Nigiri" },
];

const renderOpened = async () => {
  const hook = renderHook(() => useTagMenuItems());
  await act(() => hook.result.current.openFor(TAG));
  return hook;
};

beforeEach(() => {
  jest.resetAllMocks();
  tagApi.getTagMenuItems.mockResolvedValue(ITEMS);
});

describe("useTagMenuItems", () => {
  it("opens for a tag and loads its items", async () => {
    const { result } = await renderOpened();
    expect(tagApi.getTagMenuItems).toHaveBeenCalledWith(3);
    expect(result.current.isOpen).toBe(true);
    expect(result.current.tag).toBe(TAG);
    expect(result.current.items).toEqual(ITEMS);
    expect(result.current.loading).toBe(false);
  });

  it("shows the server's detail when loading fails", async () => {
    tagApi.getTagMenuItems.mockRejectedValue({
      response: { data: { detail: "Tag not found" } },
    });
    const { result } = await renderOpened();
    expect(result.current.loadError).toBe("Tag not found");
    expect(result.current.items).toEqual([]);
  });

  it("falls back to a default message when loading fails without detail", async () => {
    tagApi.getTagMenuItems.mockRejectedValue(new Error("network"));
    const { result } = await renderOpened();
    expect(result.current.loadError).toBe("Failed to fetch menu items");
  });

  it("removes the tag from an item and drops it from the list", async () => {
    tagApi.removeTagFromItem.mockResolvedValue({});
    const { result } = await renderOpened();
    await act(() => result.current.removeItem(ITEMS[0]));
    expect(tagApi.removeTagFromItem).toHaveBeenCalledWith(10, 3);
    expect(result.current.items).toEqual([ITEMS[1]]);
    expect(result.current.success).toBe("Tag removed from Salmon Sashimi");
  });

  it("keeps the item and shows an error when removing fails", async () => {
    tagApi.removeTagFromItem.mockRejectedValue({
      response: { data: { message: "Item not found" } },
    });
    const { result } = await renderOpened();
    await act(() => result.current.removeItem(ITEMS[0]));
    expect(result.current.items).toEqual(ITEMS);
    expect(result.current.error).toBe("Item not found");
  });

  it("close hides the dialog and clears its messages", async () => {
    tagApi.removeTagFromItem.mockRejectedValue(new Error("network"));
    const { result } = await renderOpened();
    await act(() => result.current.removeItem(ITEMS[0]));
    act(() => result.current.close());
    expect(result.current.isOpen).toBe(false);
    expect(result.current.error).toBeNull();
    expect(result.current.success).toBeNull();
  });
});
