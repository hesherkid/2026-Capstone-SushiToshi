import { renderHook, act } from "@testing-library/react";
import { useAutoRefresh, AUTO_REFRESH_MS } from "../useAutoRefresh";

const setVisibility = (state) => {
  Object.defineProperty(document, "visibilityState", { configurable: true, get: () => state });
  document.dispatchEvent(new Event("visibilitychange"));
};

describe("useAutoRefresh", () => {
  beforeEach(() => {
    jest.useFakeTimers();
    Object.defineProperty(document, "visibilityState", { configurable: true, get: () => "visible" });
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it("refreshes on an interval, not on arrival", async () => {
    const refresh = jest.fn();
    renderHook(() => useAutoRefresh(refresh));

    expect(refresh).not.toHaveBeenCalled();

    await act(async () => jest.advanceTimersByTime(AUTO_REFRESH_MS));
    expect(refresh).toHaveBeenCalledTimes(1);

    await act(async () => jest.advanceTimersByTime(AUTO_REFRESH_MS));
    expect(refresh).toHaveBeenCalledTimes(2);
  });

  it("stops while hidden and refreshes as soon as the page is back", async () => {
    const refresh = jest.fn();
    renderHook(() => useAutoRefresh(refresh));

    await act(async () => setVisibility("hidden"));
    await act(async () => jest.advanceTimersByTime(AUTO_REFRESH_MS * 3));
    expect(refresh).not.toHaveBeenCalled();

    await act(async () => setVisibility("visible"));
    expect(refresh).toHaveBeenCalledTimes(1);
  });

  it("does nothing while disabled", async () => {
    const refresh = jest.fn();
    renderHook(() => useAutoRefresh(refresh, { enabled: false }));

    await act(async () => jest.advanceTimersByTime(AUTO_REFRESH_MS * 3));
    expect(refresh).not.toHaveBeenCalled();
  });

  it("stops when the page is left", async () => {
    const refresh = jest.fn();
    const { unmount } = renderHook(() => useAutoRefresh(refresh));

    unmount();
    await act(async () => jest.advanceTimersByTime(AUTO_REFRESH_MS * 3));
    expect(refresh).not.toHaveBeenCalled();
  });

  it("uses the latest refresh function", async () => {
    const first = jest.fn();
    const second = jest.fn();
    const { rerender } = renderHook(({ fn }) => useAutoRefresh(fn), {
      initialProps: { fn: first },
    });

    rerender({ fn: second });
    await act(async () => jest.advanceTimersByTime(AUTO_REFRESH_MS));
    expect(first).not.toHaveBeenCalled();
    expect(second).toHaveBeenCalledTimes(1);
  });
});
