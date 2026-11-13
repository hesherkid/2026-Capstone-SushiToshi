import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import MoveGuestDialog from "../MoveGuestDialog";

const mockMoveGuest = jest.fn();
let mockSessions = [];

jest.mock("../../../context/SessionContext", () => ({
  useSession: () => ({
    sessions: mockSessions,
    moveGuest: mockMoveGuest,
    isSaving: false,
  }),
}));

// Plain form, tests only cover the move dialogs fields
jest.mock("@/components/common/FormDialog", () => {
  const { createElement } = require("react");
  return {
    __esModule: true,
    default: ({
      open,
      title,
      onSubmit,
      submitLabel,
      submitDisabled,
      children,
    }) =>
      open
        ? createElement(
            "form",
            {
              onSubmit: (e) => {
                e.preventDefault();
                if (!submitDisabled) onSubmit();
              },
            },
            createElement("h2", null, title),
            children,
            createElement(
              "button",
              { type: "submit", disabled: submitDisabled },
              submitLabel,
            ),
          )
        : null,
  };
});

const SESSION = 6;

const bill = (override) => ({
  status: "Open",
  adult_count: 0,
  senior_count: 0,
  child_count: 0,
  tot_count: 0,
  requested_at: null,
  ...override,
});

const birthdayDinner = bill({
  bill_id: 70,
  bill_name: "Birthday dinner",
  adult_count: 2,
  total_guests: 2,
});
const secondBill = bill({
  bill_id: 71,
  bill_name: "Table 19 - Bill 2",
  adult_count: 1,
  total_guests: 1,
});

const guest = {
  participant_id: 45,
  user_name: "Guest DYAZ",
  bill_id: 70,
  is_guest: true,
};
const ana = {
  participant_id: 46,
  user_name: "Ana",
  bill_id: 70,
  is_guest: false,
};

const renderDialog = (person, bills = [birthdayDinner, secondBill]) => {
  mockSessions = [{ session_id: SESSION, bills }];
  const onClose = jest.fn();
  render(
    <MoveGuestDialog
      open
      sessionId={SESSION}
      person={person}
      onClose={onClose}
    />,
  );
  return { onClose };
};

// Opens "Move to" select, its options are in the document
const openMoveTo = () =>
  fireEvent.mouseDown(screen.getByRole("combobox", { name: /move to/i }));

const pick = (name) => {
  openMoveTo();
  fireEvent.click(screen.getByRole("option", { name }));
};

describe("Move Guest dialog: new bill", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockMoveGuest.mockResolvedValue(true);
  });

  // Guest DYAZ means nothing on a bill, it is named after the bill it came from
  it("names a guest accounts new bill after their old bill", () => {
    renderDialog(guest);
    openMoveTo();

    expect(
      screen.getByRole("option", { name: "+ New bill: Birthday dinner 1" }),
    ).toBeInTheDocument();
  });

  it("skips a bill name that's already used at the table", () => {
    renderDialog(guest, [
      birthdayDinner,
      bill({
        bill_id: 72,
        bill_name: "Birthday dinner 1",
        adult_count: 1,
        total_guests: 1,
      }),
    ]);
    openMoveTo();

    expect(
      screen.getByRole("option", { name: "+ New bill: Birthday dinner 2" }),
    ).toBeInTheDocument();
  });

  it("names a registered customer's new bill after them", () => {
    renderDialog(ana);
    openMoveTo();

    expect(
      screen.getByRole("option", { name: "+ New bill: Ana" }),
    ).toBeInTheDocument();
  });

  // A bill needs minimum 1 guest, so a one-guest bill has nobody to split off
  it("doesn't offer a new bill when their bill has only one guest", () => {
    renderDialog({ ...ana, bill_id: 71 });
    openMoveTo();

    expect(
      screen.queryByRole("option", { name: /new bill/i }),
    ).not.toBeInTheDocument();
  });

  // A split is needed when everyone is on one bill
  it("offers a new bill even when theirs is the only open bill", () => {
    renderDialog(guest, [birthdayDinner, { ...secondBill, status: "Closed" }]);

    expect(screen.queryByText(/no other open bill/i)).not.toBeInTheDocument();
    openMoveTo();
    expect(
      screen.getByRole("option", { name: "+ New bill: Birthday dinner 1" }),
    ).toBeInTheDocument();
  });

  it("keeps the guest counts moving for a new bill, it can't start empty", () => {
    renderDialog(guest);
    pick("+ New bill: Birthday dinner 1");

    const counts = screen.getByRole("checkbox", {
      name: /count for the new bill/i,
    });
    expect(counts).toBeChecked();
    expect(counts).toBeDisabled();
    expect(screen.getByText(/starts with 1 guest/)).toBeInTheDocument();
  });

  it("asks for a new bill with no bill ID", async () => {
    const { onClose } = renderDialog(guest);
    pick("+ New bill: Birthday dinner 1");

    fireEvent.click(screen.getByRole("button", { name: "Move" }));

    await waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(mockMoveGuest).toHaveBeenCalledWith(
      SESSION,
      45,
      null,
      true,
      "adult",
      1,
      true,
    );
  });

  it("still moves to an existing bill the way it did before", async () => {
    const { onClose } = renderDialog(guest);
    pick("Table 19 - Bill 2");

    fireEvent.click(screen.getByRole("button", { name: "Move" }));

    await waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(mockMoveGuest).toHaveBeenCalledWith(
      SESSION,
      45,
      71,
      true,
      "adult",
      1,
      false,
    );
  });
});
