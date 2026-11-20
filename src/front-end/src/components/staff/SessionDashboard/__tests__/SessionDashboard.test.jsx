import {
  render,
  screen,
  within,
  fireEvent,
  waitFor,
} from "@testing-library/react";
import SessionDashboard from "@/components/staff/SessionDashboard";
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

jest.mock("@/utils/storage", () => ({
  __esModule: true,
  default: { get: jest.fn(() => "1") },
}));

jest.mock("@/contexts/NotificationContext", () => {
  const notifications = { notifySuccess: jest.fn(), notifyError: jest.fn() };
  return { useNotification: () => notifications };
});

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

// The free ice cream flavours admins keep in the Dessert category
const FLAVOURS = [
  { item_id: 501, name: "Strawberry Ice Cream" },
  { item_id: 502, name: "Chocolate Ice Cream" },
  { item_id: 503, name: "Mango Ice Cream" },
  { item_id: 504, name: "Vanilla Ice Cream" },
];

const sessions = [
  {
    session_id: 7,
    table_numbers: [3],
    started_at: "2026-10-14T20:00:00Z",
    active_participants: 2,
    bills: [
      { bill_id: 70, bill_name: "Smith", status: "Closed", total_guests: 2 },
    ],
    bill_count: 1,
    is_closable: true,
  },
  {
    session_id: 8,
    table_numbers: [1, 2],
    started_at: "2026-10-14T20:10:00Z",
    active_participants: 1, // one phone for a party of 4
    bills: [
      {
        bill_id: 80,
        bill_name: "Lee",
        status: "Open",
        total_guests: 4,
        adult_count: 4,
        people_on_bill: 1,
        started_by_guest: true,
      },
    ],
    people: [
      { participant_id: 5, user_name: "Ana Lee", bill_id: 80 },
      { participant_id: 6, user_name: "Ben Park", bill_id: null },
    ],
    bill_count: 1,
    is_closable: false,
  },
];

const requests = [
  {
    request_id: 11,
    session_id: 7,
    table_number: 3,
    notes: "More soy sauce",
    created_at: "2026-10-14T20:30:00Z",
  },
  // Another location's session
  {
    request_id: 99,
    session_id: 42,
    table_number: 9,
    notes: "Other location",
    created_at: "2026-10-14T20:31:00Z",
  },
];

const rowFor = (text) => screen.getByText(text).closest('[role="row"]');

// Grid action labels include session and table for accessibility "View session #8, Tables 1, 2".
// so match the start; within(row) already picks the session
const rowAction = (sessionText, action) =>
  within(rowFor(sessionText)).getByRole("button", {
    name: new RegExp(`^${action}\\b`),
  });

const renderDashboard = async () => {
  render(<SessionDashboard />);
  await waitFor(() => rowFor("#7"));
};

