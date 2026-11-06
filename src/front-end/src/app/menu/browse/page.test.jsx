import { render, screen, fireEvent, within } from "@testing-library/react";
import BrowseMenuPage from "./page";
import { MISO_SOUP, SALMON_ROLL } from "../_testing/tableApiMock";

const mockReplace = jest.fn();
const mockPush = jest.fn();
jest.mock("next/navigation", () => ({
  useRouter: () => ({ replace: mockReplace, push: mockPush }),
}));

let mockAuth;
jest.mock("@/hooks/useAuth", () => ({
  __esModule: true,
  default: () => mockAuth,
}));

jest.mock("@/config/api", () => ({
  __esModule: true,
  default: { get: jest.fn(), put: jest.fn(), delete: jest.fn() },
}));

const api = require("@/config/api").default;

// Only the claims are read, so the header and signature can be anything
const tokenFor = (claims) => `x.${btoa(JSON.stringify(claims))}.x`;
const IN_AN_HOUR = Math.floor(Date.now() / 1000) + 3600;

const LUNCH = { menu_id: 7, name: "Lunch", start_time: "11:00:00", end_time: "15:00:00" };
const DINNER = { menu_id: 8, name: "Dinner", start_time: "15:00:00", end_time: "22:00:00" };
const UNAGI = { ...SALMON_ROLL, itemId: 13, name: "Unagi Roll" };

// Two locations: 3 has lunch and dinner, 4 has dinner only
const browseApi = ({ favourites = [] } = {}) => (url) => {
  const ok = (data) => Promise.resolve({ status: 200, data });
  if (url === "/Location") {
    return ok([
      { location_Id: 3, name: "Sushi Toshi South" },
      { location_Id: 4, name: "Sushi Toshi North" },
    ]);
  }
  if (url === "/Category") {
    return ok([
      { category_id: 1, category_name: "Appetizers" },
      { category_id: 2, category_name: "Sushi Rolls" },
    ]);
  }
  if (url === "/Menu/location/3") return ok([DINNER, LUNCH]);
  if (url === "/Menu/location/4") return ok([DINNER]);
  if (url === "/Menu/7/item") return ok([MISO_SOUP]);
  if (url === "/Menu/8/item") return ok([MISO_SOUP, SALMON_ROLL, UNAGI]);
  if (url === "/me/favourites") return ok(favourites);
  return ok([]);
};

// Only Date is faked, so promises and waitFor run as usual
const atRestaurantTime = (utc) =>
  jest.useFakeTimers({
    now: new Date(utc),
    doNotFake: [
      "nextTick", "setImmediate", "clearImmediate", "setInterval", "clearInterval",
      "setTimeout", "clearTimeout", "queueMicrotask", "requestAnimationFrame",
      "cancelAnimationFrame", "requestIdleCallback", "cancelIdleCallback", "hrtime", "performance",
    ],
  });

describe("Browse menu page", () => {
  beforeEach(() => {
    localStorage.clear();
    mockReplace.mockReset();
    mockPush.mockReset();
    window.scrollTo = jest.fn();
    mockAuth = { isAuthenticated: true, loading: false };
    localStorage.setItem("authToken", tokenFor({ role: "Customer", exp: IN_AN_HOUR }));
    api.get.mockReset();
    api.get.mockImplementation(browseApi());
    // Noon in Edmonton
    atRestaurantTime("2026-11-04T19:00:00Z");
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it("opens on the menu served now, priced for the location, with nothing to order", async () => {
    render(<BrowseMenuPage />);

    expect(await screen.findByText("Miso Soup")).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith("/Menu/7/item", { params: { locationId: 3 } });
    expect(screen.queryByRole("button", { name: "Add one Miso Soup" })).not.toBeInTheDocument();
  });

  it("starts on dinner in the evening and lets the customer switch menus", async () => {
    // 7 pm in Edmonton
    atRestaurantTime("2026-11-05T02:00:00Z");
    render(<BrowseMenuPage />);

    expect(await screen.findByText("Unagi Roll")).toBeInTheDocument();

    // Menus are listed by the time they start, the one served now picked
    const pills = within(screen.getByRole("group", { name: "Menus" })).getAllByRole("button");
    expect(pills.map((pill) => pill.textContent)).toEqual([
      expect.stringMatching(/^Lunch · 11 a\.m\.–3 p\.m\.$/),
      expect.stringMatching(/^Dinner · 3 p\.m\.–10 p\.m\.$/),
    ]);
    expect(pills[1]).toHaveAttribute("aria-pressed", "true");
    fireEvent.click(pills[0]);

    expect(await screen.findByText("Miso Soup")).toBeInTheDocument();
    expect(screen.queryByText("Unagi Roll")).not.toBeInTheDocument();
  });

  it("shows another location's menu and remembers the choice", async () => {
    render(<BrowseMenuPage />);
    await screen.findByText("Miso Soup");

    fireEvent.mouseDown(screen.getByRole("combobox", { name: /Location/ }));
    fireEvent.click(screen.getByRole("option", { name: "Sushi Toshi North" }));

    expect(await screen.findByText("Unagi Roll")).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith("/Menu/8/item", { params: { locationId: 4 } });
    // Only one menu there, so there's nothing to pick
    expect(screen.queryByRole("group", { name: "Menus" })).not.toBeInTheDocument();
    expect(screen.getByRole("combobox", { name: /Location/ })).toHaveTextContent("Sushi Toshi North");
    expect(localStorage.getItem("browseLocationId")).toBe("4");
  });

  it("starts at the location whose table was scanned last", async () => {
    localStorage.setItem("locationId", "4");
    render(<BrowseMenuPage />);

    expect(await screen.findByText("Unagi Roll")).toBeInTheDocument();
  });

  it("lets customers filter by their favourites", async () => {
    api.get.mockImplementation(browseApi({ favourites: [SALMON_ROLL.itemId] }));
    atRestaurantTime("2026-11-05T02:00:00Z");
    render(<BrowseMenuPage />);

    fireEvent.click(await screen.findByRole("button", { name: /Favourites/ }));

    expect(screen.getByText("Salmon Roll")).toBeInTheDocument();
    expect(screen.queryByText("Unagi Roll")).not.toBeInTheDocument();
  });

  it("sends guests and seated customers to their table's menu", () => {
    localStorage.setItem("session_id", "42");
    render(<BrowseMenuPage />);
    expect(mockReplace).toHaveBeenCalledWith("/menu/full-menu");

    mockReplace.mockReset();
    localStorage.clear();
    localStorage.setItem("authToken", tokenFor({ role: "Customer", exp: IN_AN_HOUR, guest: "true" }));
    render(<BrowseMenuPage />);
    expect(mockReplace).toHaveBeenCalledWith("/menu/full-menu");
  });

  it("sends signed-out visitors to sign in", () => {
    localStorage.clear();
    mockAuth = { isAuthenticated: false, loading: false };
    render(<BrowseMenuPage />);
    expect(mockReplace).toHaveBeenCalledWith("/auth/login");
  });
});
