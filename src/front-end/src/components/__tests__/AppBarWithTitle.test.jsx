import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import AppBarWithTitle from "../AppBarWithTitle";
import { useAuth } from "@/hooks/useAuth";
import api from "@/config/api";

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

describe("App bar working location", () => {
  const locations = [
    { location_Id: 1, name: "Downtown" },
    { location_Id: 2, name: "Southside" },
  ];

  // The location list, and the location saved on the signed-in user (null if none)
  const serverReturns = (savedLocation) =>
    api.get.mockImplementation((url) =>
      Promise.resolve({ data: url === "location/" ? locations : savedLocation })
    );

  beforeEach(() => {
    jest.clearAllMocks();
    localStorage.clear();
  });

  afterEach(() => {
    api.get.mockResolvedValue({ data: [] });
  });

  it("uses the location saved on the staff member", async () => {
    serverReturns(locations[1]);
    signedInAs("staff");
    render(<AppBarWithTitle />);

    await waitFor(() => expect(localStorage.getItem("branch-location")).toBe("2"));
  });

  it("replaces a location left behind by a previous user", async () => {
    localStorage.setItem("branch-location", "1");
    serverReturns(locations[1]);
    signedInAs("staff");
    render(<AppBarWithTitle />);

    await waitFor(() => expect(localStorage.getItem("branch-location")).toBe("2"));
  });

  it("falls back to the first location when none is saved", async () => {
    serverReturns(null);
    signedInAs("admin");
    render(<AppBarWithTitle />);

    await waitFor(() => expect(localStorage.getItem("branch-location")).toBe("1"));
  });
});
