import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import MenuManagement from "../MenuManagement";
import api from "@/config/api";

jest.mock("@/config/api", () => {
  const mockApi = { get: jest.fn(), post: jest.fn(), put: jest.fn(), delete: jest.fn() };
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
    DataGrid: (props) => React.createElement(actual.DataGrid, { ...props, disableVirtualization: true }),
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
  api.get.mockImplementation(async (url) => ({ data: url === "/Menu" ? menus : [] }));
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
    api.delete.mockRejectedValue({ response: { status: 409, data: "Menu is in use" } });
    render(<MenuManagement />);
    await screen.findByText("Lunch");

    fireEvent.click(screen.getAllByRole("button", { name: "Delete" })[0]);
    const dialog = (await screen.findByText(/delete the menu "Lunch"/)).closest('[role="dialog"]');
    fireEvent.click(within(dialog).getByRole("button", { name: "Delete Menu" }));

    expect(await within(dialog).findByText("Menu is in use")).toBeInTheDocument();
    await waitFor(() => expect(within(dialog).getByRole("button", { name: "Delete Menu" })).toBeEnabled());
  });

  it("shows a location added in Manage Locations when the dialog is opened again", async () => {
    let lunchLocations = [];
    api.get.mockImplementation(async (url) => {
      if (url === "/Menu") return { data: [{ ...menus[0], menuLocations: lunchLocations }, menus[1]] };
      if (url === "/location") return { data: [{ location_Id: 7, name: "Downtown" }] };
      return { data: [] };
    });
    api.put.mockImplementation(async () => {
      lunchLocations = [{ location_Id: 7 }];   // saved on the server
      return { data: {} };
    });
    render(<MenuManagement />);
    await screen.findByText("Lunch");

    const openLocations = async () => {
      fireEvent.click(screen.getAllByRole("button", { name: "Assign Locations" })[0]);
      return (await screen.findByRole("heading", { name: "Manage Locations" })).closest('[role="dialog"]');
    };

    let dialog = await openLocations();
    fireEvent.mouseDown(within(dialog).getByRole("combobox"));
    fireEvent.click(await screen.findByRole("option", { name: "Downtown" }));
    fireEvent.click(within(dialog).getByRole("button", { name: "Add" }));
    await waitFor(() => expect(api.put).toHaveBeenCalledWith("MenuLocation/menu/1/location/7"));

    fireEvent.click(within(dialog).getAllByRole("button", { name: "Close" }).at(-1));
    await waitFor(() => expect(screen.queryByRole("heading", { name: "Manage Locations" })).not.toBeInTheDocument());
    await waitFor(() => expect(api.get.mock.calls.filter(([url]) => url === "/Menu")).toHaveLength(2));

    dialog = await openLocations();
    expect(within(dialog).getByText("Downtown")).toBeInTheDocument();
    expect(within(dialog).queryByText("No locations assigned yet")).not.toBeInTheDocument();
  });
});
