import {
  render,
  screen,
  fireEvent,
  waitFor,
  within,
} from "@testing-library/react";
import MenuItemManagement from "../MenuItemManagement";
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

jest.mock("@/components/common/ManagementPage", () => ({
  __esModule: true,
  default: ({
    title,
    addLabel,
    onAdd,
    error,
    success,
    successAction,
    successAutoHideMs,
    onSuccessClose,
    gridProps,
    children,
  }) => (
    <div>
      <h1>{title}</h1>
      <button onClick={onAdd}>{addLabel}</button>
      {error && <p data-testid="page-error">{error}</p>}
      {success && (
        <div
          data-testid="page-success"
          data-autohide={successAutoHideMs ?? "default"}
        >
          {success}
          {successAction}
          <button onClick={onSuccessClose}>Dismiss</button>
        </div>
      )}
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

// The other dialogs on the page aren't tested here
jest.mock("@/components/admin/TagsDialog", () => ({
  __esModule: true,
  default: () => null,
}));
jest.mock("@/components/admin/MenuAssignmentDialog", () => ({
  __esModule: true,
  default: () => null,
}));
jest.mock("@/components/admin/CategoryManagementDialog", () => ({
  __esModule: true,
  default: () => null,
}));

const CATEGORIES = [
  { category_id: 1, category_name: "Rolls" },
  { category_id: 2, category_name: "Nigiri" },
];

const SPICY = { tag_id: 3, name: "Spicy", color_code: "#FF5733" };
const SALMON = {
  item_id: 7,
  name: "Salmon Nigiri",
  description: "Two pieces",
  category_Id: 2,
  item_Image_Url: null,
  status: "Available",
  tags: [SPICY],
};

const mockGets = (menuAssignments = []) =>
  api.get.mockImplementation((url) => {
    if (url === "/menuItem") return Promise.resolve({ data: [SALMON] });
    if (url.toLowerCase() === "/menuitem/7")
      return Promise.resolve({ data: { ...SALMON, menuAssignments } });
    if (url === "/Category") return Promise.resolve({ data: CATEGORIES });
    return Promise.resolve({ data: [] });
  });

const listReloads = () =>
  api.get.mock.calls.filter(([url]) => url === "/menuItem").length;

const nameInput = () => screen.getByLabelText(/^Name/);
const descriptionInput = () => screen.getByLabelText(/^Description/);

const openCreate = async () => {
  render(<MenuItemManagement />);
  await screen.findByText("Salmon Nigiri");
  fireEvent.click(screen.getByRole("button", { name: "Add New Menu Item" }));
  await screen.findByText("Create Menu Item");
};

const typeNameAndDescription = () => {
  fireEvent.change(nameInput(), { target: { value: "Spicy Tuna Roll" } });
  fireEvent.change(descriptionInput(), { target: { value: "Eight pieces" } });
};

const choose = (label, option) => {
  fireEvent.mouseDown(screen.getByRole("combobox", { name: label }));
  fireEvent.click(screen.getByRole("option", { name: option }));
};

const chooseImage = async () => {
  const file = new File(["img"], "roll.png", { type: "image/png" });
  const input = screen.getByRole("dialog").querySelector('input[type="file"]');
  fireEvent.change(input, { target: { files: [file] } });
  await screen.findByAltText("Item preview");
  return file;
};

const submit = () =>
  fireEvent.submit(
    screen.getByRole("button", { name: "Save" }).closest("form"),
  );

const expectTypedValuesKept = () => {
  expect(nameInput()).toHaveValue("Spicy Tuna Roll");
  expect(descriptionInput()).toHaveValue("Eight pieces");
};

describe("Menu item form keeps what was typed", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockGets();
  });

  it("after choosing an image", async () => {
    await openCreate();
    typeNameAndDescription();

    await chooseImage();

    expectTypedValuesKept();
  });

  it("after the save fails and an error is shown", async () => {
    api.post.mockRejectedValue({ response: { status: 500 } });
    await openCreate();
    typeNameAndDescription();
    choose(/^Category/, "Rolls");

    submit();

    expect(
      await within(screen.getByRole("dialog")).findByText(
        "Failed to save menu item",
      ),
    ).toBeInTheDocument();
    expectTypedValuesKept();
  });

  it("while the save is in progress", async () => {
    api.post.mockReturnValue(new Promise(() => {})); // never settles
    await openCreate();
    typeNameAndDescription();
    choose(/^Category/, "Rolls");

    submit();

    await waitFor(() =>
      expect(
        within(screen.getByRole("dialog")).getByRole("progressbar"),
      ).toBeInTheDocument(),
    );
    expectTypedValuesKept();
  });
});

describe("Menu item create and edit", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockGets();
  });

  it("creates an item, then uploads its image with the new id", async () => {
    api.post.mockImplementation((url) =>
      url === "/menuItem"
        ? Promise.resolve({ data: { ...SALMON, item_id: 12 } })
        : Promise.resolve({
            data: { item_id: 12, image_url: "/images/12.png" },
          }),
    );
    await openCreate();
    typeNameAndDescription();
    choose(/^Category/, "Rolls");
    const file = await chooseImage();

    submit();

    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith("/menuItem", {
        name: "Spicy Tuna Roll",
        description: "Eight pieces",
        category_id: 1,
      }),
    );
    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith(
        "/menuItem/12/image",
        expect.any(FormData),
        { headers: { "Content-Type": "multipart/form-data" } },
      ),
    );
    const uploaded = api.post.mock.calls.find(
      ([url]) => url === "/menuItem/12/image",
    )[1];
    expect(uploaded.get("file")).toBe(file);
    await waitFor(() =>
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument(),
    );
  });

  it("opens an existing item with its values and category, and saves changes to it", async () => {
    api.put.mockResolvedValue({ data: SALMON });
    render(<MenuItemManagement />);
    const row = (await screen.findByText("Salmon Nigiri")).closest("tr");

    fireEvent.click(within(row).getByRole("button", { name: "Edit" }));
    await screen.findByText("Edit Menu Item");

    expect(nameInput()).toHaveValue("Salmon Nigiri");
    expect(descriptionInput()).toHaveValue("Two pieces");
    expect(
      screen.getByRole("combobox", { name: /^Category/ }),
    ).toHaveTextContent("Nigiri");

    fireEvent.change(descriptionInput(), { target: { value: "Three pieces" } });
    submit();

    await waitFor(() =>
      expect(api.put).toHaveBeenCalledWith("/menuItem/7", {
        name: "Salmon Nigiri",
        description: "Three pieces",
        category_id: 2,
      }),
    );
    expect(api.post).not.toHaveBeenCalled();
  });

  it("doesn't upload an image left over from a cancelled create", async () => {
    api.put.mockResolvedValue({ data: SALMON });
    await openCreate();
    await chooseImage();
    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
    await waitFor(() =>
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument(),
    );

    const row = screen.getByText("Salmon Nigiri").closest("tr");
    fireEvent.click(within(row).getByRole("button", { name: "Edit" }));
    await screen.findByText("Edit Menu Item");
    submit();

    await waitFor(() => expect(api.put).toHaveBeenCalled());
    expect(api.post).not.toHaveBeenCalled();
  });

  it("starts a new item empty after editing one", async () => {
    render(<MenuItemManagement />);
    const row = (await screen.findByText("Salmon Nigiri")).closest("tr");
    fireEvent.click(within(row).getByRole("button", { name: "Edit" }));
    await screen.findByText("Edit Menu Item");
    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
    await waitFor(() =>
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument(),
    );

    fireEvent.click(screen.getByRole("button", { name: "Add New Menu Item" }));
    await screen.findByText("Create Menu Item");

    expect(nameInput()).toHaveValue("");
    expect(descriptionInput()).toHaveValue("");
  });
});

