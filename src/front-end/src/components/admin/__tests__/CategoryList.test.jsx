import {
  render,
  screen,
  fireEvent,
  waitFor,
  within,
} from "@testing-library/react";
import CategoryList from "../CategoryList";
import api from "@/config/api";

jest.mock("@/config/api", () => {
  const mockApi = { get: jest.fn(), delete: jest.fn() };
  return { __esModule: true, default: mockApi, api: mockApi };
});

// The real grid is MUI DataGrid, which doesn't lay out in jsdom. This stand-in renders each
// row's cells the same way (renderCell when the column has one), so the columns can be checked
jest.mock("@/components/common/ManagementGrid", () => ({
  __esModule: true,
  default: ({ rows = [], columns = [], getRowId }) => (
    <table>
      <thead>
        <tr>
          {columns.map((col) => (
            <th key={col.field}>{col.headerName}</th>
          ))}
        </tr>
      </thead>
      <tbody>
        {rows.map((row) => (
          <tr key={getRowId(row)}>
            {columns.map((col) => (
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
  ),
}));

jest.mock("@/components/common/ManagementHeader", () => ({
  __esModule: true,
  default: ({ title, addLabel, onAdd }) => (
    <div>
      <h1>{title}</h1>
      <button onClick={onAdd}>{addLabel}</button>
    </div>
  ),
}));

const category = (overrides) => ({
  category_id: 1,
  category_name: "Rolls",
  description: "Inside-out rolls",
  adult_limit: 0,
  child_limit: 0,
  senior_limit: 0,
  toddler_limit: 0,
  per_order_limit: 0,
  over_limit_behavior: "Block",
  ...overrides,
});

const SASHIMI = category({
  category_id: 1,
  category_name: "Sashimi",
  per_order_limit: 30,
});
const HOT_STONE = category({
  category_id: 5,
  category_name: "Hot Stone",
  adult_limit: 1,
  child_limit: 1,
  senior_limit: 1,
  over_limit_behavior: "Charge",
});

const rowFor = async (name) => (await screen.findByText(name)).closest("tr");

describe("Category list", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    api.get.mockResolvedValue({ data: [SASHIMI, HOT_STONE] });
  });

  it("has a column for every limit and what happens past the per-person limit", async () => {
    render(<CategoryList />);
    await rowFor("Sashimi");

    ["Adult", "Child", "Senior", "Toddler", "Per Order", "Over Limit"].forEach(
      (header) =>
        expect(
          screen.getByRole("columnheader", { name: header }),
        ).toBeInTheDocument(),
    );
  });

  it("shows 0 as no limit, and a toddler limit of 0 as the child limit it inherits", async () => {
    render(<CategoryList />);

    const sashimi = within(await rowFor("Sashimi"));
    expect(sashimi.getByText("30")).toBeInTheDocument();
    expect(sashimi.getAllByText("—").length).toBeGreaterThan(0);

    const hotStone = within(await rowFor("Hot Stone"));
    expect(hotStone.getByText("1 (child)")).toBeInTheDocument();
    expect(hotStone.getByText("Charge")).toBeInTheDocument();
  });

  it("passes the row to the edit dialog with the name it expects", async () => {
    const onEditCategory = jest.fn();
    render(<CategoryList onEditCategory={onEditCategory} />);

    fireEvent.click(
      within(await rowFor("Hot Stone")).getByRole("button", { name: "Edit" }),
    );

    expect(onEditCategory).toHaveBeenCalledWith(
      expect.objectContaining({ category_id: 5, name: "Hot Stone" }),
    );
  });

  it("deletes a category after confirming, then reloads the list", async () => {
    api.delete.mockResolvedValue({ data: "Category deleted successfully." });
    render(<CategoryList />);

    fireEvent.click(
      within(await rowFor("Hot Stone")).getByRole("button", { name: "Delete" }),
    );
    const confirm = screen
      .getByText(/Are you sure you want to delete "Hot Stone"/)
      .closest('[role="dialog"]');
    fireEvent.click(within(confirm).getByRole("button", { name: "Delete" }));

    await waitFor(() =>
      expect(api.delete).toHaveBeenCalledWith("/Category/delete_category/5"),
    );
    await waitFor(() => expect(api.get).toHaveBeenCalledTimes(2));
  });
});
