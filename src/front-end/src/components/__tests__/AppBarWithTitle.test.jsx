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

const mockNotifyError = jest.fn();
jest.mock("@/contexts/NotificationContext", () => ({
  useNotification: () => ({ notifySuccess: jest.fn(), notifyError: mockNotifyError, notifyInfo: jest.fn() }),
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
  const downtown = { location_id: 1, name: "Downtown" };
  const southside = { location_id: 2, name: "Southside" };

  // What staff/my-locations returns for the signed-in user
  const serverReturns = (myLocations) =>
    api.get.mockImplementation((url) =>
      Promise.resolve({ data: url === "staff/my-locations" ? myLocations : [] })
    );

  beforeEach(() => {
    jest.clearAllMocks();
    localStorage.clear();
  });

  afterEach(() => {
    api.get.mockResolvedValue({ data: [] });
  });

  it("uses the location saved on the staff member", async () => {
    serverReturns({ current_location: southside, locations: [downtown, southside], all_locations: false });
    signedInAs("staff");
    render(<AppBarWithTitle />);

    await waitFor(() => expect(localStorage.getItem("branch-location")).toBe("2"));
  });

  it("replaces a location left behind by a previous user", async () => {
    localStorage.setItem("branch-location", "1");
    serverReturns({ current_location: southside, locations: [southside], all_locations: false });
    signedInAs("staff");
    render(<AppBarWithTitle />);

    await waitFor(() => expect(localStorage.getItem("branch-location")).toBe("2"));
  });

  it("starts admins with no saved location at the first location", async () => {
    serverReturns({ current_location: null, locations: [downtown, southside], all_locations: true });
    signedInAs("admin");
    render(<AppBarWithTitle />);

    await waitFor(() => expect(localStorage.getItem("branch-location")).toBe("1"));
  });

  it("shows staff with one location a label, not a dropdown", async () => {
    serverReturns({ current_location: downtown, locations: [downtown], all_locations: false });
    signedInAs("staff");
    render(<AppBarWithTitle />);

    expect(await screen.findByLabelText("Working location")).toHaveTextContent("Downtown");
    expect(screen.queryByRole("combobox")).not.toBeInTheDocument();
  });

  it("gives staff with several locations a dropdown", async () => {
    serverReturns({ current_location: downtown, locations: [downtown, southside], all_locations: false });
    signedInAs("staff");
    render(<AppBarWithTitle />);

    expect(await screen.findByRole("combobox")).toBeInTheDocument();
    expect(screen.queryByLabelText("Working location")).not.toBeInTheDocument();
  });

  it("gives admins a dropdown", async () => {
    serverReturns({ current_location: downtown, locations: [downtown], all_locations: true });
    signedInAs("admin");
    render(<AppBarWithTitle />);

    expect(await screen.findByRole("combobox")).toBeInTheDocument();
  });

  it("tells staff with no location to ask an admin", async () => {
    localStorage.setItem("branch-location", "1");
    serverReturns({ current_location: null, locations: [], all_locations: false });
    signedInAs("staff");
    render(<AppBarWithTitle />);

    await waitFor(() =>
      expect(mockNotifyError).toHaveBeenCalledWith("You don't have a location yet. Ask an admin to assign you one.")
    );
    expect(screen.getByLabelText("Working location")).toHaveTextContent("No location");
    expect(localStorage.getItem("branch-location")).toBeNull();
  });
});
