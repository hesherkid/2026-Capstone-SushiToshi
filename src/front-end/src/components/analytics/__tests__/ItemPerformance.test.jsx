import {
  render,
  screen,
  waitFor,
  within,
  fireEvent,
} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import "@testing-library/jest-dom";
import ItemPerformance from "../ItemPerformance";
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

if (typeof window.ResizeObserver === "undefined") {
  window.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
}

const ENDPOINT = "analytics/item-performance";

//  Test data (matches ItemPerformanceResponseDTO)

const makeItem = (overrides) => ({
  item_id: 0,
  name: "",
  category_id: 1,
  category_name: "Nigiri",
  status: "Available",
  currently_available: true,
  tags: [],
  units_sold: 0,
  add_on_units: 0,
  add_on_share: 0,
  add_on_days: 0,
  share_of_units: 0,
  rank: null,
  ...overrides,
});

const salmon = makeItem({
  item_id: 1,
  name: "Salmon",
  units_sold: 12,
  share_of_units: 50,
  rank: 1,
});
const tuna = makeItem({
  item_id: 2,
  name: "Tuna",
  units_sold: 8,
  share_of_units: 33.3,
  rank: 2,
  status: "Unavailable",
  currently_available: false,
});
const mochi = makeItem({
  item_id: 3,
  name: "Mochi",
  category_id: 2,
  category_name: "Desserts",
  units_sold: 4,
  share_of_units: 16.7,
  rank: 3,
  add_on_units: 4,
  add_on_share: 100,
  add_on_days: 2,
});
const seasonalRoll = makeItem({
  item_id: 4,
  name: "Seasonal Roll",
  status: "Seasonal",
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
    total_units_sold: 24,
    total_add_on_units: 4,
    items_considered: 4,
    items_with_sales: 3,
    items_with_zero_sales: 1,
  },
  best: [salmon, tuna, mochi],
  worst: [],
  zero_sales: [seasonalRoll],
  add_ons: [mochi],
  items: [salmon, tuna, mochi, seasonalRoll],
  filters: {
    categories: [
      { id: 2, name: "Desserts" },
      { id: 1, name: "Nigiri" },
    ],
    tags: [{ tag_id: 7, name: "Spicy", color: "#FF0000" }],
  },
  ...overrides,
});

const lastParams = () => api.get.mock.calls.at(-1)[1].params;

const renderLoaded = async () => {
  const user = userEvent.setup();
  render(<ItemPerformance />);
  await screen.findByText(/units sold across/i);
  return user;
};

const grid = (title) => screen.getByRole("grid", { name: title });
const allItemsGrid = () => grid("All items");

const dataRows = (gridElement) =>
  within(gridElement)
    .getAllByRole("row")
    .filter((row) => row.hasAttribute("data-id"));

const cell = (row, field) => row.querySelector(`[data-field="${field}"]`);

