import {
  render,
  screen,
  waitFor,
  within,
  fireEvent,
} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import "@testing-library/jest-dom";
import BrowsingBehavior from "../BrowsingBehavior";
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

const ENDPOINT = "analytics/browsing-behavior";

const makeItem = (overrides) => ({
  item_id: 0,
  name: "",
  category_id: 1,
  category_name: "Nigiri",
  status: "Available",
  currently_available: true,
  tags: [],
  views: 0,
  view_rank: null,
  short_views: 0,
  total_view_seconds: 0,
  average_view_seconds: null,
  viewing_sessions: 0,
  ordering_sessions: 0,
  conversion_rate: null,
  units_ordered_after_view: 0,
  average_view_seconds_ordered: null,
  average_view_seconds_not_ordered: null,
  unavailable_views: 0,
  ...overrides,
});

const salmon = makeItem({
  item_id: 1,
  name: "Salmon",
  views: 6,
  view_rank: 1,
  total_view_seconds: 75,
  average_view_seconds: 12.5,
  viewing_sessions: 3,
  ordering_sessions: 2,
  conversion_rate: 66.7,
  units_ordered_after_view: 4,
  average_view_seconds_ordered: 15,
  average_view_seconds_not_ordered: 7.5,
});

const tuna = makeItem({
  item_id: 2,
  name: "Tuna",
  status: "Unavailable",
  currently_available: false,
  views: 4,
  view_rank: 2,
  total_view_seconds: 32,
  average_view_seconds: 8,
  viewing_sessions: 2,
  ordering_sessions: 0,
  conversion_rate: 0,
  average_view_seconds_not_ordered: 8,
  unavailable_views: 2,
});

const mochi = makeItem({
  item_id: 3,
  name: "Mochi",
  category_id: 2,
  category_name: "Desserts",
  views: 2,
  view_rank: 3,
  total_view_seconds: 40,
  average_view_seconds: 20,
  viewing_sessions: 1,
  ordering_sessions: 1,
  conversion_rate: 100,
  units_ordered_after_view: 1,
  average_view_seconds_ordered: 20,
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
    total_views: 12,
    short_views_excluded: 3,
    total_view_seconds: 147,
    average_view_seconds: 12.3,
    average_view_seconds_ordered: 16,
    average_view_seconds_not_ordered: 7.8,
    viewing_sessions: 3,
    session_item_views: 6,
    session_item_orders: 3,
    conversion_rate: 50,
    items_considered: 4,
    items_viewed: 3,
    items_never_viewed: 1,
    unavailable_views: 2,
    min_view_seconds: 5,
    min_views: 10,
  },
  most_viewed: [salmon, tuna, mochi],
  least_viewed: [],
  never_viewed: [seasonalRoll],
  viewed_not_ordered: [tuna],
  top_viewed_not_ordered: [tuna],
  top_viewed_and_ordered: [salmon, mochi],
  low_conversion_high_views: [],
  unavailable_views: [tuna],
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
  const user = userEvent.setup({ delay: null });
  render(<BrowsingBehavior />);
  await screen.findByText(/views from 3 tables/i);
  return user;
};

const grid = (title) => screen.getByRole("grid", { name: title });
const allItemsGrid = () => grid("All items");

const dataRows = (gridElement) =>
  within(gridElement)
    .getAllByRole("row")
    .filter((row) => row.hasAttribute("data-id"));

const cell = (row, field) => row.querySelector(`[data-field="${field}"]`);

const rowFor = (gridElement, name) =>
  within(gridElement).getByText(name).closest('[role="row"]');

