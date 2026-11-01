import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import NewSessionDialog from "../NewSessionDialog";
import api from "@/config/api";

const mockCreateSession = jest.fn();

jest.mock("../../../context/SessionContext", () => ({
  useSession: () => ({ createSession: mockCreateSession }),
}));

jest.mock("@/config/api", () => {
  const mockApi = { get: jest.fn() };
  return { __esModule: true, default: mockApi, api: mockApi };
});

// localStorage hands back strings
let mockBranchLocation = "1";
jest.mock("@/utils/storage", () => ({
  __esModule: true,
  default: { get: (key) => (key === "branch-location" ? mockBranchLocation : null) },
}));

const responses = {
  "/Location": [
    { location_Id: 1, name: "Downtown" },
    { location_Id: 2, name: "Uptown" },
  ],
  "/Menu": [{ menu_id: 10, name: "Dinner" }],
  "/TableEntity/empty": [{ table_Id: 5, table_number: 3, seat_count: 4, tableGroup_Id: null }],
  "/TableGroup/available": [],
};

const choose = async (label, option) => {
  fireEvent.mouseDown(screen.getByRole("combobox", { name: new RegExp(label) }));
  fireEvent.click(await screen.findByRole("option", { name: option }));
};

const fillIn = async () => {
  await choose("Menu", "Dinner");
  await choose("^Table", /Table 3/);
};

beforeEach(() => {
  jest.clearAllMocks();
  mockBranchLocation = "1";
  api.get.mockImplementation(async (url) => ({ data: responses[url] }));
});

describe("NewSessionDialog", () => {
  it("only offers the working location", async () => {
    render(<NewSessionDialog open onClose={jest.fn()} />);

    const location = screen.getByRole("combobox", { name: /Location/ });
    await waitFor(() => expect(location).toHaveTextContent("Downtown"));
    expect(location).toHaveAttribute("aria-disabled", "true");
    expect(screen.queryByText("Uptown")).not.toBeInTheDocument();
  });

  it("offers every location when there is no working location", async () => {
    mockBranchLocation = null;
    render(<NewSessionDialog open onClose={jest.fn()} />);

    await choose("Location", "Uptown");
    expect(screen.getByRole("combobox", { name: /Location/ })).toHaveTextContent("Uptown");
  });

  it("can't be created until a menu and table are chosen", async () => {
    render(<NewSessionDialog open onClose={jest.fn()} />);

    const create = screen.getByRole("button", { name: "Create Session" });
    expect(create).toBeDisabled();

    await fillIn();
    expect(create).toBeEnabled();
  });

  it("closes once the session is created", async () => {
    mockCreateSession.mockResolvedValue(true);
    const onClose = jest.fn();
    render(<NewSessionDialog open onClose={onClose} />);
    await fillIn();

    fireEvent.click(screen.getByRole("button", { name: "Create Session" }));

    await waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(mockCreateSession).toHaveBeenCalledWith(10, 1, 5, "", "table");
  });

  it("stays open to try again when creating fails", async () => {
    mockCreateSession.mockResolvedValue(false);
    const onClose = jest.fn();
    render(<NewSessionDialog open onClose={onClose} />);
    await fillIn();

    fireEvent.click(screen.getByRole("button", { name: "Create Session" }));

    await waitFor(() => expect(screen.getByRole("button", { name: "Create Session" })).toBeEnabled());
    expect(mockCreateSession).toHaveBeenCalled();
    expect(onClose).not.toHaveBeenCalled();
  });
});
