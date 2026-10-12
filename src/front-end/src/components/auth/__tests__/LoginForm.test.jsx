import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useRouter, useSearchParams } from "next/navigation";
import LoginForm from "../LoginForm";
import { loginUser } from "@/utils/auth";
import api from "@/config/api";
import { decodeToken } from "@/config/auth";

jest.mock("@/config/api", () => ({
  __esModule: true,
  default: {
    get: jest.fn(),
    post: jest.fn(),
  },
}));

jest.mock("@/config/auth", () => ({ decodeToken: jest.fn() }));

// Mock dependencies
jest.mock("next/navigation", () => ({
  useRouter: jest.fn(),
  useSearchParams: jest.fn(() => new URLSearchParams()),
  usePathname: jest.fn(() => "/"),
}));

jest.mock("@/utils/auth", () => ({
  loginUser: jest.fn(),
}));

const fillLoginForm = async (
  user,
  email = "test@example.com",
  password = "password123",
) => {
  const emailInput = screen.getByLabelText(/Email Address/i);
  const passwordInput = screen.getByLabelText(/Password/i);
  await user.clear(emailInput);
  await user.type(emailInput, email);
  await user.clear(passwordInput);
  await user.type(passwordInput, password);
};

describe("LoginForm", () => {
  const mockPush = jest.fn();
  const mockReplace = jest.fn();
  const mockRefresh = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
    localStorage.clear();
    useRouter.mockReturnValue({
      push: mockPush,
      replace: mockReplace,
      refresh: mockRefresh,
    });
    useSearchParams.mockReturnValue(new URLSearchParams());
  });

  describe("Rendering", () => {
    it("renders the login form with all elements", () => {
      render(<LoginForm />);

      expect(
        screen.getByRole("heading", { name: /Start your order/i }),
      ).toBeInTheDocument();
      expect(screen.getByLabelText(/Email Address/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/Password/i)).toBeInTheDocument();
      expect(
        screen.getByRole("button", { name: /^Sign In$/i }),
      ).toBeInTheDocument();
      expect(
        screen.getByRole("button", { name: /Continue as Guest/i }),
      ).toBeInTheDocument();
      expect(
        screen.getByRole("link", { name: /Forgot Password/i }),
      ).toBeInTheDocument();
      expect(
        screen.getByRole("link", { name: /Sign Up/i }),
      ).toBeInTheDocument();
    });

    it("starts with empty fields when no default credentials are configured", () => {
      render(<LoginForm />);

      expect(screen.getByLabelText(/Email Address/i)).toHaveValue("");
      expect(screen.getByLabelText(/Password/i)).toHaveValue("");
    });

    it("pre-fills fields from NEXT_PUBLIC_DEFAULT_* when set", () => {
      process.env.NEXT_PUBLIC_DEFAULT_EMAIL = "admin.user@sushitoshi.ca";
      process.env.NEXT_PUBLIC_DEFAULT_PASSWORD = "AdminPass123!";

      try {
        render(<LoginForm />);

        expect(screen.getByLabelText(/Email Address/i)).toHaveValue(
          "admin.user@sushitoshi.ca",
        );
        expect(screen.getByLabelText(/Password/i)).toHaveValue("AdminPass123!");
      } finally {
        delete process.env.NEXT_PUBLIC_DEFAULT_EMAIL;
        delete process.env.NEXT_PUBLIC_DEFAULT_PASSWORD;
      }
    });

    it("shows a success alert after a password reset", () => {
      useSearchParams.mockReturnValue(new URLSearchParams("reset=success"));

      render(<LoginForm />);

      expect(
        screen.getByText(/Your password has been reset successfully/i),
      ).toBeInTheDocument();
    });
  });

  describe("Form Interaction", () => {
    it("allows users to type in email and password fields", async () => {
      const user = userEvent.setup();
      render(<LoginForm />);

      await fillLoginForm(user, "user@test.com", "newpassword123");

      expect(screen.getByLabelText(/Email Address/i)).toHaveValue(
        "user@test.com",
      );
      expect(screen.getByLabelText(/Password/i)).toHaveValue("newpassword123");
    });
  });

  describe("Form Submission", () => {
    it("submits the entered credentials", async () => {
      const user = userEvent.setup();
      loginUser.mockResolvedValue({ access_token: "test-token" });

      render(<LoginForm />);
      await fillLoginForm(user);
      await user.click(screen.getByRole("button", { name: /^Sign In$/i }));

      await waitFor(() => {
        expect(loginUser).toHaveBeenCalledWith(
          "test@example.com",
          "password123",
        );
      });
    });

    it("redirects to the home page on successful login", async () => {
      const user = userEvent.setup();
      loginUser.mockResolvedValue({ access_token: "test-token" });

      render(<LoginForm />);
      await fillLoginForm(user);
      await user.click(screen.getByRole("button", { name: /^Sign In$/i }));

      await waitFor(() => {
        expect(mockReplace).toHaveBeenCalledWith("/");
      });
      expect(mockRefresh).toHaveBeenCalled();
    });

    it("saves the restaurant QR context (location + table) on successful login", async () => {
      const user = userEvent.setup();
      useSearchParams.mockReturnValue(
        new URLSearchParams("locationId=2&tableNumber=7"),
      );
      loginUser.mockResolvedValue({ access_token: "test-token" });

      render(<LoginForm />);
      await fillLoginForm(user);
      await user.click(screen.getByRole("button", { name: /^Sign In$/i }));

      await waitFor(() => {
        expect(localStorage.getItem("locationId")).toBe("2");
      });
      expect(localStorage.getItem("tableNumber")).toBe("7");
    });

    it("shows an error when the response has no access token", async () => {
      const user = userEvent.setup();
      loginUser.mockResolvedValue({});

      render(<LoginForm />);
      await fillLoginForm(user);
      await user.click(screen.getByRole("button", { name: /^Sign In$/i }));

      await waitFor(() => {
        expect(
          screen.getByText(/Something went wrong\. Please try again\./i),
        ).toBeInTheDocument();
      });
      expect(mockReplace).not.toHaveBeenCalled();
    });

    it("shows a loading spinner and disables the button while submitting", async () => {
      const user = userEvent.setup();
      loginUser.mockImplementation(() => new Promise(() => { })); // never resolves

      render(<LoginForm />);
      await fillLoginForm(user);
      const submitButton = screen.getByRole("button", { name: /^Sign In$/i });
      await user.click(submitButton);

      expect(screen.getByRole("progressbar")).toBeInTheDocument();
      expect(submitButton).toBeDisabled();
    });

    it("prevents a second submission while loading", async () => {
      const user = userEvent.setup();
      loginUser.mockImplementation(() => new Promise(() => { }));

      render(<LoginForm />);
      await fillLoginForm(user);
      const submitButton = screen.getByRole("button", { name: /^Sign In$/i });
      await user.click(submitButton);

      expect(submitButton).toBeDisabled();
      expect(loginUser).toHaveBeenCalledTimes(1);
    });
  });

  describe("Guest Login", () => {
    it("signs in with the guest account and marks the session as guest", async () => {
      const user = userEvent.setup();
      loginUser.mockResolvedValue({ access_token: "guest-token" });

      render(<LoginForm />);
      await user.click(
        screen.getByRole("button", { name: /Continue as Guest/i }),
      );

      await waitFor(() => {
        expect(loginUser).toHaveBeenCalledWith(
          "guestemail@email.com",
          "GuestUser!",
        );
      });
      await waitFor(() => {
        expect(mockReplace).toHaveBeenCalledWith("/");
      });
      expect(localStorage.getItem("guest")).toBe("true");
    });

    it("creates a session for the table in the scanned QR code", async () => {
      const user = userEvent.setup();
      loginUser.mockResolvedValue({ access_token: "guest-token" });
      decodeToken.mockReturnValue({
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier": "22",
      });
      useSearchParams.mockReturnValue(
        new URLSearchParams("locationId=3&tableNumber=8")
      );
      api.get.mockImplementation((url) => {
        if (url === "/TableEntity") {
          return Promise.resolve({
            data: [{ table_Id: 5, table_number: 8, location_Id: 3, is_active: true }],
          });
        }
        if (url === "/Menu") {
          return Promise.resolve({ data: [{ menu_id: 12, location_Id: 3 }] });
        }
        return Promise.resolve({ data: [] });
      });
      api.post.mockResolvedValue({ data: { session_Id: 101 } });

      render(<LoginForm />);
      await user.click(screen.getByRole("button", { name: /Continue as Guest/i }));

      await waitFor(() => {
        expect(api.post).toHaveBeenCalledWith(
          "/DiningSession/Create_Dinning_Session",
          expect.objectContaining({
            Menu_Id: 12,
            Location_Id: 3,
            Table_Id: 5,
            AssignmentType: "table",
            Request_By_User_Id: 22,
            Request_By_Name: "Guest",
          })
        );
        expect(mockReplace).toHaveBeenCalledWith("/menu/full-menu");
      });

      expect(localStorage.getItem("session_id")).toBe("101");
    });

  });

  describe("Error Handling", () => {
    it("displays the server error detail on failed login", async () => {
      const user = userEvent.setup();
      loginUser.mockRejectedValue({
        response: { status: 401, data: { detail: "Invalid credentials" } },
      });

      render(<LoginForm />);
      await fillLoginForm(user);
      await user.click(screen.getByRole("button", { name: /^Sign In$/i }));

      await waitFor(() => {
        expect(screen.getByText("Invalid credentials")).toBeInTheDocument();
      });
    });

    it("displays a default error message when no detail is provided", async () => {
      const user = userEvent.setup();
      loginUser.mockRejectedValue({ response: { status: 500, data: {} } });

      render(<LoginForm />);
      await fillLoginForm(user);
      await user.click(screen.getByRole("button", { name: /^Sign In$/i }));

      await waitFor(() => {
        expect(
          screen.getByText(/Something went wrong\. Please try again\./i),
        ).toBeInTheDocument();
      });
    });

    it("re-enables the form after an error", async () => {
      const user = userEvent.setup();
      loginUser.mockRejectedValue({ response: { data: { detail: "Error" } } });

      render(<LoginForm />);
      await fillLoginForm(user);
      const submitButton = screen.getByRole("button", { name: /^Sign In$/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText("Error")).toBeInTheDocument();
      });
      expect(submitButton).not.toBeDisabled();
      expect(screen.queryByRole("progressbar")).not.toBeInTheDocument();
    });

    it("clears the previous error on a new submission", async () => {
      const user = userEvent.setup();
      loginUser
        .mockRejectedValueOnce({ response: { data: { detail: "Error" } } })
        .mockResolvedValueOnce({ access_token: "test-token" });

      render(<LoginForm />);
      await fillLoginForm(user);
      const submitButton = screen.getByRole("button", { name: /^Sign In$/i });

      await user.click(submitButton);
      await waitFor(() => {
        expect(screen.getByText("Error")).toBeInTheDocument();
      });

      await user.click(submitButton);
      await waitFor(() => {
        expect(screen.queryByText("Error")).not.toBeInTheDocument();
      });
    });

    it("asks unverified users to verify their email and links to resend", async () => {
      const user = userEvent.setup();
      loginUser.mockRejectedValue({
        response: {
          status: 403,
          data: {
            requires_email_verification: true,
            message: "Please verify your email before signing in.",
          },
        },
      });

      render(<LoginForm />);
      await fillLoginForm(user);
      await user.click(screen.getByRole("button", { name: /^Sign In$/i }));

      await waitFor(() => {
        expect(
          screen.getByText(/Please verify your email before signing in./i),
        ).toBeInTheDocument();
      });

      await user.click(
        screen.getByRole("button", { name: /Resend Verification Email/i }),
      );
      expect(mockPush).toHaveBeenCalledWith("/auth/resend-verification");
    });
  });

  describe("Navigation", () => {
    it("links to the forgot password page", () => {
      render(<LoginForm />);

      expect(
        screen.getByRole("link", { name: /Forgot Password/i }),
      ).toHaveAttribute("href", "/auth/forgot-password");
    });

    it("links to the register page", () => {
      render(<LoginForm />);

      expect(screen.getByRole("link", { name: /Sign Up/i })).toHaveAttribute(
        "href",
        "/auth/register",
      );
    });

    it("keeps the restaurant QR context in the sign-up link", () => {
      useSearchParams.mockReturnValue(
        new URLSearchParams("locationId=2&tableNumber=7"),
      );

      render(<LoginForm />);

      expect(screen.getByRole("link", { name: /Sign Up/i })).toHaveAttribute(
        "href",
        "/auth/register?locationId=2&tableNumber=7",
      );
    });
  });

  describe("Accessibility and Validation", () => {
    it("email input has type email and is required", () => {
      render(<LoginForm />);

      const emailInput = screen.getByLabelText(/Email Address/i);
      expect(emailInput).toHaveAttribute("type", "email");
      expect(emailInput).toBeRequired();
    });

    it("password input has type password and is required", () => {
      render(<LoginForm />);

      const passwordInput = screen.getByLabelText(/Password/i);
      expect(passwordInput).toHaveAttribute("type", "password");
      expect(passwordInput).toBeRequired();
    });

    it("sign in button is a submit button", () => {
      render(<LoginForm />);

      expect(
        screen.getByRole("button", { name: /^Sign In$/i }),
      ).toHaveAttribute("type", "submit");
    });
  });
});