describe("BrowsingBehavior", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    storage.get.mockReturnValue("1");
    api.get.mockResolvedValue({ data: buildResponse() });
  });

  describe("Data loading", () => {
    it("requests the last 7 full days for the saved location by default", async () => {
      render(<BrowsingBehavior />);

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith(
          ENDPOINT,
          expect.objectContaining({
            params: { range: "last7", top: 5, minViews: 10, locationId: "1" },
          }),
        );
      });
      expect(storage.get).toHaveBeenCalledWith("branch-location");
    });

    it("passes an abort signal so outdated requests can be cancelled", async () => {
      render(<BrowsingBehavior />);

      await waitFor(() => expect(api.get).toHaveBeenCalled());
      expect(api.get.mock.calls[0][1].signal).toBeInstanceOf(AbortSignal);
    });

    it("omits locationId when no branch location is saved", async () => {
      storage.get.mockReturnValue(null);
      render(<BrowsingBehavior />);

      await waitFor(() => expect(api.get).toHaveBeenCalled());
      expect(lastParams()).toEqual({ range: "last7", top: 5, minViews: 10 });
      expect(screen.getByText("All locations")).toBeInTheDocument();
    });

    it("shows a loading message while the request is pending", async () => {
      api.get.mockReturnValue(new Promise(() => {}));
      render(<BrowsingBehavior />);

      expect(
        await screen.findByText(/Loading browsing behavior/i),
      ).toBeInTheDocument();
    });
  });

  describe("Rendering results", () => {
    it("shows the date range, time zone and view totals", async () => {
      await renderLoaded();

      expect(screen.getByText(/America\/Edmonton time/)).toBeInTheDocument();
      expect(
        screen.getByText(/12 views from 3 tables across 3 of 4 items/i),
      ).toBeInTheDocument();
      expect(screen.getByText(/\(3 left out\)/)).toBeInTheDocument();
    });

    it("shows overall conversion and average view time, ordered vs not", async () => {
      await renderLoaded();

      expect(
        screen.getByText(
          /50% of item views led to an order at the same table/i,
        ),
      ).toBeInTheDocument();
      expect(
        screen.getByText(/16s when ordered, 7.8s when not/i),
      ).toBeInTheDocument();
    });

    it("shows the most viewed items with rank and views", async () => {
      await renderLoaded();

      const [first] = dataRows(grid("Most viewed"));
      expect(cell(first, "name")).toHaveTextContent("Salmon");
      expect(cell(first, "view_rank")).toHaveTextContent("#1");
      expect(cell(first, "views")).toHaveTextContent("6");
    });

    it("explains an empty least viewed list when every viewed item is already in most viewed", async () => {
      await renderLoaded();

      expect(
        within(grid("Least viewed (excluding zero views)")).getByText(
          /Fewer than 10 items were viewed, so every viewed item already appears in Most viewed/i,
        ),
      ).toBeInTheDocument();
    });

    it("shows viewed and ordered items with tables and conversion", async () => {
      await renderLoaded();

      const [first] = dataRows(grid("Top 5 viewed and ordered"));
      expect(cell(first, "name")).toHaveTextContent("Salmon");
      expect(cell(first, "ordering_sessions")).toHaveTextContent("2");
      expect(cell(first, "conversion_rate")).toHaveTextContent("66.7%");
    });

    it("shows viewed but not ordered items and the total count", async () => {
      await renderLoaded();

      expect(
        within(grid("Top 5 viewed but not ordered")).getByText("Tuna"),
      ).toBeInTheDocument();
      expect(
        screen.getByText("1 items in total were viewed but not ordered."),
      ).toBeInTheDocument();
    });

    it("explains the high views threshold and an empty low conversion list", async () => {
      await renderLoaded();

      expect(
        screen.getByText(
          "Items with at least 10 views, lowest conversion first.",
        ),
      ).toBeInTheDocument();
      expect(
        within(grid("Low conversion with high views")).getByText(
          "No item reached 10 views in this period.",
        ),
      ).toBeInTheDocument();
    });

    it("lists never viewed items in their own section", async () => {
      await renderLoaded();

      expect(screen.getByText("1 item never viewed")).toBeInTheDocument();
      expect(screen.getByText("Seasonal Roll (seasonal)")).toBeInTheDocument();
    });

    it("shows views made while an item was unavailable", async () => {
      await renderLoaded();

      const row = rowFor(grid("Views of unavailable items"), "Tuna");
      expect(cell(row, "unavailable_views")).toHaveTextContent("2");
      expect(cell(row, "available_now")).toHaveTextContent("Unavailable");
    });

    it("badges seasonal and not-available-now items", async () => {
      await renderLoaded();

      expect(screen.getAllByText("Seasonal").length).toBeGreaterThan(0);
      expect(screen.getAllByText("Not available now").length).toBeGreaterThan(
        0,
      );
    });

    it("shows a dash instead of 0 for items with nothing to calculate", async () => {
      await renderLoaded();

      const row = rowFor(allItemsGrid(), "Seasonal Roll");
      expect(cell(row, "view_rank")).toHaveTextContent("None");
      expect(cell(row, "average_view_seconds")).toHaveTextContent("-");
      expect(cell(row, "conversion_rate")).toHaveTextContent("-");
    });

    it("shows a message when no items match the filters", async () => {
      api.get.mockResolvedValue({
        data: buildResponse({
          items: [],
          most_viewed: [],
          never_viewed: [],
          viewed_not_ordered: [],
          top_viewed_not_ordered: [],
          top_viewed_and_ordered: [],
          unavailable_views: [],
        }),
      });
      await renderLoaded();

      expect(
        within(allItemsGrid()).getByText(/No items match these filters/i),
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
          minViews: 10,
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
        expect(lastParams()).toEqual({ range: "last7", top: 5, minViews: 10 }),
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

    it("requests the chosen high views threshold", async () => {
      const user = await renderLoaded();

      await user.click(
        screen.getByRole("combobox", { name: /high views starts at/i }),
      );
      await user.click(await screen.findByRole("option", { name: "20 views" }));

      await waitFor(() => expect(lastParams()).toMatchObject({ minViews: 20 }));
    });
  });

  describe("Item finder", () => {
    it("shows rank, tables and conversion when a viewed item is selected", async () => {
      const user = await renderLoaded();

      await user.click(within(allItemsGrid()).getByText("Salmon"));

      expect(screen.getByText(/Rank #1 of 3 viewed items/)).toBeInTheDocument();
      expect(
        screen.getByText(
          /6 views by 3 tables, 2 of which ordered it \(66.7%\)/,
        ),
      ).toBeInTheDocument();
    });

    it("mentions views made while the item was unavailable", async () => {
      const user = await renderLoaded();

      await user.click(within(allItemsGrid()).getByText("Tuna"));

      expect(screen.getByText(/2 views while unavailable/)).toBeInTheDocument();
    });

    it("says when a selected item was not viewed", async () => {
      const user = await renderLoaded();

      await user.click(screen.getByText("Seasonal Roll (seasonal)"));

      expect(screen.getByText(/Not viewed in this period/)).toBeInTheDocument();
    });

    it("selects an item when a row in a list is clicked", async () => {
      const user = await renderLoaded();

      await user.click(
        within(grid("Top 5 viewed and ordered")).getByText("Mochi"),
      );

      expect(screen.getByText(/Rank #3 of 3 viewed items/)).toBeInTheDocument();
    });

    it("finds an item by name with the search box", async () => {
      const user = await renderLoaded();

      await user.type(
        screen.getByRole("combobox", { name: /find an item/i }),
        "Tun",
      );
      await user.click(await screen.findByRole("option", { name: "Tuna" }));

      expect(screen.getByText(/Rank #2 of 3 viewed items/)).toBeInTheDocument();
    });
  });

  describe("Sorting", () => {
    it("lists all items in rank order by default, with unviewed items last", async () => {
      await renderLoaded();

      const rows = dataRows(allItemsGrid());
      expect(cell(rows[0], "name")).toHaveTextContent("Salmon");
      expect(cell(rows.at(-1), "name")).toHaveTextContent("Seasonal Roll");
    });

    it("toggles views between ascending and descending", async () => {
      const user = await renderLoaded();
      const header = within(allItemsGrid()).getByRole("columnheader", {
        name: /^Views/,
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

    it("sorts by conversion with not-viewed items lowest", async () => {
      const user = await renderLoaded();

      await user.click(
        within(allItemsGrid()).getByRole("columnheader", {
          name: /Conversion/,
        }),
      );

      const rows = dataRows(allItemsGrid());
      expect(cell(rows[0], "name")).toHaveTextContent("Seasonal Roll");
      expect(cell(rows[1], "name")).toHaveTextContent("Tuna");
      expect(cell(rows.at(-1), "name")).toHaveTextContent("Mochi");
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
        "browsing-behavior_summary_2026-09-30_to_2026-10-06.csv",
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
          data: {
            errors: { minViews: ["minViews must be between 1 and 1000"] },
          },
        },
      });
      render(<BrowsingBehavior />);

      expect(
        await screen.findByText("minViews must be between 1 and 1000"),
      ).toBeInTheDocument();
    });

    it("shows the server message on a 500", async () => {
      api.get.mockRejectedValue({
        response: {
          status: 500,
          data: {
            message: "An error occurred while loading browsing behavior.",
          },
        },
      });
      render(<BrowsingBehavior />);

      expect(
        await screen.findByText(
          "An error occurred while loading browsing behavior.",
        ),
      ).toBeInTheDocument();
    });

    it("shows a connection message when the server can't be reached", async () => {
      api.get.mockRejectedValue(new Error("Network Error"));
      render(<BrowsingBehavior />);

      expect(
        await screen.findByText(/Couldn't reach the server/i),
      ).toBeInTheDocument();
    });

    it("shows a session message on a 401", async () => {
      api.get.mockRejectedValue({ response: { status: 401, data: {} } });
      render(<BrowsingBehavior />);

      expect(
        await screen.findByText(/Your session has expired/i),
      ).toBeInTheDocument();
    });

    it("keeps the filters usable after an error", async () => {
      api.get.mockRejectedValue(new Error("Network Error"));
      render(<BrowsingBehavior />);

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
        screen.getByRole("heading", { level: 1, name: "Browsing behavior" }),
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
        "Most viewed",
        "Least viewed (excluding zero views)",
        "Top 5 viewed and ordered",
        "Top 5 viewed but not ordered",
        "Low conversion with high views",
        "Views of unavailable items",
        "All items",
      ]) {
        expect(grid(title)).toBeInTheDocument();
      }
    });
  });
});
