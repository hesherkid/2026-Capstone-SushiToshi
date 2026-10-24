import {
  render,
  screen,
  waitFor,
  within,
  fireEvent,
} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import "@testing-library/jest-dom";
import TableTurnover from "../TableTurnover";
import api from "@/config/api";
import storage from "@/utils/storage";

jest.mock("@/config/api", () => {
  const api = { get: jest.fn() };
  return { __esModule: true, default: api, api };
});

jest.mock("@/utils/storage", () => ({
  __esModule: true,
  default: {
    get: jest.fn(),
    set: jest.fn(),
    remove: jest.fn(),
  },
}));

// Two charts on this page, so each mock chart uses the chart's id as its test id.
jest.mock("react-chartjs-2", () => {
  const React = jest.requireActual("react");
  function Bar({ ref, ...props }) {
    React.useImperativeHandle(ref, () => ({
      canvas: document.createElement("canvas"),
    }));
    const dataset = props.data.datasets[0];
    return React.createElement("div", {
      role: "img",
      "aria-label": props["aria-label"],
      "data-testid": props.id,
      "data-labels": JSON.stringify(props.data.labels),
      "data-values": JSON.stringify(dataset.data),
      "data-colors": JSON.stringify(dataset.backgroundColor),
    });
  }
  return { __esModule: true, Bar };
});

if (typeof window.ResizeObserver === "undefined") {
  window.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
}

const ENDPOINT = "analytics/table-turnover";

//  Test data (matches TableTurnoverResponseDTO)

const duration = (count, average, median) => ({
  count,
  average_minutes: average,
  median_minutes: median,
});

const noDuration = () => duration(0, null, null);

const DEFAULT_HOURS = {
  12: { seatings: 1, guests: 2 },
  17: { seatings: 4, guests: 10 },
  18: { seatings: 3, guests: 14 },
  19: { seatings: 2, guests: 5 },
};

const seatingsByHour = (counts = DEFAULT_HOURS) =>
  Array.from({ length: 24 }, (_, hour) => ({
    hour,
    label: `${String(hour).padStart(2, "0")}:00`,
    seatings: counts[hour]?.seatings ?? 0,
    guests: counts[hour]?.guests ?? 0,
  }));

const peak = (hour) => ({
  hour,
  label: `${String(hour).padStart(2, "0")}:00`,
  ...DEFAULT_HOURS[hour],
});

const bucket = (party_size, sessions, over_time_limit, closed) => ({
  party_size,
  sessions,
  over_time_limit,
  duration: closed,
});

const buildResponse = (overrides = {}) => ({
  range: {
    preset: "last7",
    from: "2026-09-30",
    to: "2026-10-06",
    days: 7,
    time_zone: "America/Edmonton",
  },
  location: { id: 1, name: "North" },
  summary: {
    sessions_seated: 10,
    sessions_open: 2,
    guests_seated: 31,
    sessions_without_party_size: 1,
    sessions_over_time_limit: 2,
    time_limit_minutes: 90,
    max_gap_minutes: 120,
  },
  peak_by_seatings: peak(17),
  peak_by_guests: peak(18),
  seatings_by_hour: seatingsByHour(),
  duration_by_party_size: [
    bucket("2", 5, 1, duration(4, 75.5, 74)),
    bucket("4", 3, 1, duration(3, 88, 90)),
    bucket("6+", 1, 0, noDuration()), // still seated
    bucket("unknown", 1, 0, duration(1, 60, 60)),
  ],
  turnover_gap: {
    tables: duration(6, 12.5, 11),
    groups: noDuration(),
    gaps_over_limit: 3,
    overlapping_sessions: 0,
  },
  ...overrides,
});

const withSummary = (summaryOverrides) => ({
  summary: { ...buildResponse().summary, ...summaryOverrides },
});

const withGap = (gapOverrides) => ({
  turnover_gap: { ...buildResponse().turnover_gap, ...gapOverrides },
});

