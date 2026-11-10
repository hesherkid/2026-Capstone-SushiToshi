import React from "react";
import { render as rtlRender, screen, waitFor, fireEvent, within } from "@testing-library/react";
import FullMenu from "./page";
import { TableOrderProvider } from "@/contexts/TableOrderContext";
import { tableApi, myBillResponse, MISO_SOUP, SALMON_ROLL, LEE_BILL } from "../_testing/tableApiMock";

const mockPush = jest.fn();
jest.mock("next/navigation", () => ({
  useRouter: () => ({ push: mockPush, back: jest.fn() }),
}));

// What's typed in the search box; set before rendering
let mockSearchTerm = "";
jest.mock("@/components/menu/searchUtils", () => ({
  __esModule: true,
  // The real matching, so the search tests check what guests get
  matchesSearch: jest.requireActual("@/components/menu/searchUtils").matchesSearch,
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
    post: jest.fn(),
    put: jest.fn(),
    delete: jest.fn(),
  },
}));

const api = require("@/config/api").default;

// The page reads its table, menu and cart from TableOrderContext
const render = (ui) => rtlRender(<TableOrderProvider>{ui}</TableOrderProvider>);

describe("FullMenu guest flow", () => {
  beforeEach(() => {
    localStorage.clear();
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
      params: { location_id: 3 },
    });

    // Already on a bill, so nobody asks which one
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();

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
    expect(screen.getByRole("heading", { name: "Appetizers 1 item" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Sushi Rolls 1 item" })).toBeInTheDocument();
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
          items: [{ ...MISO_SOUP, item_limits: { per_order: 2 } }, SALMON_ROLL],
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

    it("stops an item with no limit at 99, the most one order takes", async () => {
      localStorage.setItem("menuCart", JSON.stringify({ sessionId: 42, quantities: { 1: { 11: 98 } } }));
      render(<FullMenu />);

      fireEvent.click(await screen.findByRole("button", { name: "Add one Miso Soup" }));

      await waitFor(() =>
        expect(screen.queryByRole("button", { name: "Add one Miso Soup" })).not.toBeInTheDocument(),
      );
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
        const atLocation3 = config?.params?.location_id === 3;
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
    // Browsing before being seated is for customers with an account
    expect(screen.queryByRole("button", { name: "Browse the menu" })).not.toBeInTheDocument();
  });

  it("lets customers with an account who aren't seated browse the menu", async () => {
    localStorage.clear();
    const claims = { role: "Customer", exp: Math.floor(Date.now() / 1000) + 3600 };
    localStorage.setItem("authToken", `x.${btoa(JSON.stringify(claims))}.x`);
    const seatedMock = api.get.getMockImplementation();
    api.get.mockImplementation((url, config) =>
      url === "/DiningSession/participants/active-session-id"
        ? Promise.reject({ response: { status: 404 } })
        : seatedMock(url, config),
    );
    render(<FullMenu />);

    fireEvent.click(await screen.findByRole("button", { name: "Browse the menu" }));
    expect(mockPush).toHaveBeenCalledWith("/menu/browse");
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
    api.get.mockImplementation(
      tableApi({ bills: [{ ...LEE_BILL, requested_at: "2026-10-18T04:30:00Z" }] }),
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

  describe("choosing a bill", () => {
    const PARK_BILL = {
      bill_id: 9,
      bill_name: "Park",
      status: "Open",
      total_count: 3,
      adult_count: 3,
      people_count: 1,
      requested_at: null,
    };

    it("asks a guest who isn't on a bill which one is theirs, and puts them on it", async () => {
      api.get.mockImplementation(tableApi({ bills: [LEE_BILL, PARK_BILL], myBillId: null }));
      api.put.mockResolvedValue({
        data: myBillResponse({ bills: [LEE_BILL, PARK_BILL], myBillId: 9 }),
      });
      render(<FullMenu />);

      const dialog = await screen.findByRole("dialog", { name: /^Which bill are you on\?/ });
      // How a party shares a bill
      expect(within(dialog).getByText("One person starts the bill for your party.")).toBeInTheDocument();
      expect(within(dialog).getByText("Paying separately? Start your own bill.")).toBeInTheDocument();
      expect(within(dialog).getByRole("button", { name: /Lee\s*2 guests/ })).toBeInTheDocument();
      fireEvent.click(within(dialog).getByRole("button", { name: /Park\s*3 guests/ }));

      await waitFor(() =>
        expect(api.put).toHaveBeenCalledWith("/SessionParticipant/42/bill", { bill_id: 9 }),
      );
      // Then they check the bill's guest count
      expect(await within(dialog).findByRole("heading", { name: "How many in your party?" })).toBeInTheDocument();
      expect(within(dialog).getByRole("status")).toHaveTextContent("3 guests on this bill");
      fireEvent.click(within(dialog).getByRole("button", { name: "Looks right" }));

      await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
      expect(api.put).toHaveBeenCalledTimes(1); // nothing changed, so nothing saved
      expect(screen.queryByText("Choose your bill before placing an order.")).not.toBeInTheDocument();
    });

    it("after joining a bill with more people than guests, asks them to update the count", async () => {
      const SOLO = { ...PARK_BILL, total_count: 1, adult_count: 1 };
      api.get.mockImplementation(tableApi({ bills: [SOLO], myBillId: null }));
      api.put
        .mockResolvedValueOnce({
          data: myBillResponse({ bills: [{ ...SOLO, people_count: 2 }], myBillId: 9 }),
        })
        .mockResolvedValueOnce({
          data: myBillResponse({
            bills: [{ ...SOLO, total_count: 2, adult_count: 2, people_count: 2 }],
            myBillId: 9,
          }),
        });
      render(<FullMenu />);

      const dialog = await screen.findByRole("dialog", { name: /^Which bill are you on\?/ });
      fireEvent.click(within(dialog).getByRole("button", { name: /Park/ }));

      expect(await within(dialog).findByText("2 people have joined this bill. Make sure everyone is counted."))
        .toBeInTheDocument();
      fireEvent.click(within(dialog).getByRole("button", { name: "More adults" }));
      expect(within(dialog).queryByText(/people have joined/)).not.toBeInTheDocument();
      fireEvent.click(within(dialog).getByRole("button", { name: "Save guest count" }));

      await waitFor(() =>
        expect(api.put).toHaveBeenLastCalledWith("/SessionParticipant/42/bill/guests", {
          adult_count: 2,
          senior_count: 0,
          child_count: 0,
          tot_count: 0,
        }),
      );
      await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    });

    it("shows the server's reason when the guest count can't be saved", async () => {
      api.get.mockImplementation(tableApi({ bills: [PARK_BILL], myBillId: null }));
      api.put
        .mockResolvedValueOnce({ data: myBillResponse({ bills: [PARK_BILL], myBillId: 9 }) })
        .mockRejectedValueOnce({ response: { data: { message: "Your table seats 4, and the other bills have 0." } } });
      render(<FullMenu />);

      const dialog = await screen.findByRole("dialog", { name: /^Which bill are you on\?/ });
      fireEvent.click(within(dialog).getByRole("button", { name: /Park/ }));
      fireEvent.click(await within(dialog).findByRole("button", { name: "More adults" }));
      fireEvent.click(within(dialog).getByRole("button", { name: "Save guest count" }));

      expect(await within(dialog).findByText("Your table seats 4, and the other bills have 0.")).toBeInTheDocument();
    });

    it("lets a guest at a table with no bills start one", async () => {
      api.get.mockImplementation(tableApi({ bills: [] }));
      api.post.mockResolvedValue({ data: { bill_id: 20 } });
      render(<FullMenu />);

      const dialog = await screen.findByRole("dialog", { name: /^Start your bill/ });
      expect(within(dialog).getByRole("textbox", { name: /Name for the bill/ })).toHaveAttribute(
        "placeholder",
        "e.g. your first name",
      );
      const start = within(dialog).getByRole("button", { name: "Start Bill" });
      expect(start).toBeDisabled(); // needs a name

      fireEvent.change(within(dialog).getByRole("textbox", { name: /Name for the bill/ }), {
        target: { value: "  Kim " },
      });
      fireEvent.click(within(dialog).getByRole("button", { name: "More adults" }));
      fireEvent.click(within(dialog).getByRole("button", { name: "More children" }));
      expect(within(dialog).getByRole("status")).toHaveTextContent("3 guests on this bill");
      fireEvent.click(start);

      await waitFor(() =>
        expect(api.post).toHaveBeenCalledWith("/Bill/create_Bill/42", {
          bill_name: "Kim",
          adult_count: 2,
          senior_count: 0,
          child_count: 1,
          tot_count: 0,
        }),
      );
    });

    it("shows the server's reason when a bill can't be joined", async () => {
      api.get.mockImplementation(tableApi({ bills: [LEE_BILL, PARK_BILL], myBillId: null }));
      api.put.mockRejectedValue({ response: { data: { message: "Park has already asked for the bill." } } });
      render(<FullMenu />);

      const dialog = await screen.findByRole("dialog", { name: /^Which bill are you on\?/ });
      fireEvent.click(within(dialog).getByRole("button", { name: /Park/ }));

      expect(await within(dialog).findByText("Park has already asked for the bill.")).toBeInTheDocument();
    });

    it("can't join a bill that has asked for the bill", async () => {
      api.get.mockImplementation(
        tableApi({ bills: [LEE_BILL, { ...PARK_BILL, requested_at: "2026-10-18T04:30:00Z" }], myBillId: null }),
      );
      render(<FullMenu />);

      const dialog = await screen.findByRole("dialog", { name: /^Which bill are you on\?/ });
      expect(within(dialog).getByRole("button", { name: /Park\s*Has asked for the bill/ })).toBeDisabled();
      expect(within(dialog).getByRole("button", { name: /Lee/ })).toBeEnabled();
    });

    it("lets a guest browse first, then reminds them to choose", async () => {
      api.get.mockImplementation(tableApi({ bills: [LEE_BILL, PARK_BILL], myBillId: null }));
      render(<FullMenu />);

      const dialog = await screen.findByRole("dialog", { name: /^Which bill are you on\?/ });
      fireEvent.click(within(dialog).getByRole("button", { name: "Close" }));
      await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());

      expect(screen.getByText("Choose your bill before placing an order.")).toBeInTheDocument();
      fireEvent.click(screen.getByRole("button", { name: "Choose bill" }));
      expect(await screen.findByRole("dialog", { name: /^Which bill are you on\?/ })).toBeInTheDocument();
    });
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

  describe("tag filter", () => {
    const SPICY_TUNA = {
      ...SALMON_ROLL,
      item_id: 13,
      name: "Spicy Tuna Roll",
      tags: [
        { name: "Popular", color: "#C01E2E" },
        { name: "Spicy", color: "#FF5733" },
      ],
    };

    beforeEach(() => {
      api.get.mockImplementation(tableApi({ items: [MISO_SOUP, SALMON_ROLL, SPICY_TUNA] }));
    });

    const filterRow = () => screen.getByRole("group", { name: "Filter the menu" });
    const tagButton = (name) => within(filterRow()).getByRole("button", { name });

    it("offers each tag on the menu once, A to Z", async () => {
      render(<FullMenu />);
      await screen.findByText("Miso Soup");

      const labels = within(filterRow())
        .getAllByRole("button")
        .map((button) => button.textContent);
      expect(labels).toEqual(["Popular", "Spicy"]);
    });

    it("shows only items with every picked tag, and Clear brings them all back", async () => {
      render(<FullMenu />);
      await screen.findByText("Miso Soup");

      fireEvent.click(tagButton("Popular"));
      expect(tagButton("Popular")).toHaveAttribute("aria-pressed", "true");
      expect(screen.getByText("Miso Soup")).toBeInTheDocument();
      expect(screen.getByText("Spicy Tuna Roll")).toBeInTheDocument();
      expect(screen.queryByText("Salmon Roll")).not.toBeInTheDocument();

      fireEvent.click(tagButton("Spicy"));
      expect(screen.queryByText("Miso Soup")).not.toBeInTheDocument();
      expect(screen.getByText("Spicy Tuna Roll")).toBeInTheDocument();

      fireEvent.click(screen.getByRole("button", { name: "Clear filters" }));
      expect(screen.getByText("Miso Soup")).toBeInTheDocument();
      expect(screen.getByText("Salmon Roll")).toBeInTheDocument();
      expect(screen.queryByRole("button", { name: "Clear filters" })).not.toBeInTheDocument();
    });

    it("keeps items already in the cart on screen", async () => {
      render(<FullMenu />);
      await screen.findByText("Salmon Roll");

      fireEvent.click(screen.getByRole("button", { name: "Add one Salmon Roll" }));
      fireEvent.click(tagButton("Spicy"));

      expect(screen.getByText("Salmon Roll")).toBeInTheDocument();
      expect(screen.queryByText("Miso Soup")).not.toBeInTheDocument();
    });

    it("works with the search box", async () => {
      mockSearchTerm = "miso";
      render(<FullMenu />);
      await screen.findByText("Miso Soup");

      fireEvent.click(tagButton("Spicy"));
      expect(
        screen.getByText("No items match your search and filters."),
      ).toBeInTheDocument();
    });
  });

  describe("favourites and order again", () => {
    const ROLE = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";
    // Only the claims are read, so the header and signature can be anything
    const tokenFor = (claims) =>
      `x.${btoa(JSON.stringify({ exp: Date.now() / 1000 + 3600, ...claims }))}.x`;

    // A customer with an account, with these favourites and past orders saved
    const signInCustomer = ({ favourites = [], orderedBefore = [] } = {}) => {
      localStorage.setItem("authToken", tokenFor({ [ROLE]: "Customer" }));
      const menu = tableApi();
      api.get.mockImplementation((url, config) => {
        if (url === "/me/favourites") return Promise.resolve({ data: favourites });
        if (url === "/me/ordered-before") return Promise.resolve({ data: orderedBefore });
        return menu(url, config);
      });
    };

    const pill = (name) =>
      within(screen.getByRole("group", { name: "Filter the menu" })).getByRole("button", { name });

    it("hearts an item and saves it to the account", async () => {
      signInCustomer();
      api.put.mockResolvedValue({ status: 204 });
      render(<FullMenu />);
      await screen.findByText("Miso Soup");

      const heart = screen.getByRole("button", { name: "Favourite Miso Soup" });
      expect(heart).toHaveAttribute("aria-pressed", "false");

      fireEvent.click(heart);

      expect(heart).toHaveAttribute("aria-pressed", "true");
      expect(api.put).toHaveBeenCalledWith("/me/favourites/11");
      // Its first favourite brings in the Favourites filter
      expect(pill("Favourites")).toBeInTheDocument();
    });

    it("un-hearts a favourite, and puts it back if saving fails", async () => {
      signInCustomer({ favourites: [MISO_SOUP.item_id] });
      api.delete.mockRejectedValue(new Error("offline"));
      render(<FullMenu />);

      const heart = await screen.findByRole("button", { name: "Favourite Miso Soup", pressed: true });
      fireEvent.click(heart);

      expect(api.delete).toHaveBeenCalledWith("/me/favourites/11");
      expect(await screen.findByText("Couldn't update your favourites. Please try again.")).toBeInTheDocument();
      expect(heart).toHaveAttribute("aria-pressed", "true");
    });

    it("filters to favourites", async () => {
      signInCustomer({ favourites: [SALMON_ROLL.item_id] });
      render(<FullMenu />);
      await screen.findByText("Miso Soup");

      fireEvent.click(await screen.findByRole("button", { name: "Favourites" }));

      expect(screen.getByText("Salmon Roll")).toBeInTheDocument();
      expect(screen.queryByText("Miso Soup")).not.toBeInTheDocument();
    });

    it("Order again shows only what the customer has ordered before", async () => {
      // 99 was ordered on a visit but isn't on this menu, so it's left out
      signInCustomer({ orderedBefore: [99, MISO_SOUP.item_id] });
      render(<FullMenu />);
      await screen.findByText("Salmon Roll");

      fireEvent.click(await screen.findByRole("button", { name: "Order again" }));

      expect(screen.getByText("Miso Soup")).toBeInTheDocument();
      expect(screen.queryByText("Salmon Roll")).not.toBeInTheDocument();
    });

    it("leaves out Favourites and Order again when there's nothing in them", async () => {
      signInCustomer();
      render(<FullMenu />);
      await screen.findByText("Miso Soup");

      expect(screen.queryByRole("button", { name: "Favourites" })).not.toBeInTheDocument();
      expect(screen.queryByRole("button", { name: "Order again" })).not.toBeInTheDocument();
    });

    it("gives guests no hearts", async () => {
      localStorage.setItem("authToken", tokenFor({ [ROLE]: "Customer", guest: "true" }));
      render(<FullMenu />);
      await screen.findByText("Miso Soup");

      expect(screen.queryByRole("button", { name: /^Favourite/ })).not.toBeInTheDocument();
      expect(screen.queryByText(/Make an account to save favourites/)).not.toBeInTheDocument();
      expect(api.get).not.toHaveBeenCalledWith("/me/favourites");
    });
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
