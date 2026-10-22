import { render, screen, fireEvent } from "@testing-library/react";
import CustomerSideNav, { SIDE_NAV_ATTR } from "../CustomerSideNav";
import { useAuth } from "@/hooks/useAuth";

const mockPush = jest.fn();
let mockPathname = "/menu/full-menu";
jest.mock("next/navigation", () => ({
  useRouter: () => ({ push: mockPush }),
  usePathname: () => mockPathname,
}));

jest.mock("@/hooks/useAuth", () => ({ useAuth: jest.fn() }));

describe("Customer side nav (desktop)", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    localStorage.clear();
    localStorage.setItem("session_id", "42");
    mockPathname = "/menu/full-menu";
    useAuth.mockReturnValue({ userRole: "customer" });
  });

  it("lists the four customer pages, always in view, and marks the current one", () => {
    mockPathname = "/dashboard/orders";

    render(<CustomerSideNav />);

    const nav = screen.getByRole("navigation", { name: "Customer navigation" });
    expect(nav).toHaveAttribute(SIDE_NAV_ATTR);
    ["Menu", "Orders", "Bill", "Call Server"].forEach((label) =>
      expect(screen.getByRole("button", { name: label })).toBeInTheDocument(),
    );
    expect(screen.getByRole("button", { name: "Orders" })).toHaveAttribute("aria-current", "page");
    expect(screen.getByRole("button", { name: "Menu" })).not.toHaveAttribute("aria-current");
  });

  it("goes to a page in one click", () => {
    render(<CustomerSideNav />);

    fireEvent.click(screen.getByRole("button", { name: "Bill" }));

    expect(mockPush).toHaveBeenCalledWith("/dashboard/bills");
  });

  it("shows for guests at a table too", () => {
    useAuth.mockReturnValue({ userRole: null });
    localStorage.setItem("guest", "true");

    render(<CustomerSideNav />);

    expect(screen.getByRole("navigation")).toBeInTheDocument();
  });

  // Same rules as the phone tab bar
  it.each([
    ["before joining a table", () => localStorage.removeItem("session_id")],
    ["on Review order", () => { mockPathname = "/menu/review"; }],
    ["on the thank-you page", () => { mockPathname = "/thank-you"; }],
    ["for staff", () => useAuth.mockReturnValue({ userRole: "staff" })],
  ])("stays hidden %s", (_, setUp) => {
    setUp();

    render(<CustomerSideNav />);

    expect(screen.queryByRole("navigation")).not.toBeInTheDocument();
  });
});
