import {
  render,
  screen,
  waitFor,
  within,
  fireEvent,
} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import "@testing-library/jest-dom";
import OrderTiming from "../OrderTiming";
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

jest.mock("@mui/x-data-grid", () => {
  if (typeof global.TextEncoder === "undefined") {
    const { TextEncoder, TextDecoder } = jest.requireActual("util");
    Object.assign(global, { TextEncoder, TextDecoder });
  }
  const actual = jest.requireActual("@mui/x-data-grid");
  const React = jest.requireActual("react");
  return {
    ...actual,
    DataGrid: (props) =>
      React.createElement(actual.DataGrid, {
        ...props,
        disableVirtualization: true,
      }),
  };
});

jest.mock("react-chartjs-2", () => {
  const React = jest.requireActual("react");
  const Bar = React.forwardRef(function MockBar(props, ref) {
    React.useImperativeHandle(ref, () => ({
      canvas: document.createElement("canvas"),
    }));
    const dataset = props.data.datasets[0];
    return React.createElement("div", {
      role: "img",
      "aria-label": props["aria-label"],
      "data-testid": "orders-by-hour-chart",
      "data-labels": JSON.stringify(props.data.labels),
      "data-values": JSON.stringify(dataset.data),
      "data-colors": JSON.stringify(dataset.backgroundColor),
    });
  });
  return { __esModule: true, Bar };
});

if (typeof window.ResizeObserver === "undefined") {
  window.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
}

const ENDPOINT = "analytics/order-timing";

//  Test data (matches OrderTimingResponseDTO)

const duration = (count, average, median) => ({
  count,
  average_minutes: average,
  median_minutes: median,
});

const noDuration = () => duration(0, null, null);

const ordersByHour = (counts = { 12: 4, 17: 6, 18: 9, 19: 5 }) =>
  Array.from({ length: 24 }, (_, hour) => ({
    hour,
    label: `${String(hour).padStart(2, "0")}:00`,
    orders: counts[hour] ?? 0,
  }));

const makeItem = (overrides) => ({
  item_id: 0,
  name: "",
  category_id: 1,
  category_name: "Nigiri",
  tags: [],
  deliveries: 5,
  average_minutes: 0,
  median_minutes: 0,
  rank: 1,
  ...overrides,
});

const tempura = makeItem({
  item_id: 1,
  name: "Tempura",
  category_id: 2,
  category_name: "Hot Dishes",
  deliveries: 8,
  average_minutes: 19.4,
  median_minutes: 18,
  rank: 1,
});

const salmon = makeItem({
  item_id: 2,
  name: "Salmon",
  deliveries: 12,
  average_minutes: 4.6,
  median_minutes: 4,
  rank: 2,
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
    sessions_started: 12,
    sessions_open: 2,
    sessions_over_time_limit: 3,
    time_limit_minutes: 90,
    orders_placed: 24,
    orders_missing_approval_time: 0,
    items_ranked: 2,
    min_deliveries: 5,
  },
  session_duration: duration(10, 82.5, 80),
  peak_hour: { hour: 18, label: "18:00", orders: 9 },
  orders_by_hour: ordersByHour(),
  time_to_first_order: duration(10, 6.2, 5),
  gap_between_orders: duration(28, 18.4, 17),
  average_orders_per_session: 3.8,
  pending_to_processing: duration(24, 3.1, 2.5),
  processing_to_completed: duration(20, 14.2, 13),
  slowest_items: [tempura, salmon],
  fastest_items: [],
  filters: {
    categories: [
      { id: 2, name: "Hot Dishes" },
      { id: 1, name: "Nigiri" },
    ],
    tags: [{ tag_id: 7, name: "Spicy", color: "#FF0000" }],
  },
  ...overrides,
});

const withSummary = (summaryOverrides) => ({
  summary: { ...buildResponse().summary, ...summaryOverrides },
});

