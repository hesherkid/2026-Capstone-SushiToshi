import { render, screen, fireEvent } from "@testing-library/react";
import CustomerHome from "../CustomerHome";

const mockReplace = jest.fn();
const mockPush = jest.fn();
jest.mock("next/navigation", () => ({
  useRouter: () => ({ replace: mockReplace, push: mockPush }),
}));

jest.mock("@/hooks/useAuth", () => ({
  __esModule: true,
  default: () => ({ isAuthenticated: true, loading: false }),
}));

jest.mock("@/services/tableSession", () => ({
  fetchMySessionId: jest.fn().mockResolvedValue(42),
}));

// Only the claims are read, so the header and signature can be anything
const tokenFor = (claims) => `x.${btoa(JSON.stringify(claims))}.x`;

describe("Customer home", () => {
  beforeEach(() => {
    localStorage.clear();
    mockPush.mockReset();
    localStorage.setItem(
      "authToken",
      tokenFor({ role: "Customer", exp: Math.floor(Date.now() / 1000) + 3600 }),
    );
  });

  it("lets customers look at the menu before they visit", async () => {
    render(<CustomerHome />);

    fireEvent.click(await screen.findByRole("button", { name: /View the menu/ }));
    expect(mockPush).toHaveBeenCalledWith("/menu/browse");
  });

  it("leaves the browsing menu out once they're seated", async () => {
    localStorage.setItem("session_id", "42");
    render(<CustomerHome />);

    expect(await screen.findByRole("button", { name: "Back to your table" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /View the menu/ })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Past visits/ })).toBeInTheDocument();
  });

  it("links to their account to change their name and password", async () => {
    render(<CustomerHome />);

    fireEvent.click(await screen.findByRole("button", { name: /Account/ }));
    expect(mockPush).toHaveBeenCalledWith("/account");
  });
});
