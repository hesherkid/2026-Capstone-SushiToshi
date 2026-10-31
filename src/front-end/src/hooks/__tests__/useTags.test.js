import { renderHook, waitFor, act } from "@testing-library/react";
import { useTags } from "../useTags";
import * as tagApi from "@/services/tagApi";

jest.mock("@/services/tagApi", () => ({
  getAllTags: jest.fn(),
  createTag: jest.fn(),
  updateTag: jest.fn(),
  deleteTag: jest.fn(),
}));

const TAGS = [{ tag_id: 1, tag_name: "Spicy", tag_color: "#e0a0a0" }];

const renderLoaded = async () => {
  const hook = renderHook(() => useTags());
  await waitFor(() => expect(hook.result.current.loading).toBe(false));
  return hook;
};

beforeEach(() => {
  jest.resetAllMocks();
  tagApi.getAllTags.mockResolvedValue(TAGS);
});

describe("useTags", () => {
  it("loads the tags on mount", async () => {
    const { result } = await renderLoaded();
    expect(result.current.tags).toEqual(TAGS);
    expect(result.current.error).toBeNull();
  });

  it("shows a page error when loading fails", async () => {
    tagApi.getAllTags.mockRejectedValue(new Error("network"));
    const { result } = await renderLoaded();
    expect(result.current.error).toBe("Failed to fetch tags");
    expect(result.current.tags).toEqual([]);
  });

  it("clearError removes the page error", async () => {
    tagApi.getAllTags.mockRejectedValue(new Error("network"));
    const { result } = await renderLoaded();
    act(() => result.current.clearError());
    expect(result.current.error).toBeNull();
  });

  it("create sends the tag and reloads the list", async () => {
    tagApi.createTag.mockResolvedValue({});
    const { result } = await renderLoaded();
    const tag = { tag_name: "Vegan", tag_color: "#a0e0a0" };
    await act(() => result.current.create(tag));
    expect(tagApi.createTag).toHaveBeenCalledWith(tag);
    await waitFor(() => expect(tagApi.getAllTags).toHaveBeenCalledTimes(2));
  });

  it("create throws on failure and does not reload", async () => {
    const failure = { response: { data: { detail: "Tag already exists" } } };
    tagApi.createTag.mockRejectedValue(failure);
    const { result } = await renderLoaded();
    await act(async () => {
      await expect(result.current.create({ tag_name: "Spicy" })).rejects.toBe(
        failure,
      );
    });
    expect(tagApi.getAllTags).toHaveBeenCalledTimes(1);
  });

  it("update sends the tag and reloads the list", async () => {
    tagApi.updateTag.mockResolvedValue({});
    const { result } = await renderLoaded();
    await act(() => result.current.update(TAGS[0]));
    expect(tagApi.updateTag).toHaveBeenCalledWith(TAGS[0]);
    await waitFor(() => expect(tagApi.getAllTags).toHaveBeenCalledTimes(2));
  });

  it("remove deletes by id and reloads the list", async () => {
    tagApi.deleteTag.mockResolvedValue({});
    const { result } = await renderLoaded();
    await act(() => result.current.remove(1));
    expect(tagApi.deleteTag).toHaveBeenCalledWith(1);
    await waitFor(() => expect(tagApi.getAllTags).toHaveBeenCalledTimes(2));
  });
});
