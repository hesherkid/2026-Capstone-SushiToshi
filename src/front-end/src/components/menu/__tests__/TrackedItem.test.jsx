import { render, screen } from "@testing-library/react";
import "@testing-library/jest-dom";
import TrackedItem from "../TrackedItem";
import useItemViewTracker from "@/hooks/useItemViewTracker";

jest.mock("@/hooks/useItemViewTracker", () => ({
  __esModule: true,
  default: jest.fn(),
}));

describe("TrackedItem", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    useItemViewTracker.mockReturnValue({ current: null });
  });

  it("renders the menu card it wraps", () => {
    render(
      <TrackedItem itemId={7} sessionId={42}>
        <p>Salmon Nigiri</p>
      </TrackedItem>,
    );

    expect(screen.getByText("Salmon Nigiri")).toBeInTheDocument();
  });

  it("tracks the item for the given dining session", () => {
    render(
      <TrackedItem itemId={7} sessionId={42}>
        <p>Salmon Nigiri</p>
      </TrackedItem>,
    );

    expect(useItemViewTracker).toHaveBeenCalledWith(7, 42);
  });

  it("attaches the tracker to the wrapper around the card", () => {
    const ref = { current: null };
    useItemViewTracker.mockReturnValue(ref);

    render(
      <TrackedItem itemId={7} sessionId={42}>
        <p>Salmon Nigiri</p>
      </TrackedItem>,
    );

    expect(ref.current).toBe(screen.getByText("Salmon Nigiri").parentElement);
  });
});
