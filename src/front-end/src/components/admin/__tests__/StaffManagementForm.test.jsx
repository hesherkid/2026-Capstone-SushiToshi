import { render, screen, fireEvent, waitFor } from "@testing-library/react";
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

describe("Staff management form locations", () => {
  const locations = [
    { location_Id: 1, name: "Downtown" },
    { location_Id: 2, name: "Southside" },
  ];
  const sue = {
    email: "sue@example.com",
    first_name: "Sue",
    last_name: "Staff",
    role: "Staff",
    status: "Active",
    location_id: 2,
    location_ids: [1, 2],
  };

  beforeEach(() => api.get.mockResolvedValue({ data: locations }));

  it("shows a staff member's assigned locations", async () => {
    render(<StaffManagementForm mode="edit" initialData={sue} onSubmit={jest.fn()} />);

    expect(await screen.findByText("Downtown")).toBeInTheDocument();
    expect(screen.getByText("Southside")).toBeInTheDocument();
    expect(screen.getByText("They can switch between these locations")).toBeInTheDocument();
  });

  it("sends the assigned locations when saved", async () => {
    const onSubmit = jest.fn();
    render(<StaffManagementForm mode="edit" initialData={sue} onSubmit={onSubmit} />);
    await screen.findByText("Downtown");

    fireEvent.click(screen.getByRole("button", { name: "Save Changes" }));

    await waitFor(() => expect(onSubmit).toHaveBeenCalled());
    expect(onSubmit.mock.calls[0][0].location_ids).toEqual([1, 2]);
  });

  it("warns when a staff member has no locations", async () => {
    render(<StaffManagementForm mode="edit" initialData={{ ...sue, location_ids: [] }} onSubmit={jest.fn()} />);

    expect(await screen.findByText("They can't work anywhere until you choose a location")).toBeInTheDocument();
  });

  it("doesn't ask for locations for admins", async () => {
    const onSubmit = jest.fn();
    render(<StaffManagementForm mode="edit" initialData={admin} onSubmit={onSubmit} />);

    expect(await screen.findByText("Admins can work at every location.")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Save Changes" }));

    await waitFor(() => expect(onSubmit).toHaveBeenCalled());
    expect(onSubmit.mock.calls[0][0]).not.toHaveProperty("location_ids");
  });
});