describe("Menu item delete", () => {
  const ASSIGNED_MESSAGE =
    "Cannot delete item - it is currently assigned to one or more menus. Remove menu assignments first.";

  beforeEach(() => jest.clearAllMocks());

  const clickRowDelete = async () => {
    render(<MenuItemManagement />);
    const row = (await screen.findByText("Salmon Nigiri")).closest("tr");
    fireEvent.click(within(row).getByRole("button", { name: "Delete" }));
  };

  const confirmDelete = async () => {
    const confirm = (
      await screen.findByText(/Are you sure you want to delete "Salmon Nigiri"/)
    ).closest('[role="dialog"]');
    fireEvent.click(within(confirm).getByRole("button", { name: "Delete" }));
  };

  it("won't offer to delete an item that is assigned to a menu", async () => {
    mockGets([{ assignment_Id: 3, menu_Id: 1, item_Id: 7 }]);
    await clickRowDelete();

    expect(await screen.findByTestId("page-error")).toHaveTextContent(
      ASSIGNED_MESSAGE,
    );
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(api.delete).not.toHaveBeenCalled();
  });

  it("deletes an unassigned item after confirming, then reloads the list", async () => {
    mockGets([]);
    api.delete.mockResolvedValue({
      data: { message: "Menu item deleted successfully.", item_id: 7 },
    });
    await clickRowDelete();

    await confirmDelete();

    await waitFor(() => expect(api.delete).toHaveBeenCalledWith("/menuitem/7"));
    await waitFor(() => expect(listReloads()).toBe(2));
    expect(screen.queryByTestId("page-error")).not.toBeInTheDocument();
  });

  it("shows the server's reason when the delete is refused", async () => {
    mockGets([]);
    api.delete.mockRejectedValue({
      response: {
        status: 409,
        data: {
          message:
            "Cannot delete this item because it is assigned to a menu. Remove its menu assignments first.",
        },
      },
    });
    await clickRowDelete();

    await confirmDelete();

    expect(await screen.findByTestId("page-error")).toHaveTextContent(
      "Cannot delete this item because it is assigned to a menu. Remove its menu assignments first.",
    );
    await waitFor(() =>
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument(),
    );
  });

  it("falls back to a general message when the server gives no reason", async () => {
    mockGets([]);
    api.delete.mockRejectedValue(new Error("Network Error"));
    await clickRowDelete();

    await confirmDelete();

    expect(await screen.findByTestId("page-error")).toHaveTextContent(
      "Failed to delete menu item",
    );
  });

  it("shows the server's reason when the item can't be looked up", async () => {
    api.get.mockImplementation((url) =>
      url === "/menuItem"
        ? Promise.resolve({ data: [SALMON] })
        : url === "/Category"
          ? Promise.resolve({ data: CATEGORIES })
          : url.toLowerCase() === "/menuitem/7"
            ? Promise.reject({
                response: {
                  status: 404,
                  data: { message: "Menu item was not found" },
                },
              })
            : Promise.resolve({ data: [] }),
    );
    await clickRowDelete();

    expect(await screen.findByTestId("page-error")).toHaveTextContent(
      "Menu item was not found",
    );
    expect(api.delete).not.toHaveBeenCalled();
  });
});

