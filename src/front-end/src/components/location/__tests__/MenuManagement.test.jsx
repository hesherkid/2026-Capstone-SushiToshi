import {
  render,
  screen,
  fireEvent,
  waitFor,
  within,
} from "@testing-library/react";
import MenuManagement from "../MenuManagement";
import api from "@/config/api";

jest.mock("@/config/api", () => {
  const mockApi = {
    get: jest.fn(),
    post: jest.fn(),
    put: jest.fn(),
    delete: jest.fn(),
  };
  return { __esModule: true, default: mockApi, api: mockApi };
});

jest.mock("@mui/x-data-grid", () => {
  if (typeof global.TextEncoder === "undefined") {
    const { TextEncoder, TextDecoder } = jest.requireActual("util");
    Object.assign(global, { TextEncoder, TextDecoder });
  }
  const actual = jest.requireActual("@mui/x-data-grid");
  const React = jest.requireActual("react");
  return {
    ...actual,
    DataGrid: (props) =>
      React.createElement(actual.DataGrid, {
        ...props,
        disableVirtualization: true,
      }),
  };
});

if (typeof window.ResizeObserver === "undefined") {
  window.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
}

const menus = [
  { menu_id: 1, name: "Lunch", description: "Midday", is_active: true },
  { menu_id: 2, name: "Late Night", description: "After 9", is_active: false },
];

beforeEach(() => {
  jest.clearAllMocks();
  api.get.mockImplementation(async (url) => ({
    data: url === "/Menu" ? menus : [],
  }));
});

describe("MenuManagement", () => {
  it("lists the menus with whether each is active", async () => {
    render(<MenuManagement />);

    expect(await screen.findByText("Lunch")).toBeInTheDocument();
    expect(screen.getByText("Late Night")).toBeInTheDocument();
    expect(screen.getByText("Yes")).toBeInTheDocument();
    expect(screen.getByText("No")).toBeInTheDocument();
  });

  it("keeps the delete confirmation open with the reason when deleting fails", async () => {
    api.delete.mockRejectedValue({
      response: { status: 409, data: "Menu is in use" },
    });
    render(<MenuManagement />);
    await screen.findByText("Lunch");

    fireEvent.click(screen.getByRole("button", { name: "Delete Lunch" }));
    const dialog = (await screen.findByText(/delete the menu "Lunch"/)).closest(
      '[role="dialog"]',
    );
    fireEvent.click(
      within(dialog).getByRole("button", { name: "Delete Menu" }),
    );

    expect(
      await within(dialog).findByText("Menu is in use"),
    ).toBeInTheDocument();
    await waitFor(() =>
      expect(
        within(dialog).getByRole("button", { name: "Delete Menu" }),
      ).toBeEnabled(),
    );
  });

  it("shows a location added in Manage Locations when the dialog is opened again", async () => {
    let lunchLocations = [];
    api.get.mockImplementation(async (url) => {
      if (url === "/Menu")
        return {
          data: [{ ...menus[0], location_ids: lunchLocations }, menus[1]],
        };
      if (url === "/location")
        return { data: [{ location_id: 7, name: "Downtown" }] };
      return { data: [] };
    });
    api.put.mockImplementation(async () => {
      lunchLocations = [7]; // saved on the server
      return { data: {} };
    });
    render(<MenuManagement />);
    await screen.findByText("Lunch");

    const openLocations = async () => {
      fireEvent.click(
        screen.getByRole("button", { name: "Assign Locations Lunch" }),
      );
      return (
        await screen.findByRole("heading", { name: "Manage Locations" })
      ).closest('[role="dialog"]');
    };

    let dialog = await openLocations();
    fireEvent.mouseDown(within(dialog).getByRole("combobox"));
    fireEvent.click(await screen.findByRole("option", { name: "Downtown" }));
    fireEvent.click(within(dialog).getByRole("button", { name: "Add" }));
    await waitFor(() =>
      expect(api.put).toHaveBeenCalledWith("MenuLocation/menu/1/location/7"),
    );

    fireEvent.click(
      within(dialog).getAllByRole("button", { name: "Close" }).at(-1),
    );
    await waitFor(() =>
      expect(
        screen.queryByRole("heading", { name: "Manage Locations" }),
      ).not.toBeInTheDocument(),
    );

    dialog = await openLocations();
    expect(within(dialog).getByText("Downtown")).toBeInTheDocument();
    expect(
      within(dialog).queryByText("No locations assigned yet"),
    ).not.toBeInTheDocument();
  });

  it("lists a location added on the page after it loaded", async () => {
    // Location management, above on the same page, adds Downtown after Menu management loaded
    let locations = [];
    api.get.mockImplementation(async (url) => {
      if (url === "/Menu") return { data: menus };
      if (url === "/location") return { data: locations };
      return { data: [] };
    });
    render(<MenuManagement />);
    await screen.findByText("Lunch");
    locations = [{ location_id: 8, name: "Downtown" }];

    fireEvent.click(screen.getByRole("button", { name: "Assign Locations Lunch" }));
    const dialog = (
      await screen.findByRole("heading", { name: "Manage Locations" })
    ).closest('[role="dialog"]');
    fireEvent.mouseDown(within(dialog).getByRole("combobox"));

    expect(await screen.findByRole("option", { name: "Downtown" })).toBeInTheDocument();
  });

  it("drops a deleted location from the menu's assigned locations", async () => {
    // Downtown was assigned, then deleted in Location management, which removes it from the menu
    let lunchLocations = [8];
    let locations = [{ location_id: 8, name: "Downtown" }];
    api.get.mockImplementation(async (url) => {
      if (url === "/Menu")
        return { data: [{ ...menus[0], location_ids: lunchLocations }, menus[1]] };
      if (url === "/location") return { data: locations };
      return { data: [] };
    });
    render(<MenuManagement />);
    await screen.findByText("Lunch");
    lunchLocations = [];
    locations = [];

    fireEvent.click(screen.getByRole("button", { name: "Assign Locations Lunch" }));
    const dialog = (
      await screen.findByRole("heading", { name: "Manage Locations" })
    ).closest('[role="dialog"]');

    expect(within(dialog).queryByText("Downtown")).not.toBeInTheDocument();
    expect(within(dialog).getByText("No locations assigned yet")).toBeInTheDocument();
  });

  it("takes a location off the list when the server says it's already removed", async () => {
    api.get.mockImplementation(async (url) => {
      if (url === "/Menu")
        return { data: [{ ...menus[0], location_ids: [8] }, menus[1]] };
      if (url === "/location") return { data: [{ location_id: 8, name: "Downtown" }] };
      return { data: [] };
    });
    api.delete.mockRejectedValue({ response: { status: 404, data: "Menu and Menu Location were not found" } });
    render(<MenuManagement />);
    await screen.findByText("Lunch");

    fireEvent.click(screen.getByRole("button", { name: "Assign Locations Lunch" }));
    const dialog = (
      await screen.findByRole("heading", { name: "Manage Locations" })
    ).closest('[role="dialog"]');
    const chip = within(dialog).getByText("Downtown").closest(".MuiChip-root");
    fireEvent.click(chip.querySelector(".MuiChip-deleteIcon"));

    await waitFor(() =>
      expect(api.delete).toHaveBeenCalledWith("MenuLocation/menu/1/location/8"),
    );
    await waitFor(() => expect(within(dialog).queryByText("Downtown")).not.toBeInTheDocument());
    expect(within(dialog).queryByRole("alert")).not.toBeInTheDocument();
  });
});

