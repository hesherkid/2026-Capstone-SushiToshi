import { render, screen, waitFor } from "@testing-library/react";
import RequireRole from "@/components/auth/RequireRole";
import { useAuth } from "@/hooks/useAuth";

const mockPush = jest.fn();

jest.mock("next/navigation", () => ({
  useRouter: () => ({ push: mockPush }),
}));

jest.mock("@/hooks/useAuth", () => ({ useAuth: jest.fn() }));

const ROLES = ["admin", "staff"];

const signIn = (auth) =>
  useAuth.mockReturnValue({ isAuthenticated: true, loading: false, ...auth });

const renderGuard = () =>
  render(
    <RequireRole roles={ROLES}>
      <div>Protected page</div>
    </RequireRole>
  );

describe("RequireRole", () => {
  beforeEach(() => jest.clearAllMocks());

  it.each(["staff", "admin"])("shows the page to %s", async (userRole) => {
    signIn({ userRole });
    renderGuard();

    expect(await screen.findByText("Protected page")).toBeInTheDocument();
    expect(mockPush).not.toHaveBeenCalled();
  });

  it("sends other roles to the unauthorized page", async () => {
    signIn({ userRole: "customer" });
    renderGuard();

    await waitFor(() => expect(mockPush).toHaveBeenCalledWith("/unauthorized"));
    expect(screen.queryByText("Protected page")).not.toBeInTheDocument();
  });

  it("sends signed-out visitors to log in", async () => {
    signIn({ isAuthenticated: false, userRole: null });
    renderGuard();

    await waitFor(() => expect(mockPush).toHaveBeenCalledWith("/auth/login"));
    expect(screen.queryByText("Protected page")).not.toBeInTheDocument();
  });

  it("waits for auth to load before redirecting", () => {
    signIn({ isAuthenticated: false, userRole: null, loading: true });
    renderGuard();

    expect(mockPush).not.toHaveBeenCalled();
    expect(screen.queryByText("Protected page")).not.toBeInTheDocument();
  });
});
