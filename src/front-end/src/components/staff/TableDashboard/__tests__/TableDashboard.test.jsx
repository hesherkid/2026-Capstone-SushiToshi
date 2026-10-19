import { render, screen, within, fireEvent, waitFor } from "@testing-library/react";
import TableDashboard from "@/components/staff/TableDashboard";
import api from "@/config/api";
import { useAuth } from "@/hooks/useAuth";

jest.mock("@/config/api", () => {
  const mockApi = { get: jest.fn(), post: jest.fn(), delete: jest.fn() };
  return { __esModule: true, default: mockApi, api: mockApi };
});

jest.mock("@/hooks/useAuth", () => ({ useAuth: jest.fn() }));

jest.mock("@/utils/storage", () => ({
  __esModule: true,
  default: { get: jest.fn(() => "1") },
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

const tables = [
  { table_Id: 1, table_number: 1, seat_count: 4, is_active: true, tableGroup_Id: null },
  { table_Id: 2, table_number: 2, seat_count: 2, is_active: true, tableGroup_Id: null },
  { table_Id: 3, table_number: 3, seat_count: 6, is_active: false, tableGroup_Id: null },
  { table_Id: 4, table_number: 4, seat_count: 4, is_active: true, tableGroup_Id: 10 },
];
const groups = [
  {
    tableGroup_Id: 10,
    group_Name: "Patio",
    is_Active: true,
    tables: [{ table_Id: 4, table_number: 4, seat_count: 4, is_active: true }],
  },
];
// Table 2 is in a session, table 3 is inactive
const emptyTables = [{ table_Id: 1 }, { table_Id: 4 }];

const mockApiData = () => {
  api.get.mockImplementation((url) => {
    if (url.startsWith("/TableEntity/empty")) return Promise.resolve({ data: emptyTables });
    if (url.startsWith("/TableEntity")) return Promise.resolve({ data: tables });
    if (url.startsWith("/TableGroup")) return Promise.resolve({ data: groups });
    return Promise.resolve({ data: [] });
  });
};

const rowFor = (name) => screen.getByText(name).closest('[role="row"]');

describe("Table dashboard", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockApiData();
    useAuth.mockReturnValue({ userRole: "admin" });
  });

  it("lists every table, including inactive and grouped ones, with its status", async () => {
    render(<TableDashboard />);

    expect(await screen.findByText("Table 3")).toBeInTheDocument();
    expect(within(rowFor("Table 1")).getByText("Available")).toBeInTheDocument();
    expect(within(rowFor("Table 2")).getByText("In session")).toBeInTheDocument();
    expect(within(rowFor("Table 3")).getByText("Inactive")).toBeInTheDocument();
    expect(within(rowFor("Table 4")).getByText("Patio")).toBeInTheDocument();

    // One status fetch for the location instead of a request per table
    expect(api.get).toHaveBeenCalledWith("/TableEntity/empty?locationId=1");
    expect(api.post).not.toHaveBeenCalled();
  });

  it("shows status counts and filters rows by status", async () => {
    render(<TableDashboard />);
    await screen.findByText("Table 1");

    // StatCard renders the count before its label
    const availableCard = screen
      .getByText("Tables Available")
      .closest(".MuiCard-root");
    expect(availableCard).toHaveTextContent("2Tables Available");

    fireEvent.click(screen.getByRole("button", { name: "Available" }));
    await waitFor(() => expect(screen.queryByText("Table 2")).not.toBeInTheDocument());
    expect(screen.queryByText("Table 3")).not.toBeInTheDocument();
    expect(screen.getByText("Table 1")).toBeInTheDocument();
    expect(screen.getByText("Table 4")).toBeInTheDocument();
  });

  it("hides status changes for tables in session", async () => {
    render(<TableDashboard />);
    await screen.findByText("Table 2");

    expect(within(rowFor("Table 1")).getByRole("button", { name: "Deactivate" })).toBeInTheDocument();
    expect(within(rowFor("Table 2")).queryByRole("button", { name: "Deactivate" })).not.toBeInTheDocument();
    expect(within(rowFor("Table 3")).getByRole("button", { name: "Activate" })).toBeInTheDocument();
  });

  it("asks before deleting a table", async () => {
    api.delete.mockResolvedValue({});
    render(<TableDashboard />);
    await screen.findByText("Table 1");

    fireEvent.click(within(rowFor("Table 1")).getByRole("button", { name: "Delete table" }));
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText(/Delete Table 1\?/)).toBeInTheDocument();

    fireEvent.click(within(dialog).getByRole("button", { name: "Delete" }));
    await waitFor(() => expect(api.delete).toHaveBeenCalledWith("/TableEntity/1"));
  });

  it("does not let staff delete a table", async () => {
    useAuth.mockReturnValue({ userRole: "staff" });
    render(<TableDashboard />);
    await screen.findByText("Table 1");

    expect(within(rowFor("Table 1")).queryByRole("button", { name: "Delete table" })).not.toBeInTheDocument();
    expect(within(rowFor("Table 1")).getByRole("button", { name: "Edit table" })).toBeInTheDocument();
  });

  it("lists table groups with their tables and seats", async () => {
    render(<TableDashboard />);
    await screen.findByText("Table 1");

    fireEvent.click(screen.getByRole("tab", { name: "Table groups (1)" }));
    const row = await waitFor(() => rowFor("Patio"));
    expect(within(row).getByText("Available")).toBeInTheDocument();
    // Table 4 seats 4
    expect(within(row).getAllByText("4")).toHaveLength(2);
  });
});
