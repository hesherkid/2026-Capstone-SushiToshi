import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import PastVisitsPage from "./page";

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

jest.mock("@/services/myItems", () => ({
  fetchPastVisits: jest.fn(),
}));

const { fetchPastVisits } = require("@/services/myItems");

// Only the claims are read, so the header and signature can be anything
const tokenFor = (claims) => `x.${btoa(JSON.stringify(claims))}.x`;

const VISIT = {
  session_id: 42,
  // 7:15 pm on Oct 23 in Edmonton
  started_at: "2026-10-24T01:15:00Z",
  ended_at: "2026-10-24T02:40:00Z",
  location_name: "Sushi Toshi South",
  table_numbers: [14],
  items: [
    { item_id: 7, name: "Salmon Roll", quantity: 3 },
    { item_id: 8, name: "Miso Soup", quantity: 1 },
  ],
  bills: [{ bill_id: 88, bill_name: "Lee", guest_count: 2, total: 96.6 }],
};

describe("Past visits page", () => {
  beforeEach(() => {
    localStorage.clear();
    mockReplace.mockReset();
    mockPush.mockReset();
    fetchPastVisits.mockReset();
    mockAuth = { isAuthenticated: true, loading: false };
    localStorage.setItem("authToken", tokenFor({ role: "Customer" }));
  });

  it("shows each visit's date, place, what they ordered and the bill total", async () => {
    fetchPastVisits.mockResolvedValue([VISIT]);
    render(<PastVisitsPage />);

    const card = await screen.findByRole("article");
    // The date is the restaurant's, not UTC's (Oct 24)
    expect(within(card).getByRole("heading", { name: "Fri, Oct 23, 2026" })).toBeInTheDocument();
    expect(within(card).getByText("Sushi Toshi South · Table 14")).toBeInTheDocument();
    expect(within(card).getByText("Salmon Roll")).toBeInTheDocument();
    expect(within(card).getByText("3×")).toBeInTheDocument();
    expect(within(card).getByText(/Bill total · 2 guests/)).toBeInTheDocument();
    expect(within(card).getByText("$96.60")).toBeInTheDocument();
  });

  it("names each bill when the table had several", async () => {
    fetchPastVisits.mockResolvedValue([
      {
        ...VISIT,
        bills: [
          { bill_id: 88, bill_name: "Lee", guest_count: 2, total: 96.6 },
          { bill_id: 89, bill_name: "", guest_count: 1, total: 42 },
        ],
      },
    ]);
    render(<PastVisitsPage />);

    expect(await screen.findByText(/Lee total · 2 guests/)).toBeInTheDocument();
    expect(screen.getByText(/Bill #89 total · 1 guest$/)).toBeInTheDocument();
  });

  it("folds long orders away behind Show all", async () => {
    const items = Array.from({ length: 7 }, (_, i) => ({ item_id: i + 1, name: `Item ${i + 1}`, quantity: 1 }));
    fetchPastVisits.mockResolvedValue([{ ...VISIT, items }]);
    render(<PastVisitsPage />);

    expect(await screen.findByText("Item 5")).toBeInTheDocument();
    expect(screen.queryByText("Item 6")).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Show all 7" }));
    expect(screen.getByText("Item 7")).toBeInTheDocument();
  });

  it("says when someone else at the table did the ordering", async () => {
    fetchPastVisits.mockResolvedValue([{ ...VISIT, items: [] }]);
    render(<PastVisitsPage />);

    expect(await screen.findByText("Others at your table did the ordering.")).toBeInTheDocument();
  });

  it("explains an empty list", async () => {
    fetchPastVisits.mockResolvedValue([]);
    render(<PastVisitsPage />);

    expect(await screen.findByText(/No past visits yet/)).toBeInTheDocument();
  });

  it("shows an error if the visits can't be loaded", async () => {
    fetchPastVisits.mockRejectedValue(new Error("offline"));
    jest.spyOn(console, "error").mockImplementation(() => {});
    render(<PastVisitsPage />);

    expect(await screen.findByText("Couldn't load your past visits. Please try again.")).toBeInTheDocument();
    console.error.mockRestore();
  });

  it("sends guests to the menu without asking for visits", async () => {
    localStorage.setItem("authToken", tokenFor({ role: "Customer", guest: "true" }));
    render(<PastVisitsPage />);

    await waitFor(() => expect(mockReplace).toHaveBeenCalledWith("/menu/full-menu"));
    expect(fetchPastVisits).not.toHaveBeenCalled();
  });

  it("sends signed-out visitors to sign in", async () => {
    localStorage.clear();
    mockAuth = { isAuthenticated: false, loading: false };
    render(<PastVisitsPage />);

    await waitFor(() => expect(mockReplace).toHaveBeenCalledWith("/auth/login"));
    expect(fetchPastVisits).not.toHaveBeenCalled();
  });
});