const noSeatings = () => ({
  seatings_by_hour: seatingsByHour({}),
  peak_by_seatings: null,
  peak_by_guests: null,
  duration_by_party_size: [],
  ...withSummary({
    sessions_seated: 0,
    sessions_open: 0,
    guests_seated: 0,
    sessions_without_party_size: 0,
    sessions_over_time_limit: 0,
  }),
});

const lastParams = () => api.get.mock.calls.at(-1)[1].params;

const renderLoaded = async () => {
  const user = userEvent.setup({ delay: null });
  render(<TableTurnover />);
  await screen.findByText(/parties seated \(/i);
  return user;
};

const card = (label) => screen.getByText(label).closest(".MuiCard-root");

const chartData = (id, attribute) =>
  JSON.parse(screen.getByTestId(id).getAttribute(attribute));

const HOUR_CHART = "seatings-by-hour-chart";
const VISIT_CHART = "visit-length-chart";

describe("TableTurnover", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    storage.get.mockReturnValue("1");
    api.get.mockResolvedValue({ data: buildResponse() });
  });

  describe("Data loading", () => {
    it("requests the last 7 full days for the saved location by default", async () => {
      render(<TableTurnover />);

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith(
          ENDPOINT,
          expect.objectContaining({
            params: { range: "last7", locationId: "1" },
          }),
        );
      });
      expect(storage.get).toHaveBeenCalledWith("branch-location");
    });

    it("passes an abort signal so outdated requests can be cancelled", async () => {
      render(<TableTurnover />);

      await waitFor(() => expect(api.get).toHaveBeenCalled());
      expect(api.get.mock.calls[0][1].signal).toBeInstanceOf(AbortSignal);
    });

    it("omits locationId when no branch location is saved", async () => {
      storage.get.mockReturnValue(null);
      render(<TableTurnover />);

      await waitFor(() => expect(api.get).toHaveBeenCalled());
      expect(lastParams()).toEqual({ range: "last7" });
      expect(screen.getByText("All locations")).toBeInTheDocument();
    });

    it("shows a loading message while the request is pending", async () => {
      api.get.mockReturnValue(new Promise(() => {}));
      render(<TableTurnover />);

      expect(
        await screen.findByText(/Loading table turnover/i),
      ).toBeInTheDocument();
    });
  });

  describe("Summary", () => {
    it("shows the date range, time zone, parties and guests", async () => {
      await renderLoaded();

      expect(screen.getByText(/America\/Edmonton time/)).toBeInTheDocument();
      expect(
        screen.getByText(/10 parties seated \(2 still seated\), 31 guests/i),
      ).toBeInTheDocument();
    });
  });

  describe("Seatings", () => {
    it("shows parties seated with how many are still seated", async () => {
      await renderLoaded();

      expect(card("Parties seated")).toHaveTextContent("10");
      expect(card("Parties seated")).toHaveTextContent("2 still seated");
    });

    it("explains a party with no bill in singular", async () => {
      await renderLoaded();

      expect(card("Guests seated")).toHaveTextContent("31");
      expect(card("Guests seated")).toHaveTextContent(
        "1 party has no bill, so its size is unknown",
      );
    });

    it("explains several parties with no bill in plural", async () => {
      api.get.mockResolvedValue({
        data: buildResponse(withSummary({ sessions_without_party_size: 3 })),
      });
      await renderLoaded();

      expect(card("Guests seated")).toHaveTextContent(
        "3 parties have no bill, so their sizes are unknown",
      );
    });

    it("says where guest counts come from when every party has a bill", async () => {
      api.get.mockResolvedValue({
        data: buildResponse(withSummary({ sessions_without_party_size: 0 })),
      });
      await renderLoaded();

      expect(card("Guests seated")).toHaveTextContent(
        "From the guest counts on each party's bills",
      );
    });

    it("shows visits over the time limit as a share of closed visits", async () => {
      await renderLoaded();

      // 10 seated - 2 still seated = 8 closed; 2 of 8 = 25%
      expect(card("Over 90 minutes")).toHaveTextContent("2");
      expect(card("Over 90 minutes")).toHaveTextContent("25% of closed visits");
    });

    it("shows a dash instead of 0 when no visits were closed", async () => {
      api.get.mockResolvedValue({
        data: buildResponse(
          withSummary({ sessions_open: 10, sessions_over_time_limit: 0 }),
        ),
      });
      await renderLoaded();

      expect(card("Over 90 minutes")).toHaveTextContent("-");
      expect(card("Over 90 minutes")).toHaveTextContent(
        "No closed visits in this period",
      );
    });
  });

  describe("Busiest time", () => {
    it("names the busiest hour by parties and by guests", async () => {
      await renderLoaded();

      expect(screen.getByText(/Most parties seated/)).toHaveTextContent(
        "Most parties seated: 5 PM (4). Most guests seated: 6 PM (14).",
      );
    });

    it("charts parties seated from the first to the last busy hour", async () => {
      await renderLoaded();

      expect(chartData(HOUR_CHART, "data-labels")).toEqual([
        "12 PM",
        "1 PM",
        "2 PM",
        "3 PM",
        "4 PM",
        "5 PM",
        "6 PM",
        "7 PM",
      ]);
      expect(chartData(HOUR_CHART, "data-values")).toEqual([
        1, 0, 0, 0, 0, 4, 3, 2,
      ]);
    });

    it("highlights only the busiest hour", async () => {
      await renderLoaded();

      const labels = chartData(HOUR_CHART, "data-labels");
      const colors = chartData(HOUR_CHART, "data-colors");
      const peakColor = colors[labels.indexOf("5 PM")];
      expect(colors.filter((c) => c === peakColor)).toHaveLength(1);
    });

    it("switches the chart to guests and moves the highlight", async () => {
      const user = await renderLoaded();

      await user.click(screen.getByRole("button", { name: "Guests" }));

      expect(chartData(HOUR_CHART, "data-values")).toEqual([
        2, 0, 0, 0, 0, 10, 14, 5,
      ]);
      const labels = chartData(HOUR_CHART, "data-labels");
      const colors = chartData(HOUR_CHART, "data-colors");
      const peakColor = colors[labels.indexOf("6 PM")];
      expect(colors.filter((c) => c === peakColor)).toHaveLength(1);
      expect(
        screen.getByRole("img", {
          name: "Guests seated by hour. Busiest hour 6 PM with 14 guests seated.",
        }),
      ).toBeInTheDocument();
    });

    it("doesn't send a new request when switching between parties and guests", async () => {
      const user = await renderLoaded();
      const callsBefore = api.get.mock.calls.length;

      await user.click(screen.getByRole("button", { name: "Guests" }));

      expect(api.get.mock.calls.length).toBe(callsBefore);
    });

    it("shows a message instead of a chart when no parties were seated", async () => {
      api.get.mockResolvedValue({ data: buildResponse(noSeatings()) });
      await renderLoaded();

      expect(
        screen.getByText("No parties were seated in this period."),
      ).toBeInTheDocument();
      expect(screen.queryByTestId(HOUR_CHART)).not.toBeInTheDocument();
      expect(screen.queryByText(/Most parties seated/)).not.toBeInTheDocument();
    });
  });

  describe("Visit length by party size", () => {
    it("charts the average closed visit for each party size that has one", async () => {
      await renderLoaded();

      // 6+ is still seated (no closed visits), so it isn't charted.
      expect(chartData(VISIT_CHART, "data-labels")).toEqual([
        "2 guests",
        "4 guests",
        "Unknown size",
      ]);
      expect(chartData(VISIT_CHART, "data-values")).toEqual([75.5, 88, 60]);
    });

    it("gives the chart an accessible description", async () => {
      await renderLoaded();

      expect(
        screen.getByRole("img", {
          name: "Average visit length by party size. 2 guests: 75.5 min, 4 guests: 88 min, Unknown size: 60 min.",
        }),
      ).toBeInTheDocument();
    });

    it("uses singular wording for parties of one", async () => {
      api.get.mockResolvedValue({
        data: buildResponse({
          duration_by_party_size: [bucket("1", 2, 0, duration(2, 40, 40))],
        }),
      });
      await renderLoaded();

      expect(chartData(VISIT_CHART, "data-labels")).toEqual(["1 guest"]);
    });

    it("shows a message instead of a chart when no visits were closed", async () => {
      api.get.mockResolvedValue({ data: buildResponse(noSeatings()) });
      await renderLoaded();

      expect(
        screen.getByText("No visits were closed in this period."),
      ).toBeInTheDocument();
      expect(screen.queryByTestId(VISIT_CHART)).not.toBeInTheDocument();
    });
  });

  describe("Time between parties", () => {
    it("shows single tables and table groups separately", async () => {
      await renderLoaded();

      expect(card("Single tables")).toHaveTextContent("12.5 min");
      expect(card("Single tables")).toHaveTextContent("Median 11 min · 6 gaps");
      expect(card("Table groups")).toHaveTextContent("-");
      expect(card("Table groups")).toHaveTextContent("No data in this period");
    });

    it("explains the gap rule and how many long gaps were left out", async () => {
      await renderLoaded();

      expect(screen.getByText(/From one party leaving/)).toHaveTextContent(
        "Gaps over 120 minutes count as the table sitting empty and aren't included (3 in this period).",
      );
    });

    it("doesn't mention overlapping sessions when there are none", async () => {
      await renderLoaded();

      expect(
        screen.queryByText(/seated before the previous party/),
      ).not.toBeInTheDocument();
    });

    it("explains overlapping sessions in plural", async () => {
      api.get.mockResolvedValue({
        data: buildResponse(withGap({ overlapping_sessions: 2 })),
      });
      await renderLoaded();

      expect(
        screen.getByText(/2 parties were seated before the previous party/),
      ).toHaveTextContent("so they aren't included.");
    });

    it("explains one overlapping session in singular", async () => {
      api.get.mockResolvedValue({
        data: buildResponse(withGap({ overlapping_sessions: 1 })),
      });
      await renderLoaded();

      expect(
        screen.getByText(/1 party was seated before the previous party/),
      ).toHaveTextContent("so it isn't included.");
    });
  });

  describe("Date range", () => {
    it("requests yesterday when Yesterday is selected", async () => {
      const user = await renderLoaded();

      await user.click(screen.getByRole("button", { name: "Yesterday" }));

      await waitFor(() =>
        expect(lastParams()).toMatchObject({ range: "yesterday" }),
      );
    });

    it("waits for both dates before requesting a custom range", async () => {
      const user = await renderLoaded();
      const callsBefore = api.get.mock.calls.length;

      await user.click(screen.getByRole("button", { name: "Date Range" }));

      expect(
        screen.getByText(/Pick a start and end date/i),
      ).toBeInTheDocument();
      expect(
        screen.getByRole("button", { name: "Show results" }),
      ).toBeDisabled();
      expect(api.get.mock.calls.length).toBe(callsBefore);
    });

    it("requests the custom range after Show results", async () => {
      const user = await renderLoaded();
      await user.click(screen.getByRole("button", { name: "Date Range" }));

      fireEvent.change(screen.getByLabelText("From"), {
        target: { value: "2025-01-06" },
      });
      fireEvent.change(screen.getByLabelText("To"), {
        target: { value: "2025-01-12" },
      });

      await user.click(screen.getByRole("button", { name: "Show results" }));

      await waitFor(() =>
        expect(lastParams()).toEqual({
          range: "custom",
          from: "2025-01-06",
          to: "2025-01-12",
          locationId: "1",
        }),
      );
    });

    it("keeps Show results disabled when the start is after the end", async () => {
      const user = await renderLoaded();
      await user.click(screen.getByRole("button", { name: "Date Range" }));

      fireEvent.change(screen.getByLabelText("From"), {
        target: { value: "2025-01-12" },
      });
      fireEvent.change(screen.getByLabelText("To"), {
        target: { value: "2025-01-06" },
      });

      expect(
        screen.getByRole("button", { name: "Show results" }),
      ).toBeDisabled();
    });
  });

  describe("Filters", () => {
    it("drops the location filter when the chip is removed, and can restore it", async () => {
      const user = await renderLoaded();

      const chip = screen.getByText("Location: North").closest(".MuiChip-root");
      await user.click(within(chip).getByTestId("CancelIcon"));

      await waitFor(() => expect(lastParams()).toEqual({ range: "last7" }));

      await user.click(
        screen.getByRole("button", { name: "Show my location only" }),
      );

      await waitFor(() =>
        expect(lastParams()).toMatchObject({ locationId: "1" }),
      );
    });

    it("has no category or tag filters", async () => {
      await renderLoaded();

      expect(
        screen.queryByRole("combobox", { name: /category|tag/i }),
      ).not.toBeInTheDocument();
    });
  });

  describe("Export report", () => {
    let downloads;

    beforeEach(() => {
      downloads = [];
      URL.createObjectURL = jest.fn(() => "blob:report");
      URL.revokeObjectURL = jest.fn();
      jest
        .spyOn(HTMLAnchorElement.prototype, "click")
        .mockImplementation(function recordDownload() {
          downloads.push(this.download);
        });
      window.print = jest.fn();
    });

    afterEach(() => {
      HTMLAnchorElement.prototype.click.mockRestore();
    });

    const chooseExport = async (user, option) => {
      await user.click(screen.getByRole("button", { name: "Export report" }));
      await user.click(screen.getByRole("menuitem", { name: option }));
    };

    const mockCanvas = () => {
      const getContext = jest
        .spyOn(HTMLCanvasElement.prototype, "getContext")
        .mockReturnValue({ fillRect: jest.fn(), drawImage: jest.fn() });
      const toDataURL = jest
        .spyOn(HTMLCanvasElement.prototype, "toDataURL")
        .mockReturnValue("data:image/png;base64,chart");
      return () => {
        getContext.mockRestore();
        toDataURL.mockRestore();
      };
    };

    it("downloads the summary as a CSV named after the report and period", async () => {
      const user = await renderLoaded();

      await chooseExport(user, /Summary \(CSV\)/);

      expect(URL.createObjectURL).toHaveBeenCalledWith(expect.any(Blob));
      expect(downloads).toEqual([
        "table-turnover_summary_2026-09-30_to_2026-10-06.csv",
      ]);
    });

    it("downloads seatings by hour as a CSV", async () => {
      const user = await renderLoaded();

      await chooseExport(user, /Seatings by hour \(CSV\)/);

      expect(downloads).toEqual([
        "table-turnover_seatings-by-hour_2026-09-30_to_2026-10-06.csv",
      ]);
    });

    it("downloads visit length by party size as a CSV", async () => {
      const user = await renderLoaded();

      await chooseExport(user, /Visit length by party size \(CSV\)/);

      expect(downloads).toEqual([
        "table-turnover_visit-length-by-party-size_2026-09-30_to_2026-10-06.csv",
      ]);
    });

    it("names the busiest time PNG after the measure shown", async () => {
      const restoreCanvas = mockCanvas();
      const user = await renderLoaded();

      await chooseExport(user, /Seatings by hour chart \(PNG\)/);
      await user.click(screen.getByRole("button", { name: "Guests" }));
      await chooseExport(user, /Seatings by hour chart \(PNG\)/);

      expect(downloads).toEqual([
        "table-turnover_seatings-by-hour_2026-09-30_to_2026-10-06.png",
        "table-turnover_guests-by-hour_2026-09-30_to_2026-10-06.png",
      ]);
      restoreCanvas();
    });

    it("downloads the visit length chart as a PNG", async () => {
      const restoreCanvas = mockCanvas();
      const user = await renderLoaded();

      await chooseExport(user, /Visit length chart \(PNG\)/);

      expect(downloads).toEqual([
        "table-turnover_visit-length-by-party-size_2026-09-30_to_2026-10-06.png",
      ]);
      restoreCanvas();
    });

    it("disables both chart images when nobody was seated", async () => {
      api.get.mockResolvedValue({ data: buildResponse(noSeatings()) });
      const user = await renderLoaded();

      await user.click(screen.getByRole("button", { name: "Export report" }));

      expect(
        screen.getByRole("menuitem", {
          name: /Seatings by hour chart \(PNG\)/,
        }),
      ).toHaveAttribute("aria-disabled", "true");
      expect(
        screen.getByRole("menuitem", { name: /Visit length chart \(PNG\)/ }),
      ).toHaveAttribute("aria-disabled", "true");
    });

    it("opens the print dialog for Print or save as PDF", async () => {
      const user = await renderLoaded();

      await chooseExport(user, /Print or save as PDF/);

      await waitFor(() => expect(window.print).toHaveBeenCalled());
    });

    it("hides Export report until results have loaded", async () => {
      api.get.mockReturnValue(new Promise(() => {}));
      render(<TableTurnover />);

      await screen.findByText(/Loading table turnover/i);
      expect(
        screen.queryByRole("button", { name: "Export report" }),
      ).not.toBeInTheDocument();
    });
  });

  describe("Error handling", () => {
    beforeEach(() => {
      jest.spyOn(console, "error").mockImplementation(() => {});
    });

    afterEach(() => {
      console.error.mockRestore();
    });

    it("shows validation messages from the API", async () => {
      api.get.mockRejectedValue({
        response: {
          status: 400,
          data: { errors: { from: ["from must be on or before to"] } },
        },
      });
      render(<TableTurnover />);

      expect(
        await screen.findByText("from must be on or before to"),
      ).toBeInTheDocument();
    });

    it("shows the server message on a 500", async () => {
      api.get.mockRejectedValue({
        response: {
          status: 500,
          data: { message: "An error occurred while loading table turnover." },
        },
      });
      render(<TableTurnover />);

      expect(
        await screen.findByText(
          "An error occurred while loading table turnover.",
        ),
      ).toBeInTheDocument();
    });

    it("shows a connection message when the server can't be reached", async () => {
      api.get.mockRejectedValue(new Error("Network Error"));
      render(<TableTurnover />);

      expect(
        await screen.findByText(/Couldn't reach the server/i),
      ).toBeInTheDocument();
    });

    it("shows a session message on a 401", async () => {
      api.get.mockRejectedValue({ response: { status: 401, data: {} } });
      render(<TableTurnover />);

      expect(
        await screen.findByText(/Your session has expired/i),
      ).toBeInTheDocument();
    });

    it("keeps the filters usable after an error", async () => {
      api.get.mockRejectedValue(new Error("Network Error"));
      render(<TableTurnover />);

      await screen.findByText(/Couldn't reach the server/i);
      expect(screen.getByRole("button", { name: "Yesterday" })).toBeEnabled();
    });
  });

  describe("Accessibility", () => {
    it("has a page heading", async () => {
      await renderLoaded();

      expect(
        screen.getByRole("heading", { level: 1, name: "Table turnover" }),
      ).toBeInTheDocument();
    });

    it("labels the date range buttons as a group", async () => {
      await renderLoaded();

      const group = screen.getByRole("group", { name: "Date range" });
      expect(within(group).getAllByRole("button")).toHaveLength(5);
    });

    it("labels the parties / guests switch and marks the selected one", async () => {
      await renderLoaded();

      const group = screen.getByRole("group", { name: "Busiest time measure" });
      expect(
        within(group).getByRole("button", { name: "Parties" }),
      ).toHaveAttribute("aria-pressed", "true");
    });
  });
});
