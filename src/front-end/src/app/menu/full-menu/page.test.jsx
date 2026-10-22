import React from "react";
import { render as rtlRender, screen, waitFor, fireEvent } from "@testing-library/react";
import FullMenu from "./page";
import { TableOrderProvider } from "@/contexts/TableOrderContext";
import { tableApi, MISO_SOUP, SALMON_ROLL } from "../_testing/tableApiMock";

const mockPush = jest.fn();
jest.mock("next/navigation", () => ({
  useRouter: () => ({ push: mockPush, back: jest.fn() }),
}));

// What's typed in the search box; set before rendering
let mockSearchTerm = "";
jest.mock("@/components/menu/searchUtils", () => ({
  __esModule: true,
  default: () => ({
    searchTerm: mockSearchTerm,
    setSearchTerm: jest.fn(),
    searchResults: [],
  }),
}));

jest.mock("@/config/api", () => ({
  __esModule: true,
  default: {
    get: jest.fn(),
  },
}));

const api = require("@/config/api").default;

// The page reads its table, menu and cart from TableOrderContext
const render = (ui) => rtlRender(<TableOrderProvider>{ui}</TableOrderProvider>);

describe("FullMenu guest flow", () => {
  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem("guest", "true");
    localStorage.setItem("session_id", "42");
    mockSearchTerm = "";
    mockPush.mockReset();
    // jsdom doesn't scroll; the category bar scrolls to sections
    window.scrollTo = jest.fn();
    // Guests call the API with their own guest token, through the regular api client
    api.get.mockReset();
    api.get.mockImplementation(tableApi());
  });

  it("shows the menu items and supports category filtering", async () => {
    render(<FullMenu />);

    await waitFor(() => {
      expect(screen.getByText("Miso Soup")).toBeInTheDocument();
    });

    // Items are requested for the session's location
    expect(api.get).toHaveBeenCalledWith("/Menu/7/item", {
      params: { locationId: 3 },
    });

    expect(
      screen.getByText(/there is no open bill for this session yet/i),
    ).toBeInTheDocument();

    // Picking an item shows the Review order button with the count
    fireEvent.click(screen.getByRole("button", { name: "Add one Miso Soup" }));
    expect(
      await screen.findByRole("button", { name: "Review order, 1 item" }),
    ).toBeInTheDocument();

    // Every section stays on the page; a chip scrolls to its section and lights up
    expect(screen.queryByRole("button", { name: "All" })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Appetizers" }));
    expect(window.scrollTo).toHaveBeenLastCalledWith(
      expect.objectContaining({ behavior: "smooth" }),
    );
    expect(screen.getByRole("button", { name: "Appetizers" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: "Sushi Rolls" })).toHaveAttribute("aria-pressed", "false");
    expect(screen.getByRole("heading", { name: "Appetizers 1 items" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Sushi Rolls 1 items" })).toBeInTheDocument();
    expect(screen.getByText("Salmon Roll")).toBeInTheDocument();

    // The cart is its own page now
    fireEvent.click(screen.getByRole("button", { name: "Review order, 1 item" }));
    expect(mockPush).toHaveBeenCalledWith("/menu/review");
  });

  it("lights up the category whose section is on screen as the guest scrolls", async () => {
    render(<FullMenu />);
    await screen.findByText("Salmon Roll");

    const placeSections = (appetizersTop, rollsTop) => {
      document.getElementById("category-1").getBoundingClientRect = () => ({ top: appetizersTop });
      document.getElementById("category-2").getBoundingClientRect = () => ({ top: rollsTop });
      fireEvent.scroll(window);
    };

    placeSections(-300, 600);
    expect(screen.getByRole("button", { name: "Appetizers" })).toHaveAttribute("aria-pressed", "true");

    placeSections(-900, 40);
    expect(screen.getByRole("button", { name: "Sushi Rolls" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: "Appetizers" })).toHaveAttribute("aria-pressed", "false");
  });

  describe("order limits", () => {
    it("stops adding at the limit: the plus goes and the note turns red", async () => {
      api.get.mockImplementation(
        tableApi({
          items: [{ ...MISO_SOUP, itemLimits: { perOrder: 2 } }, SALMON_ROLL],
          limits: { items: [{ item_id: 11, category_id: 1, per_order: 2, session_left: null }], categories: [] },
        }),
      );
      render(<FullMenu />);

      const add = await screen.findByRole("button", { name: "Add one Miso Soup" });
      fireEvent.click(add);
      expect(screen.getByText("2 per order")).toHaveStyle({ fontWeight: 400 });

      fireEvent.click(screen.getByRole("button", { name: "Add one Miso Soup" }));

      expect(screen.queryByRole("button", { name: "Add one Miso Soup" })).not.toBeInTheDocument();
      expect(screen.getByText("2 per order")).toHaveStyle({ fontWeight: 600 });
      // Taking one out brings the plus back
      fireEvent.click(screen.getByRole("button", { name: "Remove one Miso Soup" }));
      expect(screen.getByRole("button", { name: "Add one Miso Soup" })).toBeInTheDocument();
    });

    it("shares a category's limit between its items", async () => {
      const greenTea = { ...MISO_SOUP, item_id: 14, name: "Green Tea" };
      api.get.mockImplementation(
        tableApi({
          items: [MISO_SOUP, greenTea, SALMON_ROLL],
          limits: { items: [], categories: [{ category_id: 1, per_order: 2, session_left: null }] },
        }),
      );
      render(<FullMenu />);

      fireEvent.click(await screen.findByRole("button", { name: "Add one Miso Soup" }));
      fireEvent.click(screen.getByRole("button", { name: "Add one Green Tea" }));

      // Two in Appetizers: neither can take more, other categories can
      expect(screen.queryByRole("button", { name: "Add one Miso Soup" })).not.toBeInTheDocument();
      expect(screen.queryByRole("button", { name: "Add one Green Tea" })).not.toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Add one Salmon Roll" })).toBeInTheDocument();
    });

    it("can't add an item the table has already used up", async () => {
      api.get.mockImplementation(
        tableApi({
          limits: { items: [{ item_id: 12, category_id: 2, per_order: null, session_left: 0 }], categories: [] },
        }),
      );
      render(<FullMenu />);

      await screen.findByRole("button", { name: "Add one Miso Soup" });
      await waitFor(() =>
        expect(screen.queryByRole("button", { name: "Add one Salmon Roll" })).not.toBeInTheDocument(),
      );
    });
  });

  it("shows the location price for add-on items", async () => {
    // Wrap the default mock: the item price depends on the locationId sent
    const baseMock = api.get.getMockImplementation();
    api.get.mockImplementation((url, config) => {
      if (url.includes("/Menu/7/item")) {
        const atLocation3 = config?.params?.locationId === 3;
        return Promise.resolve({
          status: 200,
          data: [
            {
              item_id: 13,
              name: "Wagyu Nigiri",
              description: "Premium add-on",
              price: atLocation3 ? 9 : 6, // location override vs default
              is_add_on: true, // price is only shown for add-ons
              category_id: 2,
              category: "Sushi Rolls",
              tags: [],
              item_image_url: "",
            },
          ],
        });
      }
      return baseMock(url, config);
    });

    render(<FullMenu />);

    expect(await screen.findByText("Wagyu Nigiri")).toBeInTheDocument();
    expect(screen.getByText("$9.00")).toBeInTheDocument();
    expect(screen.queryByText("$6.00")).not.toBeInTheDocument();
    // Add-ons are labelled as such instead of "Included"
    expect(screen.getByText(/Add-on/)).toHaveTextContent("Add-on $9.00");
    expect(screen.queryByText("Included")).not.toBeInTheDocument();
  });

  it("falls back to default prices when the session has no location", async () => {
    const baseMock = api.get.getMockImplementation();
    api.get.mockImplementation((url, config) => {
      if (url.includes("/DiningSession/session-menu/42")) {
        return Promise.resolve({ status: 200, data: { menu_id: 7 } }); // no location
      }
      return baseMock(url, config);
    });

    render(<FullMenu />);

    await waitFor(() => {
      expect(screen.getByText("Miso Soup")).toBeInTheDocument();
    });
    expect(api.get).toHaveBeenCalledWith("/Menu/7/item", { params: {} });
  });

  it("tells guests who aren't seated to scan their table's QR code", async () => {
    const seatedMock = api.get.getMockImplementation();
    api.get.mockImplementation((url, config) =>
      url === "/DiningSession/participants/active-session-id"
        ? Promise.reject({ response: { status: 404 } })
        : seatedMock(url, config),
    );
    render(<FullMenu />);

    expect(
      await screen.findByText(/You're not seated at a table yet/),
    ).toBeInTheDocument();
    // Shown once, as info: no red error copy and no menu title
    expect(
      screen.getAllByText(/You're not seated at a table yet/),
    ).toHaveLength(1);
    expect(screen.getByRole("alert")).toHaveClass("MuiAlert-standardInfo");
    expect(screen.queryByText("All You Can Eat Menu")).not.toBeInTheDocument();
  });

  it("ignores a stale session_id left in the browser", async () => {
    // Left over from another table; the server says this guest is at session 42
    localStorage.setItem("session_id", "143");
    render(<FullMenu />);

    await waitFor(() =>
      expect(api.get).toHaveBeenCalledWith("/DiningSession/session-menu/42"),
    );
    expect(api.get).not.toHaveBeenCalledWith("/DiningSession/session-menu/143");
    expect(localStorage.getItem("session_id")).toBe("42");
  });

  it("stops ordering once the guests have asked for their bill", async () => {
    const baseMock = api.get.getMockImplementation();
    api.get.mockImplementation((url, config) =>
      url.includes("/Bill/get_bills/42")
        ? Promise.resolve({
            status: 200,
            data: [
              {
                bill_id: 7,
                status: "Open",
                created_at: "2026-10-18T04:00:00Z",
                requested_at: "2026-10-18T04:30:00Z",
              },
            ],
          })
        : baseMock(url, config),
    );
    render(<FullMenu />);

    expect(
      await screen.findAllByText(
        /You've asked for your bill, so no more orders can be added/,
      ),
    ).not.toHaveLength(0);
    // Items still show, but there's no -/+ to put them in the bag, and no way to Review order
    expect(screen.getAllByRole("heading", { level: 3 }).length).toBeGreaterThan(0);
    expect(screen.queryAllByRole("button", { name: /^Add one / })).toHaveLength(0);
    expect(screen.queryAllByRole("button", { name: /^Remove one / })).toHaveLength(0);
    expect(screen.queryByText(/^Qty /)).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^Review order/ })).not.toBeInTheDocument();
  });

  it("has the -/+ on every item while the bill is still open", async () => {
    render(<FullMenu />);

    expect((await screen.findAllByRole("button", { name: /^Add one / })).length).toBeGreaterThan(0);
  });

  it("keeps ordering on the bill this phone used last when the table has several", async () => {
    localStorage.setItem("currentBillId", "8");
    const baseMock = api.get.getMockImplementation();
    api.get.mockImplementation((url, config) =>
      url.includes("/Bill/get_bills/42")
        ? Promise.resolve({
            status: 200,
            data: [
              {
                bill_id: 9,
                bill_name: "Park",
                status: "Open",
                created_at: "2026-10-18T04:10:00Z",
              },
              {
                bill_id: 8,
                bill_name: "Lee",
                status: "Open",
                created_at: "2026-10-18T04:00:00Z",
              },
              {
                bill_id: 7,
                bill_name: "Old",
                status: "Closed",
                created_at: "2026-10-18T03:00:00Z",
              },
            ],
          })
        : baseMock(url, config),
    );
    render(<FullMenu />);

    await waitFor(() =>
      expect(localStorage.getItem("currentBillId")).toBe("8"),
    );
    expect(
      screen.queryByText(/there is no open bill/i),
    ).not.toBeInTheDocument();
  });

  it("keeps the items picked for this table after a refresh", async () => {
    const first = render(<FullMenu />);
    fireEvent.click(
      await screen.findByRole("button", { name: "Add one Miso Soup" }),
    );
    fireEvent.click(screen.getByRole("button", { name: "Add one Miso Soup" }));
    await waitFor(() =>
      expect(JSON.parse(localStorage.getItem("menuCart"))).toEqual({
        sessionId: 42,
        quantities: { 1: { 11: 2 } },
      }),
    );
    first.unmount();

    render(<FullMenu />);

    expect(
      await screen.findByRole("button", { name: "Review order, 2 items" }),
    ).toBeInTheDocument();
  });

  it("doesn't restore a cart saved at another table", async () => {
    localStorage.setItem(
      "menuCart",
      JSON.stringify({ sessionId: 99, quantities: { 1: { 11: 3 } } }),
    );
    render(<FullMenu />);
    await screen.findByRole("button", { name: "Add one Miso Soup" });

    await waitFor(() =>
      expect(JSON.parse(localStorage.getItem("menuCart"))).toEqual({
        sessionId: 42,
        quantities: {},
      }),
    );
  });

  it("doesn't say 'not seated' while it's still checking the table", async () => {
    let answer;
    const baseMock = api.get.getMockImplementation();
    api.get.mockImplementation((url, config) =>
      url === "/DiningSession/participants/active-session-id"
        ? new Promise((resolve) => {
            answer = resolve;
          })
        : baseMock(url, config),
    );
    render(<FullMenu />);

    await waitFor(() => expect(answer).toBeDefined());
    expect(
      screen.queryByText(/You're not seated at a table yet/),
    ).not.toBeInTheDocument();

    answer({ status: 200, data: { session_id: 42 } });

    expect(await screen.findByText("Miso Soup")).toBeInTheDocument();
    expect(
      screen.queryByText(/You're not seated at a table yet/),
    ).not.toBeInTheDocument();
  });

  it("matches search words across name, description and tags", async () => {
    // "miso" is in the name, "popular" is a tag
    mockSearchTerm = "Miso popular";
    render(<FullMenu />);

    expect(await screen.findByText("Miso Soup")).toBeInTheDocument();
    expect(screen.queryByText("Salmon Roll")).not.toBeInTheDocument();
  });

  it("only shows items that match every search word", async () => {
    // "miso" matches Miso Soup and "rice" matches Salmon Roll, but neither has both
    mockSearchTerm = "miso rice";
    render(<FullMenu />);

    expect(
      await screen.findByText("No items match your search."),
    ).toBeInTheDocument();
    expect(screen.queryByText("Miso Soup")).not.toBeInTheDocument();
    expect(screen.queryByText("Salmon Roll")).not.toBeInTheDocument();
  });

  it("shows placeholder cards, not 'no items', while the menu loads", async () => {
    let sendItems;
    const baseMock = api.get.getMockImplementation();
    api.get.mockImplementation((url, config) =>
      url.includes("/Menu/7/item")
        ? new Promise((resolve) => {
            sendItems = () => baseMock(url, config).then(resolve);
          })
        : baseMock(url, config),
    );
    render(<FullMenu />);

    expect(
      await screen.findByRole("status", { name: "Loading menu" }),
    ).toBeInTheDocument();
    await waitFor(() => expect(sendItems).toBeDefined());
    expect(
      screen.queryByText("No items match your search."),
    ).not.toBeInTheDocument();

    sendItems();

    expect(await screen.findByText("Miso Soup")).toBeInTheDocument();
    expect(
      screen.queryByRole("status", { name: "Loading menu" }),
    ).not.toBeInTheDocument();
  });
});
