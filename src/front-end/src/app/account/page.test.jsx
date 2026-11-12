import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import AccountPage from "./page";

const mockReplace = jest.fn();
jest.mock("next/navigation", () => ({
  useRouter: () => ({ replace: mockReplace, push: jest.fn() }),
}));

let mockAuth;
jest.mock("@/hooks/useAuth", () => ({
  __esModule: true,
  default: () => mockAuth,
}));

jest.mock("@/config/api", () => ({
  __esModule: true,
  default: { get: jest.fn(), put: jest.fn(), post: jest.fn() },
}));

const api = require("@/config/api").default;

// Only the claims are read, so the header and signature can be anything
const tokenFor = (claims) => `x.${btoa(JSON.stringify(claims))}.x`;

const ACCOUNT = {
  email: "sam@example.com",
  first_name: "Sam",
  last_name: "Tanaka",
  role: "Customer",
  has_password: true,
  can_change_name: true,
};

const type = (label, value) =>
  fireEvent.change(screen.getByLabelText(label), { target: { value } });

const fillPasswords = (current, next, confirm = next) => {
  type(/Current password/, current);
  type(/^New password/, next);
  type(/Confirm new password/, confirm);
};

describe("Account page", () => {
  beforeEach(() => {
    localStorage.clear();
    mockReplace.mockReset();
    api.get.mockReset().mockResolvedValue({ data: ACCOUNT });
    api.put.mockReset();
    api.post.mockReset();
    mockAuth = { isAuthenticated: true, loading: false };
    localStorage.setItem("authToken", tokenFor({ role: "Customer" }));
  });

  it("shows the signed-in user's email and name", async () => {
    render(<AccountPage />);

    expect(await screen.findByText("Signed in as sam@example.com")).toBeInTheDocument();
    expect(screen.getByLabelText(/First name/)).toHaveValue("Sam");
    expect(screen.getByLabelText(/Last name/)).toHaveValue("Tanaka");
    expect(api.get).toHaveBeenCalledWith("/me/account");
  });

  it("saves a changed name, trimmed, and updates the saved greeting name", async () => {
    localStorage.setItem("user", JSON.stringify({ first_name: "Sam", last_name: "Tanaka" }));
    api.put.mockResolvedValue({ data: { ...ACCOUNT, first_name: "Alex", last_name: "Kim" } });
    render(<AccountPage />);
    await screen.findByLabelText(/First name/);

    type(/First name/, "  Alex ");
    type(/Last name/, "Kim");
    fireEvent.click(screen.getByRole("button", { name: "Save name" }));

    expect(await screen.findByText("Your name was saved.")).toBeInTheDocument();
    expect(api.put).toHaveBeenCalledWith("/me/account", { first_name: "Alex", last_name: "Kim" });
    expect(JSON.parse(localStorage.getItem("user"))).toEqual({ first_name: "Alex", last_name: "Kim" });
  });

  it("only allows saving a name that changed and isn't blank", async () => {
    render(<AccountPage />);
    await screen.findByLabelText(/First name/);
    const save = screen.getByRole("button", { name: "Save name" });

    expect(save).toBeDisabled();
    type(/First name/, "   ");
    expect(save).toBeDisabled();
    type(/First name/, "Alex");
    expect(save).toBeEnabled();
  });

  it("changes the password and clears the fields", async () => {
    api.post.mockResolvedValue({ status: 204 });
    render(<AccountPage />);
    await screen.findByLabelText(/Current password/);

    fillPasswords("OldPassword1!", "NewPassword1!");
    fireEvent.click(screen.getByRole("button", { name: "Change password" }));

    expect(await screen.findByText("Your password was changed.")).toBeInTheDocument();
    expect(api.post).toHaveBeenCalledWith("/me/account/change-password", {
      current_password: "OldPassword1!",
      new_password: "NewPassword1!",
    });
    expect(screen.getByLabelText(/Current password/)).toHaveValue("");
  });

  it.each([
    ["NewPassword1!", "Different1!", "New passwords do not match"],
    ["short", "short", "New password must be at least 8 characters long"],
    ["          ", "          ", "New password can't be only spaces"],
  ])("checks the new password before sending it (%s)", async (next, confirm, message) => {
    render(<AccountPage />);
    await screen.findByLabelText(/Current password/);

    fillPasswords("OldPassword1!", next, confirm);
    fireEvent.click(screen.getByRole("button", { name: "Change password" }));

    expect(await screen.findByText(message)).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
  });

  it("shows the server's message for a wrong current password", async () => {
    api.post.mockRejectedValue({
      response: { status: 400, data: { message: "Current password is incorrect" } },
    });
    render(<AccountPage />);
    await screen.findByLabelText(/Current password/);

    fillPasswords("Wrong1234!", "NewPassword1!");
    fireEvent.click(screen.getByRole("button", { name: "Change password" }));

    expect(await screen.findByText("Current password is incorrect")).toBeInTheDocument();
  });

  it("shows staff their name without letting them change it", async () => {
    api.get.mockResolvedValue({ data: { ...ACCOUNT, role: "Staff", can_change_name: false } });
    render(<AccountPage />);

    expect(await screen.findByText("Sam Tanaka")).toBeInTheDocument();
    expect(screen.getByText("Ask an admin to change your name.")).toBeInTheDocument();
    expect(screen.queryByLabelText(/First name/)).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Save name" })).not.toBeInTheDocument();
    // Their password can still be changed
    expect(screen.getByLabelText(/Current password/)).toBeInTheDocument();
  });

  it("explains that Google-only accounts have no password to change", async () => {
    api.get.mockResolvedValue({ data: { ...ACCOUNT, has_password: false } });
    render(<AccountPage />);

    expect(await screen.findByText(/You sign in with Google/)).toBeInTheDocument();
    expect(screen.queryByLabelText(/Current password/)).not.toBeInTheDocument();
  });

  it("sends guests to the menu", async () => {
    localStorage.setItem("authToken", tokenFor({ role: "Customer", guest: "true" }));
    render(<AccountPage />);

    await waitFor(() => expect(mockReplace).toHaveBeenCalledWith("/menu/full-menu"));
    expect(api.get).not.toHaveBeenCalled();
  });

  it("sends signed-out visitors to sign in", async () => {
    localStorage.clear();
    mockAuth = { isAuthenticated: false, loading: false };
    render(<AccountPage />);

    await waitFor(() => expect(mockReplace).toHaveBeenCalledWith("/auth/login"));
  });
});
