import { render, screen } from "@testing-library/react";
import ManagementGrid from "../ManagementGrid";
import { warnOnMissingKeys } from "@/utils/missingKeyWarnings";

// The real grid, with what it needs to render in jsdom
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
      React.createElement(actual.DataGrid, { ...props, disableVirtualization: true }),
  };
});

if (typeof window.ResizeObserver === "undefined") {
  window.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
}

describe("ManagementGrid", () => {
  let warn;
  beforeEach(() => {
    warn = jest.spyOn(console, "warn").mockImplementation(() => {});
  });
  afterEach(() => warn.mockRestore());

  it("doesn't report computed columns as keys missing from the API response", async () => {
    // Rows as the API returns them in development, with the missing-key warnings on
    const rows = warnOnMissingKeys(
      [{ assignment_id: 3, menu_name: "All Day Menu" }],
      "GET /menuItem/:id",
    );
    const columns = [
      // Computed: the grid still reads row.menu
      { field: "menu", headerName: "Menu", valueGetter: (value, row) => row.menu_name },
      { field: "actions", type: "actions", getActions: () => [] },
    ];

    render(<ManagementGrid rows={rows} columns={columns} getRowId={(row) => row.assignment_id} />);

    expect(await screen.findByText("All Day Menu")).toBeInTheDocument();
    expect(warn).not.toHaveBeenCalledWith(expect.stringContaining("[api]"));
  });
});
