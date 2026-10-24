import {
  render,
  screen,
  fireEvent,
  waitFor,
  within,
} from "@testing-library/react";
import CategoryManagementDialog from "../CategoryManagementDialog";
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

const HOT_STONE = {
  category_id: 5,
  category_name: "Hot Stone",
  description: "Cooked at your table",
  adult_limit: 1,
  child_limit: 1,
  senior_limit: 1,
  toddler_limit: 0,
  per_order_limit: 0,
  over_limit_behavior: "Charge",
};

const renderDialog = (selectedCategory = null) => {
  const onClose = jest.fn();
  const onSuccess = jest.fn();
  render(
    <CategoryManagementDialog
      open
      onClose={onClose}
      selectedCategory={selectedCategory}
      onSuccess={onSuccess}
    />,
  );
  return { onClose, onSuccess };
};

const setNumber = (label, value) =>
  fireEvent.change(screen.getByLabelText(label), { target: { value } });

const choose = (label, option) => {
  fireEvent.mouseDown(screen.getByRole("combobox", { name: label }));
  fireEvent.click(screen.getByRole("option", { name: option }));
};

describe("Category dialog", () => {
  beforeEach(() => jest.clearAllMocks());

  it("creates a category with every limit and what happens past the per-person limit", async () => {
    api.post.mockResolvedValue({ data: {} });
    const { onClose, onSuccess } = renderDialog();

    fireEvent.change(screen.getByLabelText(/^Name/), {
      target: { value: "Sashimi" },
    });
    setNumber(/^Adult/, "2");
    setNumber(/^Senior/, "2");
    setNumber(/^Child/, "1");
    setNumber(/^Toddler/, "1");
    setNumber("Per Order Limit", "30");
    choose("Past per-person limit", "Charge the item's price");
    fireEvent.click(screen.getByRole("button", { name: "Create Category" }));

    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith("/Category", {
        name: "Sashimi",
        description: "",
        adult_limit: 2,
        child_limit: 1,
        senior_limit: 2,
        toddler_limit: 1,
        per_order_limit: 30,
        over_limit_behavior: "Charge",
      }),
    );
    expect(onSuccess).toHaveBeenCalled();
    expect(onClose).toHaveBeenCalled();
  });

  it("starts new categories on Block", () => {
    renderDialog();

    expect(
      screen.getByRole("combobox", { name: "Past per-person limit" }),
    ).toHaveTextContent("Block");
  });

  it("loads an existing category's limits and saves changes to it", async () => {
    api.put.mockResolvedValue({ data: {} });
    renderDialog({ ...HOT_STONE, name: HOT_STONE.category_name });

    expect(screen.getByLabelText(/^Name/)).toHaveValue("Hot Stone");
    expect(screen.getByLabelText(/^Adult/)).toHaveValue(1);
    expect(
      screen.getByRole("combobox", { name: "Past per-person limit" }),
    ).toHaveTextContent("Charge the item's price");

    setNumber("Per Order Limit", "2");
    fireEvent.click(screen.getByRole("button", { name: "Save Changes" }));

    await waitFor(() =>
      expect(api.put).toHaveBeenCalledWith(
        "/Category/update_category/5",
        expect.objectContaining({
          per_order_limit: 2,
          over_limit_behavior: "Charge",
          toddler_limit: 0,
        }),
      ),
    );
  });

  it.each([
    [{ adult: "1", child: "2" }, "Child limit cannot exceed Adult limit."],
    // 0 means unlimited, so the other limits are set too: an unlimited child would exceed adult first
    [
      { adult: "1", child: "1", senior: "2" },
      "Senior limit cannot exceed Adult limit.",
    ],
    [
      { adult: "2", child: "1", senior: "2", toddler: "2" },
      "Toddler limit cannot exceed Child limit.",
    ],
  ])("won't save limits that break the rules (%o)", async (limits, message) => {
    renderDialog();
    fireEvent.change(screen.getByLabelText(/^Name/), {
      target: { value: "Rolls" },
    });
    if (limits.adult) setNumber(/^Adult/, limits.adult);
    if (limits.child) setNumber(/^Child/, limits.child);
    if (limits.senior) setNumber(/^Senior/, limits.senior);
    if (limits.toddler) setNumber(/^Toddler/, limits.toddler);

    fireEvent.click(screen.getByRole("button", { name: "Create Category" }));

    expect(await screen.findByText(message)).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
  });

  it("shows the server's validation messages as text, not JSON", async () => {
    api.post.mockRejectedValue({
      response: {
        data: {
          title: "One or more validation errors occurred.",
          errors: { Adult_Limit: ["Adult limit is required (0 = unlimited)"] },
        },
      },
    });
    renderDialog();
    fireEvent.change(screen.getByLabelText(/^Name/), {
      target: { value: "Rolls" },
    });

    fireEvent.click(screen.getByRole("button", { name: "Create Category" }));

    expect(
      await screen.findByText("Adult limit is required (0 = unlimited)"),
    ).toBeInTheDocument();
  });

  const confirmDelete = () => {
    fireEvent.click(screen.getByRole("button", { name: "Delete" }));
    const confirm = screen
      .getByText(/Are you sure you want to delete "Hot Stone"/)
      .closest('[role="dialog"]');
    fireEvent.click(within(confirm).getByRole("button", { name: "Delete" }));
  };

  it("deletes an empty category (the item lookup returns 404)", async () => {
    api.get.mockRejectedValue({ response: { status: 404 } });
    api.delete.mockResolvedValue({ data: "Category deleted successfully." });
    const { onSuccess } = renderDialog({
      ...HOT_STONE,
      name: HOT_STONE.category_name,
    });

    confirmDelete();

    await waitFor(() =>
      expect(api.delete).toHaveBeenCalledWith("/Category/delete_category/5"),
    );
    expect(api.get).toHaveBeenCalledWith("/Category/get_menu_by_category/5", {
      params: { limit: 1 },
    });
    expect(onSuccess).toHaveBeenCalled();
  });

  it("won't delete a category that still has menu items", async () => {
    api.get.mockResolvedValue({ data: [{ item_id: 66 }] });
    renderDialog({ ...HOT_STONE, name: HOT_STONE.category_name });

    confirmDelete();

    expect(
      await screen.findByText(
        /Cannot delete category - it contains menu items/,
      ),
    ).toBeInTheDocument();
    expect(api.delete).not.toHaveBeenCalled();
  });
});
