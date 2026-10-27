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
});