describe("Session dashboard", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    api.get.mockImplementation((url) => {
      if (url.startsWith("/Dashboard/sessions"))
        return Promise.resolve({ status: 200, data: sessions });
      if (url === "/ServiceRequest/pending")
        return Promise.resolve({ status: 200, data: requests });
      if (url === "/Bill/ice-cream-flavours")
        return Promise.resolve({ status: 200, data: FLAVOURS });
      if (url === "/Bill/summary/80") {
        return Promise.resolve({
          status: 200,
          data: {
            bill_id: 80,
            bill_name: "Lee",
            status: "Open",
            addon_items: [],
            total_amount: 180.6,
          },
        });
      }
      return Promise.resolve({ status: 200, data: [] });
    });
    api.post.mockResolvedValue({ status: 200 });
    api.put.mockResolvedValue({ status: 200, data: {} });
  });

  it("lists sessions as rows, with tables that need help first", async () => {
    await renderDashboard();

    const rows = screen.getAllByRole("row").slice(1);
    expect(within(rows[0]).getByText("#7")).toBeInTheDocument();
    expect(within(rows[0]).getByText("1 request")).toBeInTheDocument();
    expect(rows[0]).toHaveClass("row-needs-help");

    const busy = rowFor("#8");
    expect(within(busy).getByText("Tables 1, 2")).toBeInTheDocument();
    expect(within(busy).getByText("1 of 1")).toBeInTheDocument();
    expect(within(busy).getByText("4")).toBeInTheDocument(); // guests on the bills, not phones

    // Requests from other locations are ignored; one fetch each, not one per session
    expect(
      screen.getByText("Pending Requests").closest(".MuiCard-root"),
    ).toHaveTextContent("1Pending Requests");
    expect(
      screen.getByText("Tables In Use").closest(".MuiCard-root"),
    ).toHaveTextContent("3Tables In Use");
    expect(api.get).toHaveBeenCalledWith("/Dashboard/sessions?location_id=1");
    expect(api.get).toHaveBeenCalledWith("/ServiceRequest/pending");
    expect(api.get.mock.calls.some(([url]) => url.includes("by-session"))).toBe(
      false,
    );
  });

  it("only offers to end sessions with no open bills, and asks first", async () => {
    await renderDashboard();

    expect(
      within(rowFor("#8")).queryByRole("button", { name: /^End session\b/ }),
    ).not.toBeInTheDocument();

    fireEvent.click(rowAction("#7", "End session"));
    const dialog = await screen.findByRole("dialog");
    expect(
      within(dialog).getByText(/End Session #7 for table 3\?/),
    ).toBeInTheDocument();

    fireEvent.click(
      within(dialog).getByRole("button", { name: "End Session" }),
    );
    await waitFor(() =>
      expect(api.put).toHaveBeenCalledWith("/DiningSession/7/close"),
    );
  });

  it("shows a session's requests and lets staff mark them done", async () => {
    await renderDashboard();

    fireEvent.click(rowAction("#7", "View session"));
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText("More soy sauce")).toBeInTheDocument();
    expect(within(dialog).getByText("Smith")).toBeInTheDocument();

    fireEvent.click(
      within(dialog).getByRole("button", { name: "Mark request done" }),
    );
    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith("/ServiceRequest/11/complete"),
    );
  });

  it("flags bills a guest started on their phone so staff check the guest count", async () => {
    await renderDashboard();

    fireEvent.click(rowAction("#8", "View session"));
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText("Started by guest")).toBeInTheDocument();
  });

  it("shows which tables have a guest count to check, and opens the session from there", async () => {
    await renderDashboard();

    // Session 7's only bill is closed, so there's nothing to check
    expect(
      within(rowFor("#7")).queryByText("Check guests"),
    ).not.toBeInTheDocument();

    fireEvent.click(rowAction("#8", "Check guests"));
    const dialog = await screen.findByRole("dialog", {
      name: /Dining Session #8/,
    });
    expect(within(dialog).getByText("Started by guest")).toBeInTheDocument();
  });

  it("warns staff when more people are on a bill than its guest count, or a guest changed it", async () => {
    // A guest said 1, then two more people joined from their phones
    const PARK = {
      bill_id: 81,
      bill_name: "Park",
      status: "Open",
      total_guests: 1,
      people_on_bill: 3,
      counts_changed_by_guest: true,
    };
    const withPark = sessions.map((s) =>
      s.session_id === 8 ? { ...s, bills: [...s.bills, PARK] } : s,
    );
    const defaultGet = api.get.getMockImplementation();
    api.get.mockImplementation((url) =>
      url.startsWith("/Dashboard/sessions")
        ? Promise.resolve({ status: 200, data: withPark })
        : defaultGet(url),
    );
    await renderDashboard();

    fireEvent.click(rowAction("#8", "View session"));
    const dialog = await screen.findByRole("dialog");
    expect(
      within(dialog).getByText("3 people on 1-guest bill"),
    ).toBeInTheDocument();
    expect(
      within(dialog).getByText("Count changed by guest"),
    ).toBeInTheDocument();
    // Lee's 4 guests share one phone, which is fine
    expect(
      within(dialog).queryByText(/people on 4-guest bill/),
    ).not.toBeInTheDocument();
  });

  it("lets staff lock a bill's guest count so guests can't change it, and unlock it", async () => {
    await renderDashboard();

    fireEvent.click(rowAction("#8", "View session"));
    const sessionDialog = await screen.findByRole("dialog");
    fireEvent.click(
      within(sessionDialog).getByRole("button", { name: "Lock guests on Lee" }),
    );

    await waitFor(() =>
      expect(api.put).toHaveBeenCalledWith(
        "/Bill/lock-guest-counts/80",
        { locked: true },
        { params: { session_id: 8 } },
      ),
    );
  });

  it("shows a locked bill, with a way to unlock it", async () => {
    const locked = sessions.map((s) =>
      s.session_id === 8
        ? {
            ...s,
            bills: [
              { ...s.bills[0], started_by_guest: false, counts_locked: true },
            ],
          }
        : s,
    );
    const defaultGet = api.get.getMockImplementation();
    api.get.mockImplementation((url) =>
      url.startsWith("/Dashboard/sessions")
        ? Promise.resolve({ status: 200, data: locked })
        : defaultGet(url),
    );
    await renderDashboard();

    fireEvent.click(rowAction("#8", "View session"));
    const sessionDialog = await screen.findByRole("dialog");
    expect(
      within(sessionDialog).getByText("Guests locked"),
    ).toBeInTheDocument();
    // Staff can still fix a locked count
    expect(
      within(sessionDialog).getByRole("button", { name: "Edit guests on Lee" }),
    ).toBeEnabled();
    fireEvent.click(
      within(sessionDialog).getByRole("button", {
        name: "Unlock guests on Lee",
      }),
    );

    await waitFor(() =>
      expect(api.put).toHaveBeenCalledWith(
        "/Bill/lock-guest-counts/80",
        { locked: false },
        { params: { session_id: 8 } },
      ),
    );
  });

  // Guests say yes on their bill; staff send it for the bills that are ready, even after closing
  const LEE = sessions[1].bills[0];
  const PARK = { bill_id: 81, bill_name: "Park", status: "Open", total_guests: 2, adult_count: 2 };

  const withBills = (bills) => {
    const changed = sessions.map((s) => (s.session_id === 8 ? { ...s, bills } : s));
    const defaultGet = api.get.getMockImplementation();
    api.get.mockImplementation((url) =>
      url.startsWith("/Dashboard/sessions")
        ? Promise.resolve({ status: 200, data: changed })
        : defaultGet(url),
    );
  };

  const openSession = async () => {
    fireEvent.click(rowAction("#8", "View session"));
    return screen.findByRole("dialog");
  };

  const openIceCream = async (saidYes) => {
    const sessionDialog = await openSession();
    expect(within(sessionDialog).getByText("Table wants ice cream")).toBeInTheDocument();
    expect(within(sessionDialog).getByText(`${saidYes} said yes. Ask the table which flavours.`)).toBeInTheDocument();
    fireEvent.click(
      within(sessionDialog).getByRole("button", { name: "Free ice cream for the table" }),
    );
    const dialog = await screen.findByRole("dialog", { name: /Free ice cream for Tables 1, 2/ });
    await within(dialog).findByRole("button", { name: "More Mango" }); // flavours loaded
    return dialog;
  };

  it("lets staff pick flavours and send ice cream for the bill that said yes, even after closing it", async () => {
    withBills([{ ...LEE, status: "Closed", wants_ice_cream: true }, PARK]);
    await renderDashboard();

    const dialog = await openIceCream("Lee");
    // Lee said yes, so it starts ticked; Park can be added
    expect(within(dialog).getByRole("checkbox", { name: /Lee/ })).toBeChecked();
    expect(within(dialog).getByRole("checkbox", { name: /Park/ })).not.toBeChecked();
    const send = within(dialog).getByRole("button", { name: /to kitchen/ });
    expect(send).toBeDisabled(); // nothing chosen yet
    fireEvent.click(within(dialog).getByRole("button", { name: "More Mango" }));
    fireEvent.click(within(dialog).getByRole("button", { name: "More Mango" }));
    fireEvent.click(within(dialog).getByRole("button", { name: "More Vanilla" }));
    expect(send).toHaveTextContent("Send 3 to kitchen");
    fireEvent.click(send);

    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith("/Bill/send-ice-cream/8", {
        bill_ids: [80],
        flavours: [{ item_id: 503, quantity: 2 }, { item_id: 504, quantity: 1 }],
      }),
    );
  });

  it("caps at one each for the ticked bills, and lets staff add another party", async () => {
    withBills([{ ...LEE, total_guests: 1, wants_ice_cream: true }, PARK]);
    await renderDashboard();

    const dialog = await openIceCream("Lee");
    fireEvent.click(within(dialog).getByRole("button", { name: "More Strawberry" }));
    expect(within(dialog).getByRole("button", { name: "More Mango" })).toBeDisabled(); // Lee is 1 guest

    // Park's 2 guests want some too
    fireEvent.click(within(dialog).getByRole("checkbox", { name: /Park/ }));
    fireEvent.click(within(dialog).getByRole("button", { name: "More Mango" }));
    fireEvent.click(within(dialog).getByRole("button", { name: "More Mango" }));
    expect(within(dialog).getByRole("button", { name: "More Vanilla" })).toBeDisabled();
    fireEvent.click(within(dialog).getByRole("button", { name: /to kitchen/ }));

    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith("/Bill/send-ice-cream/8", {
        bill_ids: [80, 81],
        flavours: [{ item_id: 501, quantity: 1 }, { item_id: 503, quantity: 2 }],
      }),
    );
  });

  it("won't send for a bill with no guests until the count is fixed", async () => {
    withBills([{ ...LEE, wants_ice_cream: true }, { ...PARK, total_guests: 0 }]);
    await renderDashboard();

    const dialog = await openIceCream("Lee");
    fireEvent.click(within(dialog).getByRole("button", { name: "More Mango" }));
    fireEvent.click(within(dialog).getByRole("checkbox", { name: /Park/ }));

    expect(within(dialog).getByText(/Park has no guests yet/)).toBeInTheDocument();
    expect(within(dialog).getByRole("button", { name: /to kitchen/ })).toBeDisabled();
    expect(within(dialog).getByRole("button", { name: "More Vanilla" })).toBeDisabled();
  });

  it("offers the flavours admins keep in Dessert, and says so when there are none", async () => {
    const defaultGet = api.get.getMockImplementation();
    api.get.mockImplementation((url) =>
      url === "/Bill/ice-cream-flavours"
        ? Promise.resolve({ status: 200, data: [{ item_id: 600, name: "Pistachio Ice Cream" }] })
        : defaultGet(url),
    );
    withBills([{ ...LEE, wants_ice_cream: true }]);
    await renderDashboard();

    const sessionDialog = await openSession();
    fireEvent.click(within(sessionDialog).getByRole("button", { name: "Free ice cream for the table" }));
    const dialog = await screen.findByRole("dialog", { name: /Free ice cream for Tables 1, 2/ });
    fireEvent.click(await within(dialog).findByRole("button", { name: "More Pistachio" }));
    fireEvent.click(within(dialog).getByRole("button", { name: /to kitchen/ }));
    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith("/Bill/send-ice-cream/8", {
        bill_ids: [80],
        flavours: [{ item_id: 600, quantity: 1 }],
      }),
    );
  });

  it("explains when no flavours are available", async () => {
    const defaultGet = api.get.getMockImplementation();
    api.get.mockImplementation((url) =>
      url === "/Bill/ice-cream-flavours" ? Promise.resolve({ status: 200, data: [] }) : defaultGet(url),
    );
    withBills([{ ...LEE, wants_ice_cream: true }]);
    await renderDashboard();

    const sessionDialog = await openSession();
    fireEvent.click(within(sessionDialog).getByRole("button", { name: "Free ice cream for the table" }));
    const dialog = await screen.findByRole("dialog", { name: /Free ice cream for Tables 1, 2/ });
    expect(await within(dialog).findByText(/No ice cream flavours are available/)).toBeInTheDocument();
    expect(within(dialog).getByRole("button", { name: /to kitchen/ })).toBeDisabled();
  });

  it("gives a bill that says yes later its own turn", async () => {
    withBills([
      { ...LEE, wants_ice_cream: true, ice_cream_sent_at: "2026-10-18T04:30:00Z" },
      { ...PARK, wants_ice_cream: true },
    ]);
    await renderDashboard();

    const dialog = await openIceCream("Park");
    // Lee already had theirs, so only Park is offered
    expect(within(dialog).queryByRole("checkbox", { name: /Lee/ })).not.toBeInTheDocument();
    expect(within(dialog).getByRole("checkbox", { name: /Park/ })).toBeChecked();
  });

  it("shows which bills had their ice cream, and only offers the rest", async () => {
    withBills([{ ...LEE, wants_ice_cream: true, ice_cream_sent_at: "2026-10-18T04:30:00Z" }, PARK]);
    await renderDashboard();

    const sessionDialog = await openSession();
    expect(within(sessionDialog).getByText(/Ice cream sent/)).toBeInTheDocument();
    // Nobody waiting, so no strip; Park can still have some if they ask the server
    expect(within(sessionDialog).queryByText("Table wants ice cream")).not.toBeInTheDocument();
    fireEvent.click(
      within(sessionDialog).getByRole("button", { name: "Free ice cream for the table" }),
    );
    const dialog = await screen.findByRole("dialog", { name: /Free ice cream for Tables 1, 2/ });
    expect(within(dialog).queryByRole("checkbox", { name: /Lee/ })).not.toBeInTheDocument();
    expect(within(dialog).getByRole("checkbox", { name: /Park/ })).toBeInTheDocument();
  });

  // The table asks a server in person, or has no phones: staff can still send it
  it("lets staff send ice cream when nobody asked in the app, with no bill ticked to start", async () => {
    withBills([LEE, PARK]);
    await renderDashboard();

    const sessionDialog = await openSession();
    expect(within(sessionDialog).queryByText("Table wants ice cream")).not.toBeInTheDocument();
    fireEvent.click(
      within(sessionDialog).getByRole("button", { name: "Free ice cream for the table" }),
    );

    const dialog = await screen.findByRole("dialog", { name: /Free ice cream for Tables 1, 2/ });
    expect(within(dialog).getByRole("checkbox", { name: /Lee/ })).not.toBeChecked();
    expect(within(dialog).getByRole("checkbox", { name: /Park/ })).not.toBeChecked();
    expect(await within(dialog).findByRole("button", { name: "More Mango" })).toBeDisabled(); // tick someone first

    fireEvent.click(within(dialog).getByRole("checkbox", { name: /Park/ }));
    fireEvent.click(within(dialog).getByRole("button", { name: "More Mango" }));
    fireEvent.click(within(dialog).getByRole("button", { name: /to kitchen/ }));

    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith("/Bill/send-ice-cream/8", {
        bill_ids: [81],
        flavours: [{ item_id: 503, quantity: 1 }],
      }),
    );
  });

  it("shows one ice cream button: the strip's, when a bill said yes", async () => {
    withBills([{ ...LEE, wants_ice_cream: true }, PARK]);
    await renderDashboard();

    const sessionDialog = await openSession();
    expect(
      within(sessionDialog).getAllByRole("button", { name: "Free ice cream for the table" }),
    ).toHaveLength(1);
  });

  it("offers no ice cream once every bill has had it", async () => {
    withBills([{ ...LEE, ice_cream_sent_at: "2026-10-18T04:30:00Z" }]);
    await renderDashboard();

    const sessionDialog = await openSession();
    expect(
      within(sessionDialog).queryByRole("button", { name: /free ice cream/i }),
    ).not.toBeInTheDocument();
  });

  it("lets staff fix a bill's guest counts", async () => {
    await renderDashboard();

    fireEvent.click(rowAction("#8", "View session"));
    const sessionDialog = await screen.findByRole("dialog");
    fireEvent.click(
      within(sessionDialog).getByRole("button", { name: "Edit guests on Lee" }),
    );

    const dialog = await screen.findByRole("dialog", {
      name: /Edit Guests on Lee/,
    });
    expect(
      within(dialog).getByRole("spinbutton", { name: "Adults" }),
    ).toHaveValue(4);
    fireEvent.change(
      within(dialog).getByRole("spinbutton", { name: "Adults" }),
      { target: { value: "3" } },
    );
    fireEvent.change(
      within(dialog).getByRole("spinbutton", { name: "Children" }),
      { target: { value: "2" } },
    );
    expect(within(dialog).getByText("Total Guests: 5")).toBeInTheDocument();
    fireEvent.click(
      within(dialog).getByRole("button", { name: "Save Guests" }),
    );

    await waitFor(() =>
      expect(api.put).toHaveBeenCalledWith(
        "/Bill/guest-counts/80",
        { adult_count: 3, senior_count: 0, child_count: 2, tot_count: 0 },
        { params: { session_id: 8 } },
      ),
    );
  });

  it("fills in a bill's current guests when staff edit them", async () => {
    const PARK = {
      bill_id: 81,
      bill_name: "Park",
      status: "Open",
      total_guests: 5,
      adult_count: 2,
      senior_count: 1,
      child_count: 2,
      tot_count: 0,
    };
    // Without the breakdown, the guests show as adults so the total still matches
    const KIM = {
      bill_id: 82,
      bill_name: "Kim",
      status: "Open",
      total_guests: 3,
    };
    const withBills = sessions.map((s) =>
      s.session_id === 8 ? { ...s, bills: [...s.bills, PARK, KIM] } : s,
    );
    const defaultGet = api.get.getMockImplementation();
    api.get.mockImplementation((url) =>
      url.startsWith("/Dashboard/sessions")
        ? Promise.resolve({ status: 200, data: withBills })
        : defaultGet(url),
    );
    await renderDashboard();

    fireEvent.click(rowAction("#8", "View session"));
    const sessionDialog = await screen.findByRole("dialog");
    fireEvent.click(
      within(sessionDialog).getByRole("button", {
        name: "Edit guests on Park",
      }),
    );
    let dialog = await screen.findByRole("dialog", {
      name: /Edit Guests on Park/,
    });
    expect(
      within(dialog).getByRole("spinbutton", { name: "Adults" }),
    ).toHaveValue(2);
    expect(
      within(dialog).getByRole("spinbutton", { name: "Seniors" }),
    ).toHaveValue(1);
    expect(
      within(dialog).getByRole("spinbutton", { name: "Children" }),
    ).toHaveValue(2);
    expect(within(dialog).getByText("Total Guests: 5")).toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole("button", { name: "Cancel" }));
    // Back to the session dialog
    const backToSession = await screen.findByRole("dialog", {
      name: /Dining Session #8/,
    });
    fireEvent.click(
      within(backToSession).getByRole("button", { name: "Edit guests on Kim" }),
    );
    dialog = await screen.findByRole("dialog", { name: /Edit Guests on Kim/ });
    expect(
      within(dialog).getByRole("spinbutton", { name: "Adults" }),
    ).toHaveValue(3);
    expect(within(dialog).getByText("Total Guests: 3")).toBeInTheDocument();
  });

  it("lists who's on which bill, and lets staff move someone with their orders", async () => {
    const PARK = {
      bill_id: 81,
      bill_name: "Park",
      status: "Open",
      total_guests: 1,
      people_on_bill: 0,
    };
    const withPark = sessions.map((s) =>
      s.session_id === 8 ? { ...s, bills: [...s.bills, PARK] } : s,
    );
    const defaultGet = api.get.getMockImplementation();
    api.get.mockImplementation((url) =>
      url.startsWith("/Dashboard/sessions")
        ? Promise.resolve({ status: 200, data: withPark })
        : defaultGet(url),
    );
    await renderDashboard();

    fireEvent.click(rowAction("#8", "View session"));
    const sessionDialog = await screen.findByRole("dialog");
    expect(
      within(sessionDialog).getByText("People at the table (2)"),
    ).toBeInTheDocument();
    expect(
      within(sessionDialog).getByText("Not on a bill yet"),
    ).toBeInTheDocument();
    fireEvent.click(
      within(sessionDialog).getByRole("button", { name: "Move Ana Lee" }),
    );

    const dialog = await screen.findByRole("dialog", { name: /Move Ana Lee/ });
    expect(within(dialog).getByText("Ana Lee is on Lee.")).toBeInTheDocument();
    expect(
      within(dialog).getByRole("checkbox", {
        name: /Take Ana Lee's orders with them/,
      }),
    ).toBeChecked();
    fireEvent.mouseDown(
      within(dialog).getByRole("combobox", { name: /Move to/ }),
    );
    fireEvent.click(await screen.findByRole("option", { name: "Park" }));
    // By default Ana also moves from Lee's guest count to Park's
    expect(
      within(dialog).getByRole("checkbox", {
        name: "Change both bills' guest counts",
      }),
    ).toBeChecked();
    expect(
      within(dialog).getByRole("combobox", { name: /Kind of guest/ }),
    ).toHaveTextContent("Adult");
    expect(
      within(dialog).getByText(
        "Lee goes from 4 guests to 3 guests, and Park from 1 guest to 2 guests.",
      ),
    ).toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole("button", { name: "Move" }));

    await waitFor(() =>
      expect(api.put).toHaveBeenCalledWith(
        "/SessionParticipant/8/participants/5/bill",
        {
          bill_id: 81,
          move_orders: true,
          guest_type: "adult",
          guest_count: 1,
        },
      ),
    );
  });

  it("moves several guests' counts when one phone stands for a party, and warns about bills that asked for the bill", async () => {
    // Park has asked for the bill
    const PARK = {
      bill_id: 81,
      bill_name: "Park",
      status: "Open",
      total_guests: 1,
      adult_count: 1,
      requested_at: "2026-10-14T21:00:00Z",
    };
    const withPark = sessions.map((s) =>
      s.session_id === 8 ? { ...s, bills: [...s.bills, PARK] } : s,
    );
    const defaultGet = api.get.getMockImplementation();
    api.get.mockImplementation((url) =>
      url.startsWith("/Dashboard/sessions")
        ? Promise.resolve({ status: 200, data: withPark })
        : defaultGet(url),
    );
    await renderDashboard();

    fireEvent.click(rowAction("#8", "View session"));
    fireEvent.click(
      within(await screen.findByRole("dialog")).getByRole("button", {
        name: "Move Ana Lee",
      }),
    );
    const dialog = await screen.findByRole("dialog", { name: /Move Ana Lee/ });
    fireEvent.mouseDown(
      within(dialog).getByRole("combobox", { name: /Move to/ }),
    );
    fireEvent.click(await screen.findByRole("option", { name: "Park" }));
    expect(
      within(dialog).getByText(/Park has asked for the bill/),
    ).toBeInTheDocument();

    // Lee is 4 adults, so up to 3 can move and still leave one
    const howMany = within(dialog).getByRole("spinbutton", {
      name: "How many",
    });
    expect(within(dialog).getByText("Up to 3")).toBeInTheDocument();
    fireEvent.change(howMany, { target: { value: "3" } });
    expect(
      within(dialog).getByText(
        "Lee goes from 4 guests to 1 guest, and Park from 1 guest to 4 guests.",
      ),
    ).toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole("button", { name: "Move" }));

    await waitFor(() =>
      expect(api.put).toHaveBeenCalledWith(
        "/SessionParticipant/8/participants/5/bill",
        {
          bill_id: 81,
          move_orders: true,
          guest_type: "adult",
          guest_count: 3,
        },
      ),
    );
  });

  it("can move someone without changing either bill's guest count", async () => {
    const PARK = {
      bill_id: 81,
      bill_name: "Park",
      status: "Open",
      total_guests: 1,
      people_on_bill: 0,
    };
    const withPark = sessions.map((s) =>
      s.session_id === 8 ? { ...s, bills: [...s.bills, PARK] } : s,
    );
    const defaultGet = api.get.getMockImplementation();
    api.get.mockImplementation((url) =>
      url.startsWith("/Dashboard/sessions")
        ? Promise.resolve({ status: 200, data: withPark })
        : defaultGet(url),
    );
    await renderDashboard();

    fireEvent.click(rowAction("#8", "View session"));
    fireEvent.click(
      within(await screen.findByRole("dialog")).getByRole("button", {
        name: "Move Ana Lee",
      }),
    );
    const dialog = await screen.findByRole("dialog", { name: /Move Ana Lee/ });
    fireEvent.mouseDown(
      within(dialog).getByRole("combobox", { name: /Move to/ }),
    );
    fireEvent.click(await screen.findByRole("option", { name: "Park" }));
    fireEvent.click(
      within(dialog).getByRole("checkbox", {
        name: "Change both bills' guest counts",
      }),
    );
    expect(
      within(dialog).getByText(/Both bills keep their guest counts/),
    ).toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole("button", { name: "Move" }));

    await waitFor(() =>
      expect(api.put).toHaveBeenCalledWith(
        "/SessionParticipant/8/participants/5/bill",
        {
          bill_id: 81,
          move_orders: true,
          guest_type: null,
          guest_count: 1,
        },
      ),
    );
  });

  it("merges one bill into another, moving its guests even without a phone", async () => {
    // Nobody joined Park on a phone; its 2 guests still move
    const PARK = {
      bill_id: 81,
      bill_name: "Park",
      status: "Open",
      total_guests: 2,
      adult_count: 2,
      people_on_bill: 0,
    };
    const withPark = sessions.map((s) =>
      s.session_id === 8 ? { ...s, bills: [...s.bills, PARK] } : s,
    );
    const defaultGet = api.get.getMockImplementation();
    api.get.mockImplementation((url) =>
      url.startsWith("/Dashboard/sessions")
        ? Promise.resolve({ status: 200, data: withPark })
        : defaultGet(url),
    );
    await renderDashboard();

    fireEvent.click(rowAction("#8", "View session"));
    fireEvent.click(
      within(await screen.findByRole("dialog")).getByRole("button", {
        name: "Merge Park into another bill",
      }),
    );
    const dialog = await screen.findByRole("dialog", { name: /Merge Park/ });
    fireEvent.mouseDown(
      within(dialog).getByRole("combobox", { name: /Merge into/ }),
    );
    fireEvent.click(await screen.findByRole("option", { name: "Lee" }));
    expect(
      within(dialog).getByText(/Lee goes from 4 guests to 6 guests/),
    ).toBeInTheDocument();
    expect(
      within(dialog).getByText(/Park is then cancelled/),
    ).toBeInTheDocument();
    fireEvent.click(
      within(dialog).getByRole("button", { name: "Merge Bills" }),
    );

    await waitFor(() =>
      expect(api.put).toHaveBeenCalledWith(
        "/Bill/merge/81",
        { into_bill_id: 80 },
        { params: { session_id: 8 } },
      ),
    );
  });

  it("only offers to merge when the table has another open bill", async () => {
    await renderDashboard();

    fireEvent.click(rowAction("#8", "View session"));
    const dialog = await screen.findByRole("dialog");
    expect(
      within(dialog).queryByRole("button", { name: /Merge Lee/ }),
    ).not.toBeInTheDocument();
  });

  it("closes a bill from its summary and blocks ending the session until then", async () => {
    await renderDashboard();

    fireEvent.click(rowAction("#8", "View session"));
    const dialog = await screen.findByRole("dialog");
    expect(
      within(dialog).getByRole("button", { name: "End Session" }),
    ).toBeDisabled();
    expect(
      within(dialog).getByText("Close all bills to end the session."),
    ).toBeInTheDocument();
    // Bills are closed from the summary, not the bill list
    expect(
      within(dialog).queryByRole("button", { name: /Close bill/i }),
    ).not.toBeInTheDocument();

    fireEvent.click(
      within(dialog).getByRole("button", { name: "View Lee summary" }),
    );
    const closeButton = await screen.findByRole("button", {
      name: "Close Bill",
    });
    expect(
      screen.queryByRole("button", { name: /Ready for Bill/i }),
    ).not.toBeInTheDocument();
    fireEvent.click(closeButton);

    const confirm = await screen.findByText(/Close Lee\?/);
    fireEvent.click(
      within(confirm.closest('[role="dialog"]')).getByRole("button", {
        name: "Close Bill",
      }),
    );

    await waitFor(() =>
      expect(api.put).toHaveBeenCalledWith(
        "/Bill/close-bill/80",
        null,
        expect.anything(),
      ),
    );
  });
});
