import {
  render,
  screen,
  fireEvent,
  waitFor,
  within,
} from "@testing-library/react";
import TagManagement from "../TagManagement";
import api from "@/config/api";

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

jest.mock("@/config/api", () => {
  const mockApi = {
    get: jest.fn(),
    post: jest.fn(),
    put: jest.fn(),
    delete: jest.fn(),
  };
  return { __esModule: true, default: mockApi, api: mockApi };
});

jest.mock("@/components/common/ManagementPage", () => ({
  __esModule: true,
  default: ({
    title,
    addLabel,
    onAdd,
    error,
    success,
    gridProps,
    children,
  }) => (
    <div>
      <h1>{title}</h1>
      <button onClick={onAdd}>{addLabel}</button>
      {error && <p data-testid="page-error">{error}</p>}
      {success && <p data-testid="page-success">{success}</p>}
      <table>
        <tbody>
          {gridProps.rows.map((row) => (
            <tr key={gridProps.getRowId(row)}>
              {gridProps.columns.map((col) => (
                <td key={col.field}>
                  {col.renderCell
                    ? col.renderCell({ value: row[col.field], row })
                    : row[col.field]}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
      {children}
    </div>
  ),
}));

const SPICY = { tag_id: 5, tag_name: "Spicy", tag_color: "#FF5733" };

const ITEMS = [
  {
    item_id: 66,
    name: "Hot Stone Steak",
    description: "Cooked at your table",
    image_url: "/images/66.png",
  },
  { item_id: 67, name: "Dragon Roll", description: "Eel", image_url: null },
];

const mockGets = (items = ITEMS) =>
  api.get.mockImplementation((url) => {
    if (url === "/Tag/colors")
      return Promise.resolve({ status: 200, data: [SPICY] });
    if (url === "/tag/5/menu_items")
      return Promise.resolve({ status: 200, data: items });
    return Promise.resolve({ status: 200, data: [] });
  });

const rowAction = async (label) => {
  const row = (await screen.findByText("#FF5733")).closest("tr");
  fireEvent.click(within(row).getByRole("button", { name: label }));
};

describe("Tag delete dialog", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockGets();
  });

  it("deletes the tag after confirming, then reloads the list", async () => {
    api.delete.mockResolvedValue({
      status: 200,
      data: { detail: "Tag deleted" },
    });
    render(<TagManagement />);

    await rowAction("Delete");
    const dialog = (
      await screen.findByText("Are you sure you want to delete this tag?")
    ).closest('[role="dialog"]');
    fireEvent.click(within(dialog).getByRole("button", { name: "Delete" }));

    await waitFor(() => expect(api.delete).toHaveBeenCalledWith("/tag/5"));
    expect(await screen.findByTestId("page-success")).toHaveTextContent(
      "Tag deleted successfully",
    );
    await waitFor(() =>
      expect(
        api.get.mock.calls.filter(([url]) => url === "/Tag/colors"),
      ).toHaveLength(2),
    );
  });

  it("shows the server's reason inside the dialog when the delete fails", async () => {
    api.delete.mockRejectedValue({
      response: { status: 404, data: { detail: "Tag not found" } },
    });
    render(<TagManagement />);

    await rowAction("Delete");
    const dialog = (
      await screen.findByText("Are you sure you want to delete this tag?")
    ).closest('[role="dialog"]');
    fireEvent.click(within(dialog).getByRole("button", { name: "Delete" }));

    expect(
      await within(dialog).findByText("Tag not found"),
    ).toBeInTheDocument();
  });
});

describe("Tag menu items dialog", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockGets();
  });

  const openItems = async () => {
    render(<TagManagement />);
    await rowAction("View Items");
    return (await screen.findByText("Hot Stone Steak")).closest(
      '[role="dialog"]',
    );
  };

  it("lists the tag's menu items with their images", async () => {
    const dialog = await openItems();

    expect(api.get).toHaveBeenCalledWith("/tag/5/menu_items");
    expect(within(dialog).getByAltText("Hot Stone Steak")).toHaveAttribute(
      "src",
      "/images/66.png",
    );
    expect(
      within(dialog).queryByAltText("Dragon Roll"),
    ).not.toBeInTheDocument();
  });

  it("removes the tag from one item using the menu item route", async () => {
    api.delete.mockResolvedValue({ status: 200, data: {} });
    const dialog = await openItems();

    fireEvent.click(
      within(dialog).getByRole("button", {
        name: "Remove tag from Hot Stone Steak",
      }),
    );

    await waitFor(() =>
      expect(api.delete).toHaveBeenCalledWith("/menuItem/66/tags/5"),
    );
    await waitFor(() =>
      expect(
        within(dialog).queryByText("Hot Stone Steak"),
      ).not.toBeInTheDocument(),
    );
    expect(within(dialog).getByText("Dragon Roll")).toBeInTheDocument();
    expect(
      within(dialog).getByText("Tag removed from Hot Stone Steak"),
    ).toBeInTheDocument();
    expect(screen.queryByTestId("page-success")).not.toBeInTheDocument();
  });

  it("doesn't carry the dialog's message onto the page after closing", async () => {
    api.delete.mockResolvedValue({ status: 200, data: {} });
    const dialog = await openItems();
    fireEvent.click(
      within(dialog).getByRole("button", {
        name: "Remove tag from Hot Stone Steak",
      }),
    );
    await within(dialog).findByText("Tag removed from Hot Stone Steak");

    fireEvent.click(within(dialog).getByText("Close", { selector: "button" }));

    await waitFor(() =>
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument(),
    );
    expect(screen.queryByTestId("page-success")).not.toBeInTheDocument();
    expect(
      screen.queryByText("Tag removed from Hot Stone Steak"),
    ).not.toBeInTheDocument();
  });

  it("shows the server's reason in the dialog when removing fails", async () => {
    api.delete.mockRejectedValue({
      response: { status: 409, data: "Item does not exist on menu" },
    });
    const dialog = await openItems();

    fireEvent.click(
      within(dialog).getByRole("button", {
        name: "Remove tag from Hot Stone Steak",
      }),
    );

    expect(
      await within(dialog).findByText("Item does not exist on menu"),
    ).toBeInTheDocument();
    expect(within(dialog).getByText("Hot Stone Steak")).toBeInTheDocument();
  });

  it("says so when no items use the tag", async () => {
    mockGets([]);
    render(<TagManagement />);

    await rowAction("View Items");

    expect(
      await screen.findByText("No menu items found with this tag"),
    ).toBeInTheDocument();
  });
});
