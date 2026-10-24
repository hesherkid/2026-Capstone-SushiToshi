import {
  render,
  screen,
  fireEvent,
  waitFor,
  within,
} from "@testing-library/react";
import MenuAssignmentDialog from "../MenuAssignmentDialog";
import api from "@/config/api";

jest.mock("@/config/api", () => {
  const mockApi = jest.fn();
  mockApi.get = jest.fn();
  mockApi.delete = jest.fn();
  return { __esModule: true, default: mockApi, api: mockApi };
});

const MENUS = [
  { menu_id: 1, name: "All Day Menu" },
  { menu_id: 2, name: "Lunch Special" },
];

const HOT_STONE_ROW = {
  assignment_Id: 3,
  menu_Id: 1,
  item_Id: 66,
  location_Id: null,
  menu: MENUS[0],
  price: 5,
  is_Add_On: false,
  status: "Available",
  adult_Limit: 1,
  child_limit: 1,
  senior_limit: 1,
  tot_Limit: 0,
  per_Order_Limit: 0,
  over_Limit_Behavior: "Charge",
};

const mockApi = (assignments = [HOT_STONE_ROW]) => {
  api.get.mockImplementation((url) => {
    if (url === "/Location") return Promise.resolve({ data: [] });
    if (url === "/menuItem/66")
      return Promise.resolve({
        data: { item_id: 66, menuAssignments: assignments },
      });
    return Promise.resolve({ data: [] });
  });
  api.mockResolvedValue({ data: {} });
};

const renderDialog = () =>
  render(
    <MenuAssignmentDialog
      open
      onClose={jest.fn()}
      selectedItem={{ item_id: 66, name: "Hot Stone Steak" }}
      menus={MENUS}
      onSuccess={jest.fn()}
    />,
  );

const priceInput = () => screen.getByLabelText(/^Price/);

const editRow = async (menuName) => {
  const row = (await screen.findByText(menuName)).closest("tr");
  fireEvent.click(within(row).getAllByRole("button")[0]);
};

const submit = (buttonName) =>
  fireEvent.submit(
    screen.getByRole("button", { name: buttonName }).closest("form"),
  );

const choose = (label, option) => {
  fireEvent.mouseDown(screen.getByRole("combobox", { name: label }));
  fireEvent.click(screen.getByRole("option", { name: option }));
};

describe("Menu assignment dialog", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockApi();
  });

  it("summarises each assignment's limits, including the charge past the limit", async () => {
    renderDialog();

    const row = (await screen.findByText("All Day Menu")).closest("tr");
    expect(within(row).getByText("$5.00")).toBeInTheDocument();
    expect(within(row).getByText("1/adult, then $5.00")).toBeInTheDocument();
  });

  it("loads one price for an included item, and keeps it when Add-on is switched", async () => {
    renderDialog();
    await editRow("All Day Menu");

    await waitFor(() => expect(priceInput()).toHaveValue(5));
    expect(
      screen.getByText(
        "Included: only charged past a per-person limit set to Charge (0 = never)",
      ),
    ).toBeInTheDocument();
    // There's no separate full price any more
    expect(screen.queryByLabelText(/Full Price/i)).not.toBeInTheDocument();

    const addOn = screen.getByLabelText("Is Add-on Item");
    fireEvent.click(addOn);
    expect(screen.getByText("Charged every time")).toBeInTheDocument();
    fireEvent.click(addOn);

    expect(priceInput()).toHaveValue(5);
  });

  it("saves the single price with the limits, and no full price", async () => {
    renderDialog();
    await editRow("All Day Menu");
    await waitFor(() => expect(priceInput()).toHaveValue(5));

    fireEvent.change(priceInput(), { target: { value: "6" } });
    submit("Update Assignment");

    await waitFor(() => expect(api).toHaveBeenCalled());
    const request = api.mock.calls[0][0];
    expect(request).toMatchObject({
      method: "PUT",
      url: "/menuassignment/1/66",
      data: expect.objectContaining({
        price: 6,
        adult_limit: 1,
        over_limit_behavior: "Charge",
        is_add_on: false,
      }),
    });
    expect(request.data).not.toHaveProperty("full_price");
  });

  it("won't save an item that charges past its limit without a price", async () => {
    mockApi([]);
    renderDialog();
    await screen.findByText("No current assignments");

    choose("Past per-person limit", "Charge the item's price");
    fireEvent.change(priceInput(), { target: { value: "0" } });
    submit("Add Assignment");

    expect(
      await screen.findByText(
        "Items that charge over the limit need a price greater than 0.",
      ),
    ).toBeInTheDocument();
    expect(api).not.toHaveBeenCalled();
  });

  it("won't save limits that break the rules", async () => {
    mockApi([]);
    renderDialog();
    await screen.findByText("No current assignments");

    fireEvent.change(screen.getByLabelText("Adult"), {
      target: { value: "1" },
    });
    fireEvent.change(screen.getByLabelText("Child"), {
      target: { value: "2" },
    });
    submit("Add Assignment");

    expect(
      await screen.findByText("Child limit cannot exceed Adult limit."),
    ).toBeInTheDocument();
    expect(api).not.toHaveBeenCalled();
  });

  it("shows the server's reason when a save is rejected", async () => {
    api.mockRejectedValue({
      response: {
        data: "Items that charge over the limit need a price greater than 0",
      },
    });
    renderDialog();
    await editRow("All Day Menu");
    await waitFor(() => expect(priceInput()).toHaveValue(5));

    submit("Update Assignment");

    expect(
      await screen.findByText(
        "Items that charge over the limit need a price greater than 0",
      ),
    ).toBeInTheDocument();
  });
});
