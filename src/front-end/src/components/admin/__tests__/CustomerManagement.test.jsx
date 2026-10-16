import { render, screen, within, waitFor, fireEvent } from "@testing-library/react";
import CustomerManagement from "@/components/admin/CustomerManagement";
import AppNavigationDrawer from "@/components/AppNavigationDrawer";
import api from "@/config/api";

jest.mock("@/config/api", () => {
  const mockApi = { get: jest.fn(), delete: jest.fn() };
  return { __esModule: true, default: mockApi, api: mockApi };
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

const daysAgo = (days) => new Date(Date.now() - days * 86400000).toISOString();

const customer = (id, overrides = {}) => ({
  user_id: id,
  email: `guest${id}@example.com`,
  first_name: "Guest",
  last_name: `${id}`,
  role: "Customer",
  status: "Active",
  created_at: daysAgo(100),
  last_Interaction_at: null,
  is_email_confirmed: true,
  ...overrides,
});

const statValue = (label) =>
  screen.getByText(label).closest(".MuiCard-root").textContent.replace(label, "");

describe("Customer management", () => {
  beforeEach(() => jest.clearAllMocks());

  it("lists customers with their status and email verification", async () => {
    api.get.mockResolvedValue({
      data: [
        customer(1, { first_name: "Mia", last_name: "Chen", created_at: daysAgo(3) }),
        customer(2, { status: "Suspended", is_email_confirmed: false }),
      ],
    });
    render(<CustomerManagement />);

    const row = (await screen.findByText("Mia Chen")).closest('[role="row"]');
    expect(within(row).getByText("guest1@example.com")).toBeInTheDocument();
    expect(within(row).getByText("Verified")).toBeInTheDocument();

    const suspended = screen.getByText("guest2@example.com").closest('[role="row"]');
    expect(within(suspended).getByText("Suspended")).toBeInTheDocument();
    expect(within(suspended).getByText("Unverified")).toBeInTheDocument();

    expect(statValue("Total Customers")).toBe("2");
    expect(statValue("Active Customers")).toBe("1");
    expect(statValue("New in 30 Days")).toBe("1");
  });

  it("loads every page of customers past the API's 100 limit", async () => {
    const firstPage = Array.from({ length: 100 }, (_, i) => customer(i + 1));
    api.get
      .mockResolvedValueOnce({ data: firstPage })
      .mockResolvedValueOnce({ data: [customer(101)] });
    render(<CustomerManagement />);

    await waitFor(() => expect(statValue("Total Customers")).toBe("101"));
    expect(api.get).toHaveBeenNthCalledWith(1, "/staff/customers", { params: { skip: 0, limit: 100 } });
    expect(api.get).toHaveBeenNthCalledWith(2, "/staff/customers", { params: { skip: 100, limit: 100 } });
  });

  it("shows an error when customers can't be loaded", async () => {
    api.get.mockRejectedValue(new Error("network"));
    render(<CustomerManagement />);

    expect(await screen.findByText("Failed to load customers")).toBeInTheDocument();
  });
});

describe("Customer actions", () => {
  const rowFor = (email) => screen.getByText(email).closest('[role="row"]');

  beforeEach(() => {
    jest.clearAllMocks();
    api.get.mockResolvedValue({
      data: [
        customer(1, { first_name: "Mia", last_name: "Chen" }),
        customer(2, { first_name: "Sam", last_name: "Lee", status: "Inactive" }),
      ],
    });
    api.delete.mockResolvedValue({ status: 204 });
  });

  const confirmIn = async (buttonName) => {
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: buttonName }));
    return dialog;
  };

  it("asks before deleting and explains what is removed", async () => {
    render(<CustomerManagement />);
    await screen.findByText("guest1@example.com");

    fireEvent.click(within(rowFor("guest1@example.com")).getByRole("button", { name: "Delete customer" }));
    expect(await screen.findByText(/This can't be undone/)).toBeInTheDocument();
    await confirmIn("Delete");

    await waitFor(() => expect(api.delete).toHaveBeenCalledWith("/staff/customers/1"));
    expect(await screen.findByText("Mia Chen has been deleted")).toBeInTheDocument();
  });

  it("shows the server's message when an action fails", async () => {
    api.delete.mockRejectedValue({ response: { data: "Customer not found" } });
    render(<CustomerManagement />);
    await screen.findByText("guest1@example.com");

    fireEvent.click(within(rowFor("guest1@example.com")).getByRole("button", { name: "Delete customer" }));
    await confirmIn("Delete");

    expect(await screen.findByText("Customer not found")).toBeInTheDocument();
  });
});

describe("Admin navigation", () => {
  it("links to customer management under Management", () => {
    render(<AppNavigationDrawer open role="admin" onClose={jest.fn()} onNavigate={jest.fn()} onLogout={jest.fn()} />);

    const items = screen.getAllByRole("button").map((el) => el.textContent);
    expect(items).toContain("Manage Customers");
    expect(screen.getByText("Management")).toBeInTheDocument();
    // Customers sits with the other management pages, before the tools group
    expect(items.indexOf("Manage Customers")).toBe(items.indexOf("Manage Staff") + 1);
    expect(items.indexOf("Manage Customers")).toBeLessThan(items.indexOf("QR Codes"));
  });

  it("is not shown to staff", () => {
    render(<AppNavigationDrawer open role="staff" onClose={jest.fn()} onNavigate={jest.fn()} onLogout={jest.fn()} />);

    expect(screen.queryByText("Manage Customers")).not.toBeInTheDocument();
    expect(screen.queryByText("Management")).not.toBeInTheDocument();
  });
});