const lastParams = () => api.get.mock.calls.at(-1)[1].params;

const renderLoaded = async () => {
  const user = userEvent.setup();
  render(<OrderTiming />);
  await screen.findByText(/tables seated/i);
  return user;
};

const card = (label) => screen.getByText(label).closest(".MuiCard-root");

const grid = (title) => screen.getByRole("grid", { name: title });

const dataRows = (gridElement) =>
  within(gridElement)
    .getAllByRole("row")
    .filter((row) => row.hasAttribute("data-id"));

const cell = (row, field) => row.querySelector(`[data-field="${field}"]`);

const chart = () => screen.getByTestId("orders-by-hour-chart");
const chartData = (attribute) => JSON.parse(chart().getAttribute(attribute));

describe("OrderTiming", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    storage.get.mockReturnValue("1");
    api.get.mockResolvedValue({ data: buildResponse() });
  });

  describe("Data loading", () => {
    it("requests the last 7 full days for the saved location by default", async () => {
      render(<OrderTiming />);

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith(
          ENDPOINT,
          expect.objectContaining({
            params: { range: "last7", top: 5, locationId: "1" },
          }),
        );
      });
      expect(storage.get).toHaveBeenCalledWith("branch-location");
    });

    it("passes an abort signal so outdated requests can be cancelled", async () => {
      render(<OrderTiming />);

      await waitFor(() => expect(api.get).toHaveBeenCalled());
      expect(api.get.mock.calls[0][1].signal).toBeInstanceOf(AbortSignal);
    });

    it("omits locationId when no branch location is saved", async () => {
      storage.get.mockReturnValue(null);
      render(<OrderTiming />);

      await waitFor(() => expect(api.get).toHaveBeenCalled());
      expect(lastParams()).toEqual({ range: "last7", top: 5 });
      expect(screen.getByText("All locations")).toBeInTheDocument();
    });

    it("shows a loading message while the request is pending", async () => {
      api.get.mockReturnValue(new Promise(() => {}));
      render(<OrderTiming />);

      expect(
        await screen.findByText(/Loading order timing/i),
      ).toBeInTheDocument();
    });
  });

  describe("Summary", () => {
    it("shows the date range, time zone, tables and orders", async () => {
      await renderLoaded();

      expect(screen.getByText(/America\/Edmonton time/)).toBeInTheDocument();
      expect(
        screen.getByText(
          /12 tables seated \(2 still open\), 24 orders placed/i,
        ),
      ).toBeInTheDocument();
    });
  });

  describe("Visits", () => {
    it("shows the average visit with its median and count", async () => {
      await renderLoaded();

      expect(card("Average visit")).toHaveTextContent("82.5 min");
      expect(card("Average visit")).toHaveTextContent(
        "Median 80 min · 10 closed visits",
      );
    });

    it("shows visits over the time limit as a count and share of closed visits", async () => {
      await renderLoaded();

      expect(card("Over 90 minutes")).toHaveTextContent("3");
      expect(card("Over 90 minutes")).toHaveTextContent("30% of closed visits");
    });

    it("shows time to first order, gap between orders and orders per table", async () => {
      await renderLoaded();

      expect(card("Seated to first order")).toHaveTextContent("6.2 min");
      expect(card("Seated to first order")).toHaveTextContent("10 tables");
      expect(card("Gap between orders")).toHaveTextContent("18.4 min");
      expect(card("Gap between orders")).toHaveTextContent("28 gaps");
      expect(card("Orders per table")).toHaveTextContent("3.8");
    });

    it("shows dashes instead of 0 when nothing was measured", async () => {
      api.get.mockResolvedValue({
        data: buildResponse({
          session_duration: noDuration(),
          time_to_first_order: noDuration(),
          average_orders_per_session: null,
          ...withSummary({ sessions_over_time_limit: 0 }),
        }),
      });
      await renderLoaded();

      expect(card("Average visit")).toHaveTextContent("-");
      expect(card("Average visit")).toHaveTextContent("No data in this period");
      expect(card("Over 90 minutes")).toHaveTextContent(
        "No closed visits in this period",
      );
      expect(card("Seated to first order")).toHaveTextContent(
        "No data in this period",
      );
      expect(card("Orders per table")).toHaveTextContent("-");
    });
  });

  describe("Orders by hour", () => {
    it("names the busiest hour in restaurant time", async () => {
      await renderLoaded();

      expect(screen.getByText(/Busiest hour/)).toHaveTextContent(
        "Busiest hour: 6 PM with 9 orders.",
      );
    });

    it("charts only the hours from the first to the last order", async () => {
      await renderLoaded();

      expect(chartData("data-labels")).toEqual([
        "12 PM",
        "1 PM",
        "2 PM",
        "3 PM",
        "4 PM",
        "5 PM",
        "6 PM",
        "7 PM",
      ]);
      expect(chartData("data-values")).toEqual([4, 0, 0, 0, 0, 6, 9, 5]);
    });

    it("highlights the busiest hour in a different colour", async () => {
      await renderLoaded();

      const labels = chartData("data-labels");
      const colors = chartData("data-colors");
      const peakColor = colors[labels.indexOf("6 PM")];
      expect(colors.filter((c) => c === peakColor)).toHaveLength(1);
    });

    it("gives the chart an accessible description", async () => {
      await renderLoaded();

      expect(
        screen.getByRole("img", {
          name: "Orders by hour. Busiest hour 6 PM with 9 orders.",
        }),
      ).toBeInTheDocument();
    });

    it("shows a message instead of a chart when no orders were placed", async () => {
      api.get.mockResolvedValue({
        data: buildResponse({
          orders_by_hour: ordersByHour({}),
          peak_hour: null,
          ...withSummary({ orders_placed: 0 }),
        }),
      });
      await renderLoaded();

      expect(
        screen.getByText("No orders were placed in this period."),
      ).toBeInTheDocument();
      expect(
        screen.queryByTestId("orders-by-hour-chart"),
      ).not.toBeInTheDocument();
      expect(screen.queryByText(/Busiest hour/)).not.toBeInTheDocument();
    });
  });

  describe("Order status", () => {
    it("shows waiting for staff and kitchen times with median and count", async () => {
      await renderLoaded();

      const waiting = card("Pending to processing (waiting for staff)");
      expect(waiting).toHaveTextContent("3.1 min");
      expect(waiting).toHaveTextContent("Median 2.5 min · 24 orders");

      const kitchen = card("Processing to completed (kitchen and delivery)");
      expect(kitchen).toHaveTextContent("14.2 min");
      expect(kitchen).toHaveTextContent("Median 13 min · 20 orders");
    });

    it("doesn't mention missing approval times when there are none", async () => {
      await renderLoaded();

      expect(
        screen.queryByText(/before approval times were recorded/),
      ).not.toBeInTheDocument();
    });

    it("explains orders that were approved before approval times were recorded", async () => {
      api.get.mockResolvedValue({
        data: buildResponse(withSummary({ orders_missing_approval_time: 3 })),
      });
      await renderLoaded();

      expect(
        screen.getByText(
          /3 orders were approved before approval times were recorded/,
        ),
      ).toHaveTextContent("so they aren't included below.");
    });

    it("uses singular wording for one order without an approval time", async () => {
      api.get.mockResolvedValue({
        data: buildResponse(withSummary({ orders_missing_approval_time: 1 })),
      });
      await renderLoaded();

      expect(
        screen.getByText(
          /1 order was approved before approval times were recorded/,
        ),
      ).toHaveTextContent("so it isn't included below.");
    });
  });

  describe("Slowest and fastest items", () => {
    it("explains how items are timed and how many qualify", async () => {
      await renderLoaded();

      expect(
        screen.getByText(
          /Time from staff approval to the item being delivered/,
        ),
      ).toHaveTextContent(
        "Items need at least 5 deliveries to be ranked; 2 items qualify.",
      );
    });

    it("lists the slowest items with rank, category, deliveries and times", async () => {
      await renderLoaded();

      const [first, second] = dataRows(grid("Slowest 5 items"));
      expect(cell(first, "rank")).toHaveTextContent("#1");
      expect(cell(first, "name")).toHaveTextContent("Tempura");
      expect(cell(first, "category_name")).toHaveTextContent("Hot Dishes");
      expect(cell(first, "deliveries")).toHaveTextContent("8");
      expect(cell(first, "median_minutes")).toHaveTextContent("18 min");
      expect(cell(first, "average_minutes")).toHaveTextContent("19.4 min");
      expect(cell(second, "name")).toHaveTextContent("Salmon");
    });

    it("explains an empty fastest list when every ranked item is already in slowest", async () => {
      await renderLoaded();

      expect(
        within(grid("Fastest 5 items")).getByText(
          /Fewer than 10 items qualify, so every ranked item already appears in Slowest/,
        ),
      ).toBeInTheDocument();
    });

    it("explains an empty slowest list", async () => {
      api.get.mockResolvedValue({
        data: buildResponse({
          slowest_items: [],
          ...withSummary({ items_ranked: 0 }),
        }),
      });
      await renderLoaded();

      expect(
        within(grid("Slowest 5 items")).getByText(
          "No items had 5 or more deliveries in this period.",
        ),
      ).toBeInTheDocument();
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
          top: 5,
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

      await waitFor(() =>
        expect(lastParams()).toEqual({ range: "last7", top: 5 }),
      );

      await user.click(
        screen.getByRole("button", { name: "Show my location only" }),
      );

      await waitFor(() =>
        expect(lastParams()).toMatchObject({ locationId: "1" }),
      );
    });

    it("requests the chosen category", async () => {
      const user = await renderLoaded();

      await user.click(screen.getByRole("combobox", { name: /category/i }));
      await user.click(
        await screen.findByRole("option", { name: "Hot Dishes" }),
      );

      await waitFor(() =>
        expect(lastParams()).toMatchObject({ categoryId: 2 }),
      );
    });

    it("requests the chosen tag", async () => {
      const user = await renderLoaded();

      await user.click(screen.getByRole("combobox", { name: /tag/i }));
      await user.click(await screen.findByRole("option", { name: "Spicy" }));

      await waitFor(() => expect(lastParams()).toMatchObject({ tagId: 7 }));
    });

    it("explains that category and tag only filter the item lists", async () => {
      await renderLoaded();

      expect(
        screen.getByText(
          "Category and tag filter the slowest and fastest items only.",
        ),
      ).toBeInTheDocument();
    });
  });

  describe("Export report", () => {
    let downloads;

    beforeEach(() => {
      downloads = [];
      // jsdom can't create files or follow download links, so record the file names instead.
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

    it("downloads the summary as a CSV named after the report and period", async () => {
      const user = await renderLoaded();

      await chooseExport(user, /Summary \(CSV\)/);

      expect(URL.createObjectURL).toHaveBeenCalledWith(expect.any(Blob));
      expect(downloads).toEqual([
        "order-timing_summary_2026-09-30_to_2026-10-06.csv",
      ]);
    });

    it("downloads the orders by hour numbers as a CSV", async () => {
      const user = await renderLoaded();

      await chooseExport(user, /Orders by hour \(CSV\)/);

      expect(downloads).toEqual([
        "order-timing_orders-by-hour_2026-09-30_to_2026-10-06.csv",
      ]);
    });

    it("downloads the chart as a PNG", async () => {
      // jsdom has no canvas drawing, so stand in for the 2D context and image encoding.
      const getContext = jest
        .spyOn(HTMLCanvasElement.prototype, "getContext")
        .mockReturnValue({ fillRect: jest.fn(), drawImage: jest.fn() });
      const toDataURL = jest
        .spyOn(HTMLCanvasElement.prototype, "toDataURL")
        .mockReturnValue("data:image/png;base64,chart");
      const user = await renderLoaded();

      await chooseExport(user, /Orders by hour chart \(PNG\)/);

      expect(toDataURL).toHaveBeenCalledWith("image/png");
      expect(downloads).toEqual([
        "order-timing_orders-by-hour_2026-09-30_to_2026-10-06.png",
      ]);

      getContext.mockRestore();
      toDataURL.mockRestore();
    });

    it("disables the chart image when no orders were placed", async () => {
      api.get.mockResolvedValue({
        data: buildResponse({
          orders_by_hour: ordersByHour({}),
          peak_hour: null,
        }),
      });
      const user = await renderLoaded();

      await user.click(screen.getByRole("button", { name: "Export report" }));

      expect(
        screen.getByRole("menuitem", { name: /Orders by hour chart \(PNG\)/ }),
      ).toHaveAttribute("aria-disabled", "true");
    });

    it("opens the print dialog for Print or save as PDF", async () => {
      const user = await renderLoaded();

      await chooseExport(user, /Print or save as PDF/);

      await waitFor(() => expect(window.print).toHaveBeenCalled());
    });

    it("hides Export report until results have loaded", async () => {
      api.get.mockReturnValue(new Promise(() => {}));
      render(<OrderTiming />);

      await screen.findByText(/Loading order timing/i);
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
          data: { errors: { top: ["top must be between 1 and 25"] } },
        },
      });
      render(<OrderTiming />);

      expect(
        await screen.findByText("top must be between 1 and 25"),
      ).toBeInTheDocument();
    });

    it("shows the server message on a 500", async () => {
      api.get.mockRejectedValue({
        response: {
          status: 500,
          data: { message: "An error occurred while loading order timing." },
        },
      });
      render(<OrderTiming />);

      expect(
        await screen.findByText(
          "An error occurred while loading order timing.",
        ),
      ).toBeInTheDocument();
    });

    it("shows a connection message when the server can't be reached", async () => {
      api.get.mockRejectedValue(new Error("Network Error"));
      render(<OrderTiming />);

      expect(
        await screen.findByText(/Couldn't reach the server/i),
      ).toBeInTheDocument();
    });

    it("shows a session message on a 401", async () => {
      api.get.mockRejectedValue({ response: { status: 401, data: {} } });
      render(<OrderTiming />);

      expect(
        await screen.findByText(/Your session has expired/i),
      ).toBeInTheDocument();
    });

    it("keeps the filters usable after an error", async () => {
      api.get.mockRejectedValue(new Error("Network Error"));
      render(<OrderTiming />);

      await screen.findByText(/Couldn't reach the server/i);
      expect(screen.getByRole("button", { name: "Yesterday" })).toBeEnabled();
    });
  });

  describe("Accessibility", () => {
    it("labels the date range buttons as a group", async () => {
      await renderLoaded();

      const group = screen.getByRole("group", { name: "Date range" });
      expect(within(group).getAllByRole("button")).toHaveLength(5);
    });

    it("marks the selected range as pressed", async () => {
      await renderLoaded();

      expect(
        screen.getByRole("button", { name: "Last 7 full days before today" }),
      ).toHaveAttribute("aria-pressed", "true");
    });

    it("has a page heading", async () => {
      await renderLoaded();

      expect(
        screen.getByRole("heading", { level: 1, name: "Order timing" }),
      ).toBeInTheDocument();
    });

    it("labels both item tables with their titles", async () => {
      await renderLoaded();

      expect(grid("Slowest 5 items")).toBeInTheDocument();
      expect(grid("Fastest 5 items")).toBeInTheDocument();
    });
  });
});
