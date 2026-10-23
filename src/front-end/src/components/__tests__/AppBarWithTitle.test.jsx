import { render, screen, fireEvent } from "@testing-library/react";
import AppBarWithTitle from "../AppBarWithTitle";
import { useAuth } from "@/hooks/useAuth";

const mockPush = jest.fn();
jest.mock("next/navigation", () => ({
  useRouter: () => ({ push: mockPush, replace: jest.fn() }),
  usePathname: () => "/menu/full-menu",
}));

// eslint-disable-next-line @next/next/no-img-element, jsx-a11y/alt-text
jest.mock("next/image", () => ({ __esModule: true, default: (props) => <img alt={props.alt} /> }));

jest.mock("@/hooks/useAuth", () => ({
  useAuth: jest.fn(),
  dispatchAuthChange: jest.fn(),
}));

jest.mock("@/config/api", () => ({
  __esModule: true,
  default: { get: jest.fn().mockResolvedValue({ data: [] }), put: jest.fn() },
}));

jest.mock("@/contexts/NotificationContext", () => ({
  useNotification: () => ({ notifySuccess: jest.fn(), notifyError: jest.fn(), notifyInfo: jest.fn() }),
}));

jest.mock("@/components/AppNavigationDrawer", () => ({ __esModule: true, default: () => null }));

const signedInAs = (userRole) =>
  useAuth.mockReturnValue({ isAuthenticated: !!userRole, userRole, loading: false });

describe("App bar account icon", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    localStorage.clear();
  });

  it("takes a customer with an account to their home page", () => {
    signedInAs("customer");
    render(<AppBarWithTitle />);

    fireEvent.click(screen.getByRole("button", { name: "Account" }));

    expect(mockPush).toHaveBeenCalledWith("/");
  });

  it("isn't shown to guests", () => {
    localStorage.setItem("guest", "true");
    signedInAs("customer");
    render(<AppBarWithTitle />);

    expect(screen.queryByRole("button", { name: "Account" })).not.toBeInTheDocument();
  });

  it.each(["staff", "admin", null])("isn't shown when signed in as %s", (role) => {
    signedInAs(role);
    render(<AppBarWithTitle />);

    expect(screen.queryByRole("button", { name: "Account" })).not.toBeInTheDocument();
  });

  // They have the bottom tabs (phones) and the side rail (desktop) instead
  it("gives customers no menu drawer button", () => {
    signedInAs("customer");
    render(<AppBarWithTitle />);

    expect(screen.queryByRole("button", { name: "menu" })).not.toBeInTheDocument();
  });

  it("keeps the menu drawer button for staff", () => {
    signedInAs("staff");
    render(<AppBarWithTitle />);

    expect(screen.getByRole("button", { name: "menu" })).toBeInTheDocument();
  });
});
