import { render, screen } from "@testing-library/react";
import StaffManagementForm from "@/components/admin/StaffManagementForm";
import api from "@/config/api";

jest.mock("@/config/api", () => {
  const mockApi = { get: jest.fn() };
  return { __esModule: true, default: mockApi, api: mockApi };
});

const admin = {
  email: "admin@example.com",
  first_name: "Ada",
  last_name: "Admin",
  role: "Admin",
  status: "Active",
};

// Role is the first dropdown; Location is the second
const roleSelect = () => screen.getAllByRole("combobox")[0];

describe("Staff management form", () => {
  beforeEach(() => api.get.mockResolvedValue({ data: [] }));

  it("shows the account's current role when editing", async () => {
    render(<StaffManagementForm mode="edit" initialData={admin} onSubmit={jest.fn()} />);

    expect(await screen.findByText("Admin")).toBeInTheDocument();
  });

  it("locks the role when admins edit their own account", async () => {
    render(<StaffManagementForm mode="edit" initialData={admin} onSubmit={jest.fn()} lockRole />);

    expect(roleSelect()).toHaveAttribute("aria-disabled", "true");
    expect(await screen.findByText("You can't change your own role")).toBeInTheDocument();
  });

  it("leaves the role editable for other accounts", async () => {
    render(<StaffManagementForm mode="edit" initialData={admin} onSubmit={jest.fn()} />);

    expect(roleSelect()).not.toHaveAttribute("aria-disabled");
    expect(screen.queryByText("You can't change your own role")).not.toBeInTheDocument();
  });
});