describe("Removing a tag from the grid", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockGets();
    api.delete.mockResolvedValue({ data: {} });
    api.post.mockResolvedValue({ data: {} });
  });

  const clickTagX = async (tagName) => {
    const row = (await screen.findByText("Salmon Nigiri")).closest("tr");
    const chip = within(row).getByRole("button", { name: tagName });
    fireEvent.click(chip.querySelector(".MuiChip-deleteIcon"));
  };

  const removeSpicy = async () => {
    render(<MenuItemManagement />);
    await clickTagX("Spicy");
    return screen.findByTestId("page-success");
  };

  it("removes the tag and offers Undo for 10 seconds", async () => {
    const alert = await removeSpicy();

    expect(api.delete).toHaveBeenCalledWith("/menuitem/7/tags/3");
    expect(alert).toHaveTextContent('Removed "Spicy" from Salmon Nigiri');
    expect(
      within(alert).getByRole("button", { name: "Undo" }),
    ).toBeInTheDocument();
    expect(alert).toHaveAttribute("data-autohide", "10000");
    expect(listReloads()).toBe(2);
  });

  it("puts the tag back when Undo is clicked", async () => {
    const alert = await removeSpicy();

    fireEvent.click(within(alert).getByRole("button", { name: "Undo" }));

    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith("/menuitem/7/tags/3"),
    );
    expect(await screen.findByTestId("page-success")).toHaveTextContent(
      'Restored "Spicy"',
    );
    expect(
      screen.queryByRole("button", { name: "Undo" }),
    ).not.toBeInTheDocument();
    expect(screen.getByTestId("page-success")).toHaveAttribute(
      "data-autohide",
      "default",
    );
    await waitFor(() => expect(listReloads()).toBe(3));
  });

  it("treats 'already assigned' on Undo as done", async () => {
    api.post.mockRejectedValue({
      response: { status: 409, data: "Tag already assigned to this menu item" },
    });
    const alert = await removeSpicy();

    fireEvent.click(within(alert).getByRole("button", { name: "Undo" }));

    expect(await screen.findByText('Restored "Spicy"')).toBeInTheDocument();
    expect(screen.queryByTestId("page-error")).not.toBeInTheDocument();
  });

  it("shows the server's reason when Undo fails", async () => {
    api.post.mockRejectedValue({
      response: {
        status: 404,
        data: "Item you were searching for was not found.",
      },
    });
    const alert = await removeSpicy();

    fireEvent.click(within(alert).getByRole("button", { name: "Undo" }));

    expect(await screen.findByTestId("page-error")).toHaveTextContent(
      "Item you were searching for was not found.",
    );
    expect(screen.queryByTestId("page-success")).not.toBeInTheDocument();
  });

  it("drops Undo when the alert is dismissed", async () => {
    const alert = await removeSpicy();

    fireEvent.click(within(alert).getByRole("button", { name: "Dismiss" }));

    expect(
      screen.queryByRole("button", { name: "Undo" }),
    ).not.toBeInTheDocument();
  });

  it("shows the server's reason and no Undo when the removal fails", async () => {
    api.delete.mockRejectedValue({
      response: { status: 409, data: "Item does not exist on menu" },
    });
    render(<MenuItemManagement />);

    await clickTagX("Spicy");

    expect(await screen.findByTestId("page-error")).toHaveTextContent(
      "Item does not exist on menu",
    );
    expect(screen.queryByTestId("page-success")).not.toBeInTheDocument();
  });
});
