import { render, screen, within, waitFor, fireEvent } from "@testing-library/react";
import StaffManagementPage from "@/components/admin/StaffManagementPage";
import api from "@/config/api";
import { useAuth } from "@/hooks/useAuth";

jest.mock("@/config/api", () => {
  const mockApi = { get: jest.fn(), put: jest.fn(), post: jest.fn() };
  return { __esModule: true, default: mockApi, api: mockApi };
});

jest.mock("@/hooks/useAuth", () => ({ useAuth: jest.fn() }));

// Stand-ins for the form and password dialog: just the error they're given and their buttons
jest.mock("@/components/admin/StaffManagementForm", () => ({
  __esModule: true,
  default: ({ error, onSubmit, onClose }) => (
    <div data-testid="staff-form">
      {error && <p data-testid="staff-form-error">{error}</p>}
      <button onClick={() => onSubmit({ email: "new@example.com", role: "staff" }).catch(() => {})}>Submit form</button>
      <button onClick={onClose}>Cancel form</button>
    </div>
  ),
}));
jest.mock("@/components/admin/PasswordChangeDialog", () => ({
  __esModule: true,
  default: ({ open, error }) =>
    open ? (
      <div data-testid="password-dialog">
        {error && <p data-testid="password-dialog-error">{error}</p>}
      </div>
    ) : null,
}));

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

const staff = [
  { user_id: 1, email: "admin@example.com", first_name: "Ada", last_name: "Admin", role: "Admin", status: "Active" },
  { user_id: 2, email: "sue@example.com", first_name: "Sue", last_name: "Staff", role: "Staff", status: "Active" },
  { user_id: 3, email: "tom@example.com", first_name: "Tom", last_name: "Staff", role: "Staff", status: "Inactive" },
];

const rowFor = (email) => screen.getByText(email).closest('[role="row"]');

describe("Staff management account status", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    useAuth.mockReturnValue({ userRole: "admin", userEmail: "admin@example.com" });
    api.get.mockResolvedValue({ status: 200, data: staff });
    api.put.mockResolvedValue({ status: 200, data: {} });
  });

  it("only loads staff, not the customer list", async () => {
    render(<StaffManagementPage />);
    await screen.findByText("sue@example.com");

    expect(api.get).toHaveBeenCalledWith("/staff");
    expect(api.get.mock.calls.some(([url]) => url.includes("customers"))).toBe(false);
  });

  it("asks before deactivating a staff member", async () => {
    render(<StaffManagementPage />);
    await screen.findByText("sue@example.com");

    fireEvent.click(within(rowFor("sue@example.com")).getByRole("button", { name: "Deactivate Account" }));
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText(/Deactivate Sue Staff\?/)).toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole("button", { name: "Deactivate" }));

    await waitFor(() =>
      expect(api.put).toHaveBeenCalledWith("/staff/2/status", { status: "Inactive" })
    );
  });

  it("reactivates an inactive staff member straight away", async () => {
    render(<StaffManagementPage />);
    await screen.findByText("tom@example.com");

    fireEvent.click(within(rowFor("tom@example.com")).getByRole("button", { name: "Reactivate Account" }));

    await waitFor(() =>
      expect(api.put).toHaveBeenCalledWith("/staff/3/status", { status: "Active" })
    );
  });

  it("doesn't offer admins a way to deactivate their own account", async () => {
    render(<StaffManagementPage />);
    await screen.findByText("admin@example.com");

    const ownRow = rowFor("admin@example.com");
    expect(within(ownRow).queryByRole("button", { name: "Deactivate Account" })).not.toBeInTheDocument();
    expect(within(ownRow).getByRole("button", { name: "Edit User" })).toBeInTheDocument();
  });

  it("hides status actions from anyone who isn't an admin", async () => {
    useAuth.mockReturnValue({ userRole: "staff", userEmail: "sue@example.com" });
    render(<StaffManagementPage />);
    await screen.findByText("tom@example.com");

    expect(screen.queryByRole("button", { name: "Deactivate Account" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Reactivate Account" })).not.toBeInTheDocument();
  });
});

describe("Staff management dialogs", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    useAuth.mockReturnValue({ userRole: "admin", userEmail: "admin@example.com" });
    api.get.mockResolvedValue({ status: 200, data: staff });
  });

  it("doesn't carry a failed save's error into the next dialog", async () => {
    api.post.mockRejectedValue({ response: { status: 400, data: { detail: "Email already exists" } } });
    render(<StaffManagementPage />);
    await screen.findByText("sue@example.com");

    fireEvent.click(screen.getByRole("button", { name: "Create New Staff Account" }));
    fireEvent.click(screen.getByRole("button", { name: "Submit form" }));
    expect(await screen.findByTestId("staff-form-error")).toHaveTextContent("Email already exists");
    fireEvent.click(screen.getByRole("button", { name: "Cancel form" }));

    // Another dialog opens clean
    fireEvent.click(within(rowFor("sue@example.com")).getByRole("button", { name: "Reset Password" }));
    expect(screen.getByTestId("password-dialog")).toBeInTheDocument();
    expect(screen.queryByTestId("password-dialog-error")).not.toBeInTheDocument();

    // And so does the form when it's opened again
    fireEvent.click(within(rowFor("sue@example.com")).getByRole("button", { name: "Edit User" }));
    expect(screen.getByTestId("staff-form")).toBeInTheDocument();
    expect(screen.queryByTestId("staff-form-error")).not.toBeInTheDocument();
  });
});
