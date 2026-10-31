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
    if (url === "/Tag") return Promise.resolve({ status: 200, data: [SPICY] });
    if (url === "/tag/5/menu_items")
      return Promise.resolve({ status: 200, data: items });
    return Promise.resolve({ status: 200, data: [] });
  });

const rowAction = async (label) => {
  const row = (await screen.findByText("#FF5733")).closest("tr");
  fireEvent.click(within(row).getByRole("button", { name: label }));
};

const tagListLoads = () => api.get.mock.calls.filter(([url]) => url === "/Tag");

const openForm = async (heading) => {
  if (heading === "Add New Tag") {
    await screen.findByText("#FF5733");
    fireEvent.click(screen.getByRole("button", { name: "Add New Tag" }));
  } else {
    await rowAction("Edit");
  }
  return (await screen.findByRole("heading", { name: heading })).closest(
    '[role="dialog"]',
  );
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
      expect(api.get.mock.calls.filter(([url]) => url === "/Tag")).toHaveLength(
        2,
      ),
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

  it("doesn't show a failed delete's error on the page after cancelling", async () => {
    api.delete.mockRejectedValue({
      response: { status: 404, data: { detail: "Tag not found" } },
    });
    render(<TagManagement />);

    await rowAction("Delete");
    const dialog = (
      await screen.findByText("Are you sure you want to delete this tag?")
    ).closest('[role="dialog"]');
    fireEvent.click(within(dialog).getByRole("button", { name: "Delete" }));
    await within(dialog).findByText("Tag not found");
    fireEvent.click(within(dialog).getByRole("button", { name: "Cancel" }));

    await waitFor(() =>
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument(),
    );
    expect(screen.queryByTestId("page-error")).not.toBeInTheDocument();
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

describe("Tag add/edit dialog", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockGets();
  });

  it("doesn't show an earlier page error when a tag is opened for editing", async () => {
    api.delete.mockRejectedValue({
      response: { status: 404, data: { detail: "Tag not found" } },
    });
    render(<TagManagement />);

    // A failed delete leaves an error behind
    await rowAction("Delete");
    const confirm = (
      await screen.findByText("Are you sure you want to delete this tag?")
    ).closest('[role="dialog"]');
    fireEvent.click(within(confirm).getByRole("button", { name: "Delete" }));
    await within(confirm).findByText("Tag not found");
    fireEvent.click(within(confirm).getByRole("button", { name: "Cancel" }));
    await waitFor(() =>
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument(),
    );

    await rowAction("Edit");
    const dialog = (
      await screen.findByRole("heading", { name: "Update Tag" })
    ).closest('[role="dialog"]');
    expect(within(dialog).queryByText("Tag not found")).not.toBeInTheDocument();
  });

  it("creates a tag, closes the dialog and reloads the list", async () => {
    api.post.mockResolvedValue({ status: 201, data: {} });
    render(<TagManagement />);
    const dialog = await openForm("Add New Tag");

    fireEvent.change(within(dialog).getByLabelText("Tag Name"), {
      target: { value: "Vegan" },
    });
    fireEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith("/Tag", {
        tag_name: "Vegan",
        tag_color: expect.stringMatching(/^#[0-9a-f]{6}$/),
      }),
    );
    expect(await screen.findByTestId("page-success")).toHaveTextContent(
      "Tag created successfully",
    );
    await waitFor(() =>
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument(),
    );
    await waitFor(() => expect(tagListLoads()).toHaveLength(2));
  });

  it("updates a tag through its id", async () => {
    api.put.mockResolvedValue({ status: 200, data: {} });
    render(<TagManagement />);
    const dialog = await openForm("Update Tag");

    expect(within(dialog).getByLabelText("Tag Name")).toHaveValue("Spicy");
    fireEvent.change(within(dialog).getByLabelText("Tag Name"), {
      target: { value: "Extra Spicy" },
    });
    fireEvent.click(within(dialog).getByRole("button", { name: "Update" }));

    await waitFor(() =>
      expect(api.put).toHaveBeenCalledWith("/Tag/5", {
        tag_id: 5,
        tag_name: "Extra Spicy",
        tag_color: "#FF5733",
      }),
    );
    expect(await screen.findByTestId("page-success")).toHaveTextContent(
      "Tag updated successfully",
    );
  });

  it("keeps the dialog open with the server's reason when saving fails", async () => {
    api.post.mockRejectedValue({
      response: { status: 400, data: { detail: "Tag already exists" } },
    });
    render(<TagManagement />);
    const dialog = await openForm("Add New Tag");

    fireEvent.change(within(dialog).getByLabelText("Tag Name"), {
      target: { value: "Spicy" },
    });
    fireEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(
      await within(dialog).findByText("Tag already exists"),
    ).toBeInTheDocument();
    expect(screen.queryByTestId("page-error")).not.toBeInTheDocument();
    expect(screen.queryByTestId("page-success")).not.toBeInTheDocument();
    expect(tagListLoads()).toHaveLength(1);
  });

  it("asks for a name without calling the server", async () => {
    render(<TagManagement />);
    const dialog = await openForm("Add New Tag");

    fireEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(
      await within(dialog).findByText("Tag name is required"),
    ).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
  });
});

describe("Tag list", () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it("shows a page error when the tags can't be loaded", async () => {
    api.get.mockRejectedValue(new Error("network"));
    render(<TagManagement />);

    expect(await screen.findByTestId("page-error")).toHaveTextContent(
      "Failed to fetch tags",
    );
  });
});
