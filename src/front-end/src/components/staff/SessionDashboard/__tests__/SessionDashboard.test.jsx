import { render, screen, within, fireEvent, waitFor } from "@testing-library/react";
import SessionDashboard from "@/components/staff/SessionDashboard";
import api from "@/config/api";

jest.mock("@/config/api", () => {
  const mockApi = { get: jest.fn(), post: jest.fn(), put: jest.fn(), delete: jest.fn() };
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

const sessions = [
  {
    session_Id: 7,
    table_Numbers: [3],
    started_At: "2026-10-14T20:00:00Z",
    active_Participants: 2,
    bills: [{ bill_Id: 70, bill_Name: "Smith", status: "Closed", total_Guests: 2 }],
    bill_Count: 1,
    is_Closable: true,
  },
  {
    session_Id: 8,
    table_Numbers: [1, 2],
    started_At: "2026-10-14T20:10:00Z",
    active_Participants: 4,
    bills: [{ bill_Id: 80, bill_Name: "Lee", status: "Open", total_Guests: 4 }],
    bill_Count: 1,
    is_Closable: false,
  },
];

const requests = [
  { request_id: 11, session_id: 7, table_number: 3, notes: "More soy sauce", created_at: "2026-10-14T20:30:00Z" },
  // Another location's session
  { request_id: 99, session_id: 42, table_number: 9, notes: "Other location", created_at: "2026-10-14T20:31:00Z" },
];

const rowFor = (text) => screen.getByText(text).closest('[role="row"]');

const renderDashboard = async () => {
  render(<SessionDashboard />);
  await waitFor(() => rowFor("#7"));
};

describe("Session dashboard", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    api.get.mockImplementation((url) => {
      if (url.startsWith("/Dashboard/sessions")) return Promise.resolve({ status: 200, data: sessions });
      if (url === "/ServiceRequest/pending") return Promise.resolve({ status: 200, data: requests });
      if (url === "/Bill/summary/80") {
        return Promise.resolve({
          status: 200,
          data: { bill_id: 80, bill_name: "Lee", status: "Open", addon_items: [], total_amount: 180.6 },
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

    // Requests from other locations are ignored; one fetch each, not one per session
    expect(screen.getByText("Pending Requests").closest(".MuiCard-root")).toHaveTextContent("1Pending Requests");
    expect(screen.getByText("Tables In Use").closest(".MuiCard-root")).toHaveTextContent("3Tables In Use");
    expect(api.get).toHaveBeenCalledWith("/Dashboard/sessions?locationId=1");
    expect(api.get).toHaveBeenCalledWith("/ServiceRequest/pending");
    expect(api.get.mock.calls.some(([url]) => url.includes("by-session"))).toBe(false);
  });

  it("only offers to end sessions with no open bills, and asks first", async () => {
    await renderDashboard();

    expect(within(rowFor("#8")).queryByRole("button", { name: "End session" })).not.toBeInTheDocument();

    fireEvent.click(within(rowFor("#7")).getByRole("button", { name: "End session" }));
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText(/End Session #7 for table 3\?/)).toBeInTheDocument();

    fireEvent.click(within(dialog).getByRole("button", { name: "End Session" }));
    await waitFor(() => expect(api.put).toHaveBeenCalledWith("/DiningSession/7/close"));
  });

  it("shows a session's requests and lets staff mark them done", async () => {
    await renderDashboard();

    fireEvent.click(within(rowFor("#7")).getByRole("button", { name: "View session" }));
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText("More soy sauce")).toBeInTheDocument();
    expect(within(dialog).getByText("Smith")).toBeInTheDocument();

    fireEvent.click(within(dialog).getByRole("button", { name: "Mark request done" }));
    await waitFor(() => expect(api.post).toHaveBeenCalledWith("/ServiceRequest/11/complete"));
  });

  it("closes a bill from its summary and blocks ending the session until then", async () => {
    await renderDashboard();

    fireEvent.click(within(rowFor("#8")).getByRole("button", { name: "View session" }));
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByRole("button", { name: "End Session" })).toBeDisabled();
    expect(within(dialog).getByText("Close all bills to end the session.")).toBeInTheDocument();
    // Bills are closed from the summary, not the bill list
    expect(within(dialog).queryByRole("button", { name: /Close bill/i })).not.toBeInTheDocument();

    fireEvent.click(within(dialog).getByRole("button", { name: "View Lee summary" }));
    const closeButton = await screen.findByRole("button", { name: "Close Bill" });
    expect(screen.queryByRole("button", { name: /Ready for Bill/i })).not.toBeInTheDocument();
    fireEvent.click(closeButton);

    const confirm = await screen.findByText(/Close Lee\?/);
    fireEvent.click(
      within(confirm.closest('[role="dialog"]')).getByRole("button", { name: "Close Bill" })
    );

    await waitFor(() => expect(api.put).toHaveBeenCalledWith("/Bill/close_bill/80", null, expect.anything()));
  });
});