describe("ItemPerformance", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    storage.get.mockReturnValue("1");
    api.get.mockResolvedValue({ data: buildResponse() });
  });

  describe("Data loading", () => {
    it("requests the last 7 full days for the saved location by default", async () => {
      render(<ItemPerformance />);

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
      render(<ItemPerformance />);

      await waitFor(() => expect(api.get).toHaveBeenCalled());
      expect(api.get.mock.calls[0][1].signal).toBeInstanceOf(AbortSignal);
    });

    it("omits locationId when no branch location is saved", async () => {
      storage.get.mockReturnValue(null);
      render(<ItemPerformance />);

      await waitFor(() => expect(api.get).toHaveBeenCalled());
      expect(lastParams()).toEqual({ range: "last7", top: 5 });
      expect(screen.getByText("All locations")).toBeInTheDocument();
    });

    it("shows a loading message while the request is pending", async () => {
      api.get.mockReturnValue(new Promise(() => {}));
      render(<ItemPerformance />);

      expect(
        await screen.findByText(/Loading item performance/i),
      ).toBeInTheDocument();
    });
  });

  describe("Rendering results", () => {
    it("shows the date range, time zone and summary", async () => {
      await renderLoaded();

      expect(screen.getByText(/America\/Edmonton time/)).toBeInTheDocument();
      expect(
        screen.getByText(
          /24 units sold across 3 of 4 items, 4 of them as add-ons/i,
        ),
      ).toBeInTheDocument();
    });

    it("shows the top items with their rank and units", async () => {
      await renderLoaded();

      const [first] = dataRows(grid("Top 5 items"));
      expect(cell(first, "rank")).toHaveTextContent("#1");
      expect(cell(first, "name")).toHaveTextContent("Salmon");
      expect(cell(first, "units_sold")).toHaveTextContent("12");
    });

    it("explains an empty bottom list when every seller is already in the top list", async () => {
      await renderLoaded();

      expect(
        within(grid("Bottom 5 items (with sales)")).getByText(
          /Fewer than 10 items sold, so every seller already appears in the top list/i,
        ),
      ).toBeInTheDocument();
    });

    it("lists zero-sales items in their own section", async () => {
      await renderLoaded();

      expect(screen.getByText("1 item with no sales")).toBeInTheDocument();
      expect(screen.getByText("Seasonal Roll (seasonal)")).toBeInTheDocument();
    });

    it("badges seasonal and no-longer-available items", async () => {
      await renderLoaded();

      expect(screen.getAllByText("Seasonal").length).toBeGreaterThan(0);
      expect(screen.getAllByText("No longer available").length).toBeGreaterThan(
        0,
      );
    });

    it("shows add-on items with how many days they were added on", async () => {
      await renderLoaded();

      const [mochiRow] = dataRows(grid("Add-on items"));
      expect(cell(mochiRow, "name")).toHaveTextContent("Mochi");
      expect(cell(mochiRow, "add_on_days")).toHaveTextContent("2 of 7");
      expect(cell(mochiRow, "add_on_share")).toHaveTextContent("100%");
    });

    it("shows a message when there are no add-on sales", async () => {
      api.get.mockResolvedValue({ data: buildResponse({ add_ons: [] }) });
      await renderLoaded();

      expect(
        within(grid("Add-on items")).getByText(
          "No add-on sales in this period.",
        ),
      ).toBeInTheDocument();
    });

    it("shows a message when no items match the filters", async () => {
      api.get.mockResolvedValue({
        data: buildResponse({
          items: [],
          best: [],
          zero_sales: [],
          add_ons: [],
        }),
      });
      await renderLoaded();

      expect(
        within(allItemsGrid()).getByText(/No items match these filters/i),
      ).toBeInTheDocument();
    });

    it("shows each item's status in the all items table", async () => {
      await renderLoaded();

      const rows = dataRows(allItemsGrid());
      const statusOf = (name) =>
        cell(
          rows.find((row) => cell(row, "name").textContent === name),
          "availability",
        );
      expect(statusOf("Salmon")).toHaveTextContent("Available");
      expect(statusOf("Tuna")).toHaveTextContent("No longer available");
      expect(statusOf("Seasonal Roll")).toHaveTextContent("Seasonal");
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

    it("requests today when Today is selected", async () => {
      const user = await renderLoaded();

      await user.click(screen.getByRole("button", { name: "Today" }));

      await waitFor(() =>
        expect(lastParams()).toMatchObject({ range: "today" }),
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

      const showResults = screen.getByRole("button", { name: "Show results" });
      expect(showResults).toBeEnabled();
      await user.click(showResults);

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

  describe("Location filter", () => {
    it("shows the location name returned by the API", async () => {
      await renderLoaded();

      expect(screen.getByText("Location: North")).toBeInTheDocument();
    });

    it("drops the location filter when the chip is removed, and can restore it", async () => {
      const user = await renderLoaded();

      const chip = screen.getByText("Location: North").closest(".MuiChip-root");
      await user.click(within(chip).getByTestId("CancelIcon"));

      await waitFor(() =>
        expect(lastParams()).toEqual({ range: "last7", top: 5 }),
      );
      expect(screen.getByText("All locations")).toBeInTheDocument();

      await user.click(
        screen.getByRole("button", { name: "Show my location only" }),
      );

      await waitFor(() =>
        expect(lastParams()).toMatchObject({ locationId: "1" }),
      );
    });
  });

  describe("Category and tag filters", () => {
    it("requests the chosen category", async () => {
      const user = await renderLoaded();

      await user.click(screen.getByRole("combobox", { name: /category/i }));
      await user.click(await screen.findByRole("option", { name: "Desserts" }));

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
  });

  describe("Item finder", () => {
    it("shows rank and share when a sold item is selected", async () => {
      const user = await renderLoaded();

      await user.click(within(allItemsGrid()).getByText("Salmon"));

      expect(
        screen.getByText(/Rank #1 of 3 items with sales/),
      ).toBeInTheDocument();
      expect(
        screen.getByText(/12 units, 50% of units sold/),
      ).toBeInTheDocument();
    });

    it("includes add-on details for add-on items", async () => {
      const user = await renderLoaded();

      await user.click(within(allItemsGrid()).getByText("Mochi"));

      expect(
        screen.getByText(/4 units \(100%\) were add-ons, on 2 of 7 days/),
      ).toBeInTheDocument();
    });

    it("explains when a selected item is no longer available", async () => {
      const user = await renderLoaded();

      await user.click(within(allItemsGrid()).getByText("Tuna"));

      expect(
        screen.getByText(/no longer available but sold during this period/i),
      ).toBeInTheDocument();
    });

    it("selects an item when a row in a short list is clicked", async () => {
      const user = await renderLoaded();

      await user.click(within(grid("Top 5 items")).getByText("Tuna"));

      expect(
        screen.getByText(/Rank #2 of 3 items with sales/),
      ).toBeInTheDocument();
    });

    it("says when a selected item had no sales", async () => {
      const user = await renderLoaded();

      await user.click(screen.getByText("Seasonal Roll (seasonal)"));

      expect(screen.getByText(/No sales in this period/)).toBeInTheDocument();
    });

    it("finds an item by name with the search box", async () => {
      const user = await renderLoaded();

      await user.type(
        screen.getByRole("combobox", { name: /find an item/i }),
        "Tun",
      );
      await user.click(await screen.findByRole("option", { name: "Tuna" }));

      expect(
        screen.getByText(/Rank #2 of 3 items with sales/),
      ).toBeInTheDocument();
    });
  });

  describe("Sorting", () => {
    it("lists all items in rank order by default, with unranked items last", async () => {
      await renderLoaded();

      const rows = dataRows(allItemsGrid());
      expect(cell(rows[0], "name")).toHaveTextContent("Salmon");
      expect(cell(rows.at(-1), "name")).toHaveTextContent("Seasonal Roll");
      expect(cell(rows.at(-1), "rank")).toHaveTextContent("None");
    });

    it("toggles units sold between ascending and descending", async () => {
      const user = await renderLoaded();
      const header = within(allItemsGrid()).getByRole("columnheader", {
        name: /Units sold/,
      });

      await user.click(header);
      expect(cell(dataRows(allItemsGrid())[0], "name")).toHaveTextContent(
        "Seasonal Roll",
      );

      await user.click(header);
      expect(cell(dataRows(allItemsGrid())[0], "name")).toHaveTextContent(
        "Salmon",
      );
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

    it("downloads the summary as a CSV named after the report and period", async () => {
      const user = await renderLoaded();

      await user.click(screen.getByRole("button", { name: "Export report" }));
      await user.click(
        screen.getByRole("menuitem", { name: /Summary \(CSV\)/ }),
      );

      expect(URL.createObjectURL).toHaveBeenCalledWith(expect.any(Blob));
      expect(downloads).toEqual([
        "item-performance_summary_2026-09-30_to_2026-10-06.csv",
      ]);
    });

    it("opens the print dialog for Print or save as PDF", async () => {
      const user = await renderLoaded();

      await user.click(screen.getByRole("button", { name: "Export report" }));
      await user.click(
        screen.getByRole("menuitem", { name: /Print or save as PDF/ }),
      );

      await waitFor(() => expect(window.print).toHaveBeenCalled());
    });

    it("hides Export report until results have loaded", async () => {
      api.get.mockReturnValue(new Promise(() => {}));
      render(<ItemPerformance />);

      await screen.findByText(/Loading item performance/i);
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
          data: { errors: { to: ["The end date can't be in the future."] } },
        },
      });
      render(<ItemPerformance />);

      expect(
        await screen.findByText("The end date can't be in the future."),
      ).toBeInTheDocument();
    });

    it("shows the server message on a 500", async () => {
      api.get.mockRejectedValue({
        response: {
          status: 500,
          data: {
            message: "An error occurred while loading item performance.",
          },
        },
      });
      render(<ItemPerformance />);

      expect(
        await screen.findByText(
          "An error occurred while loading item performance.",
        ),
      ).toBeInTheDocument();
    });

    it("shows a connection message when the server can't be reached", async () => {
      api.get.mockRejectedValue(new Error("Network Error"));
      render(<ItemPerformance />);

      expect(
        await screen.findByText(/Couldn't reach the server/i),
      ).toBeInTheDocument();
    });

    it("shows a session message on a 401", async () => {
      api.get.mockRejectedValue({ response: { status: 401, data: {} } });
      render(<ItemPerformance />);

      expect(
        await screen.findByText(/Your session has expired/i),
      ).toBeInTheDocument();
    });

    it("keeps the filters usable after an error", async () => {
      api.get.mockRejectedValue(new Error("Network Error"));
      render(<ItemPerformance />);

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
        screen.getByRole("heading", { level: 1, name: "Item performance" }),
      ).toBeInTheDocument();
    });

    it("labels the item search box", async () => {
      await renderLoaded();

      expect(
        screen.getByRole("combobox", { name: /find an item/i }),
      ).toBeInTheDocument();
    });

    it("labels every table with its title", async () => {
      await renderLoaded();

      for (const title of [
        "Top 5 items",
        "Bottom 5 items (with sales)",
        "Add-on items",
        "All items",
      ]) {
        expect(grid(title)).toBeInTheDocument();
      }
    });
  });
});
