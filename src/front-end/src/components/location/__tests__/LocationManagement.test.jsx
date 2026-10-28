import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import LocationManagement from "../LocationManagement";
import api from "@/config/api";
import * as pricingService from "@/services/pricingService";

jest.mock("@/config/api", () => {
  const mockApi = { get: jest.fn(), post: jest.fn(), put: jest.fn(), delete: jest.fn() };
  return { __esModule: true, default: mockApi, api: mockApi };
});
jest.mock("@/services/pricingService");
// The dialog has its own tests; here it only needs to show which location it opened for
jest.mock("@/components/location/LocationPricingDialog", () => ({
  __esModule: true,
  default: ({ open, location, onClose }) =>
    open ? (
      <div role="dialog">
        Pricing: {location?.name}
        <button onClick={onClose}>Close</button>
      </div>
    ) : null,
}));

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

const locations = [
  { location_Id: 1, name: "Downtown", address_one: "123 Jasper Ave" },
  { location_Id: 2, name: "Southgate", address_one: "45 Whyte Ave" },
];

const menus = [
  { menu_id: 10, name: "Lunch" },
  { menu_id: 11, name: "Dinner" },
];

// Downtown has both menus; Southgate has none
let assignedMenus;
const getResponses = (url) => {
  if (url === "/Location") return { data: locations };
  if (url === "/Menu") return { data: menus };
  const match = url.match(/^\/Location\/(\d+)\/menus$/);
  if (match) return { data: assignedMenus[match[1]] ?? [] };
  throw new Error(`Unexpected GET ${url}`);
};

beforeEach(() => {
  jest.clearAllMocks();
  assignedMenus = { 1: [10, 11], 2: [] };
  api.get.mockImplementation(async (url) => getResponses(url));
  pricingService.getLocationPricingList.mockResolvedValue([]);
});

describe("LocationManagement menus column", () => {
  it("lists each location's menus by name, and flags a location with none", async () => {
    render(<LocationManagement />);

    expect(await screen.findByText("Lunch")).toBeInTheDocument();
    expect(screen.getByText("Dinner")).toBeInTheDocument();
    expect(screen.getByText("No menus")).toBeInTheDocument();
  });

  it("refreshes a location's menus when the Assign Menus dialog closes", async () => {
    render(<LocationManagement />);
    await screen.findByText("No menus");

    // Southgate is the second row
    fireEvent.click(screen.getAllByRole("button", { name: "Assign Menus" })[1]);
    const dialog = (await screen.findByRole("heading", { name: "Manage Menus" })).closest('[role="dialog"]');
    expect(within(dialog).getByText("Southgate")).toBeInTheDocument();

    assignedMenus[2] = [11];   // Dinner was assigned in the dialog
    // The bottom Close button (the ✕ in the title bar is labelled Close too)
    fireEvent.click(within(dialog).getAllByRole("button", { name: "Close" }).at(-1));

    await waitFor(() => expect(screen.queryByText("No menus")).not.toBeInTheDocument());
    // Chips only: the dialog's own list of menus may still be fading out
    expect(screen.getAllByText("Dinner").filter((el) => el.closest(".MuiChip-root"))).toHaveLength(2);
  });

  it("leaves the column blank when menus can't be loaded", async () => {
    jest.spyOn(console, "error").mockImplementation(() => {});
    api.get.mockImplementation(async (url) => {
      if (url === "/Menu") throw new Error("500");
      return getResponses(url);
    });
    render(<LocationManagement />);

    expect(await screen.findByText("Downtown")).toBeInTheDocument();
    await waitFor(() => expect(api.get).toHaveBeenCalledWith("/Menu"));
    expect(screen.queryByText("No menus")).not.toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });
});

describe("LocationManagement pricing column", () => {
  it("shows Custom for a location with its own pricing and Default for the rest", async () => {
    pricingService.getLocationPricingList.mockResolvedValue([
      { location_id: 1, location_name: "Downtown", source: "Location" },
      { location_id: 2, location_name: "Southgate", source: "Default" },
    ]);
    render(<LocationManagement />);

    expect(await screen.findByRole("button", { name: "Custom pricing for Downtown" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Default pricing for Southgate" })).toBeInTheDocument();
  });

  it("opens the location's pricing from its chip and refreshes the column on close", async () => {
    pricingService.getLocationPricingList.mockResolvedValueOnce([
      { location_id: 1, location_name: "Downtown", source: "Default" },
    ]);
    render(<LocationManagement />);

    fireEvent.click(await screen.findByRole("button", { name: "Default pricing for Downtown" }));
    expect(screen.getByRole("dialog")).toHaveTextContent("Pricing: Downtown");

    // Custom pricing was switched on in the dialog
    pricingService.getLocationPricingList.mockResolvedValueOnce([
      { location_id: 1, location_name: "Downtown", source: "Location" },
    ]);
    fireEvent.click(screen.getByRole("button", { name: "Close" }));

    expect(await screen.findByRole("button", { name: "Custom pricing for Downtown" })).toBeInTheDocument();
  });

  it("leaves the column blank when pricing can't be loaded", async () => {
    jest.spyOn(console, "error").mockImplementation(() => {});
    pricingService.getLocationPricingList.mockRejectedValue(new Error("403"));
    render(<LocationManagement />);

    expect(await screen.findByText("Downtown")).toBeInTheDocument();
    await waitFor(() => expect(pricingService.getLocationPricingList).toHaveBeenCalled());
    expect(screen.queryByRole("button", { name: /pricing for/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });
});
