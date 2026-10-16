import { render, screen, waitFor } from "@testing-library/react";
import OrdersPage from "@/app/staff/dashboard/orders/page";
import { useAuth } from "@/hooks/useAuth";

const mockPush = jest.fn();

jest.mock("next/navigation", () => ({
  useRouter: () => ({ push: mockPush }),
}));

jest.mock("@/hooks/useAuth", () => ({ useAuth: jest.fn() }));

jest.mock("@/components/staff/Order", () => ({
  __esModule: true,
  default: () => <div>Order dashboard</div>,
}));

const signIn = (auth) =>
  useAuth.mockReturnValue({ isAuthenticated: true, loading: false, ...auth });

describe("Staff orders page", () => {
  beforeEach(() => jest.clearAllMocks());

  it.each(["staff", "admin"])("shows the order dashboard to %s", async (userRole) => {
    signIn({ userRole });
    render(<OrdersPage />);

    expect(await screen.findByText("Order dashboard")).toBeInTheDocument();
    expect(mockPush).not.toHaveBeenCalled();
  });

  it("sends customers to the unauthorized page", async () => {
    signIn({ userRole: "customer" });
    render(<OrdersPage />);

    await waitFor(() => expect(mockPush).toHaveBeenCalledWith("/unauthorized"));
    expect(screen.queryByText("Order dashboard")).not.toBeInTheDocument();
  });

  it("sends signed-out visitors to log in", async () => {
    signIn({ isAuthenticated: false, userRole: null });
    render(<OrdersPage />);

    await waitFor(() => expect(mockPush).toHaveBeenCalledWith("/auth/login"));
    expect(screen.queryByText("Order dashboard")).not.toBeInTheDocument();
  });
});
