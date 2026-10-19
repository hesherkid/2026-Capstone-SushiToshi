import { render, screen } from "@testing-library/react";
import { SeatedFor } from "../SessionColumns";

describe("Seated For", () => {
  beforeEach(() => jest.useFakeTimers().setSystemTime(new Date("2026-10-18T06:00:00Z")));
  afterEach(() => jest.useRealTimers());

  it("shows just the duration for up to 1.5 hours", () => {
    render(<SeatedFor startedAt="2026-10-18T04:30:00" />);

    expect(screen.getByText("1h 30m")).toBeInTheDocument();
    expect(screen.queryByLabelText("Seated for more than 1.5 hours")).not.toBeInTheDocument();
  });

  it("adds a warning once a table passes 1.5 hours", () => {
    render(<SeatedFor startedAt="2026-10-18T04:15:00" />);

    expect(screen.getByText("1h 45m")).toBeInTheDocument();
    expect(screen.getByLabelText("Seated for more than 1.5 hours")).toBeInTheDocument();
  });

  it("shows a dash when there's no start time", () => {
    render(<SeatedFor startedAt={null} />);

    expect(screen.getByText("—")).toBeInTheDocument();
  });
});
