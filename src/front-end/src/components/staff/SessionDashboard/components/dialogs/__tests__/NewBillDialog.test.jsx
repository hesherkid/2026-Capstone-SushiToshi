import { render, screen } from "@testing-library/react";
import NewBillDialog from "../NewBillDialog";

let mockSessions = [];

jest.mock("../../../context/SessionContext", () => ({
  useSession: () => ({ createBill: jest.fn(), sessions: mockSessions }),
}));

const renderDialog = (sessionId = 8) =>
  render(<NewBillDialog open sessionId={sessionId} onClose={jest.fn()} />);

describe("New bill dialog title", () => {
  beforeEach(() => {
    localStorage.clear();
    mockSessions = [];
  });

  it("names the session's table for staff", () => {
    mockSessions = [{ session_Id: 8, table_Numbers: [3] }];
    renderDialog();

    expect(screen.getByText("Create New Bill for Table 3")).toBeInTheDocument();
  });

  it("names every table in a table group", () => {
    mockSessions = [{ session_Id: 8, table_Numbers: [1, 2] }];
    renderDialog();

    expect(screen.getByText("Create New Bill for Tables 1, 2")).toBeInTheDocument();
  });

  it("uses the guest's QR table when the session list isn't loaded", () => {
    localStorage.setItem("tableNumber", "5");
    renderDialog();

    expect(screen.getByText("Create New Bill for Table 5")).toBeInTheDocument();
  });

  it("falls back to a plain title when the table isn't known", () => {
    renderDialog();

    expect(screen.getByText("Create New Bill")).toBeInTheDocument();
  });
});
