import { render, screen, fireEvent } from "@testing-library/react";
import CustomerBottomNav from "../CustomerBottomNav";
import { useAuth } from "@/hooks/useAuth";

const mockPush = jest.fn();
let mockPathname = "/menu/full-menu";
jest.mock("next/navigation", () => ({
  useRouter: () => ({ push: mockPush }),
  usePathname: () => mockPathname,
}));

jest.mock("@/hooks/useAuth", () => ({ useAuth: jest.fn() }));

describe("Customer bottom nav", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    localStorage.clear();
    // Seated at a table from this phone, unless a test says otherwise
    localStorage.setItem("session_id", "42");
    mockPathname = "/menu/full-menu";
    useAuth.mockReturnValue({ userRole: null });
  });

  it("shows the four customer pages to a guest and highlights the current one", () => {
    localStorage.setItem("guest", "true");
    mockPathname = "/dashboard/bills";

    render(<CustomerBottomNav />);

    ["Menu", "Orders", "Bill", "Call Server"].forEach((label) =>
      expect(screen.getByRole("button", { name: label })).toBeInTheDocument(),
    );
    expect(screen.getByRole("button", { name: "Bill" })).toHaveClass("Mui-selected");
  });

  it("goes to a page in one tap", () => {
    useAuth.mockReturnValue({ userRole: "customer" });

    render(<CustomerBottomNav />);
    fireEvent.click(screen.getByRole("button", { name: "Call Server" }));

    expect(mockPush).toHaveBeenCalledWith("/dashboard/call-server");
  });

  it("steps aside on Review order, which has its own bar at the bottom", () => {
    localStorage.setItem("guest", "true");
    mockPathname = "/menu/review";

    render(<CustomerBottomNav />);

    expect(screen.queryByRole("navigation")).not.toBeInTheDocument();
  });

  it("stays hidden for staff and on the login page", () => {
    useAuth.mockReturnValue({ userRole: "staff" });
    const { unmount } = render(<CustomerBottomNav />);
    expect(screen.queryByRole("navigation")).not.toBeInTheDocument();
    unmount();

    localStorage.setItem("guest", "true");
    mockPathname = "/auth/login";
    render(<CustomerBottomNav />);
    expect(screen.queryByRole("navigation")).not.toBeInTheDocument();
  });

  it("stays hidden for a customer who hasn't joined a table", () => {
    localStorage.removeItem("session_id");
    useAuth.mockReturnValue({ userRole: "customer" });

    render(<CustomerBottomNav />);

    expect(screen.queryByRole("navigation")).not.toBeInTheDocument();
  });
});
