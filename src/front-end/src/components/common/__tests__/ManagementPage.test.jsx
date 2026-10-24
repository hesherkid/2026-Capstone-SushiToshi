import { render, screen, fireEvent, act } from "@testing-library/react";
import ManagementPage from "../ManagementPage";

// Header and grid aren't tested here
jest.mock("@/components/common/ManagementHeader", () => ({
  __esModule: true,
  default: ({ title }) => <h1>{title}</h1>,
}));
jest.mock("@/components/common/ManagementGrid", () => ({
  __esModule: true,
  default: () => null,
}));

const renderPage = (props) =>
  render(<ManagementPage title="Things" gridProps={{}} {...props} />);

describe("Management page success alert", () => {
  beforeEach(() => jest.useFakeTimers());
  afterEach(() => jest.useRealTimers());

  it("hides a plain success message after 6 seconds", () => {
    const onSuccessClose = jest.fn();
    renderPage({ success: "Saved", onSuccessClose });

    act(() => jest.advanceTimersByTime(5999));
    expect(onSuccessClose).not.toHaveBeenCalled();
    act(() => jest.advanceTimersByTime(1));
    expect(onSuccessClose).toHaveBeenCalledTimes(1);
  });

  it("uses a longer time when one is given", () => {
    const onSuccessClose = jest.fn();
    renderPage({
      success: "Removed",
      onSuccessClose,
      successAutoHideMs: 10000,
    });

    act(() => jest.advanceTimersByTime(6000));
    expect(onSuccessClose).not.toHaveBeenCalled();
    act(() => jest.advanceTimersByTime(4000));
    expect(onSuccessClose).toHaveBeenCalledTimes(1);
  });

  it("shows the action and still offers Close", () => {
    const onSuccessClose = jest.fn();
    const onUndo = jest.fn();
    renderPage({
      success: "Removed",
      onSuccessClose,
      successAction: <button onClick={onUndo}>Undo</button>,
    });

    fireEvent.click(screen.getByRole("button", { name: "Undo" }));
    expect(onUndo).toHaveBeenCalled();
    fireEvent.click(screen.getByRole("button", { name: "Close" }));
    expect(onSuccessClose).toHaveBeenCalled();
  });

  it("keeps MUI's own Close button when there's no action", () => {
    const onSuccessClose = jest.fn();
    renderPage({ success: "Saved", onSuccessClose });

    fireEvent.click(screen.getByRole("button", { name: "Close" }));
    expect(onSuccessClose).toHaveBeenCalled();
  });
});
