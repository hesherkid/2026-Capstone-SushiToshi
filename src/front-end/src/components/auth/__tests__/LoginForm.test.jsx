import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useRouter, useSearchParams } from "next/navigation";
import LoginForm from "../LoginForm";
import { loginUser } from "@/utils/auth";

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
        screen.getByRole("heading", { name: /Login to Sushi Toshi/i }),
      ).toBeInTheDocument();
      expect(screen.getByLabelText(/Email Address/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/Password/i)).toBeInTheDocument();
      expect(
        screen.getByRole("button", { name: /^Sign In$/i }),
      ).toBeInTheDocument();
      expect(
        screen.getByRole("button", { name: /Sign In As Guest/i }),
      ).toBeInTheDocument();
      expect(
        screen.getByRole("button", { name: /Forgot Password/i }),
      ).toBeInTheDocument();
      expect(
        screen.getByRole("button", { name: /Create New Account/i }),
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

    it("renders the lock icon", () => {
      const { container } = render(<LoginForm />);

      expect(container.querySelector("svg")).toBeInTheDocument();
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
          screen.getByText(/no access token was returned/i),
        ).toBeInTheDocument();
      });
      expect(mockReplace).not.toHaveBeenCalled();
    });

    it("shows a loading spinner and disables the button while submitting", async () => {
      const user = userEvent.setup();
      loginUser.mockImplementation(() => new Promise(() => {})); // never resolves

      render(<LoginForm />);
      await fillLoginForm(user);
      const submitButton = screen.getByRole("button", { name: /^Sign In$/i });
      await user.click(submitButton);

      expect(screen.getByRole("progressbar")).toBeInTheDocument();
      expect(submitButton).toBeDisabled();
    });

    it("prevents a second submission while loading", async () => {
      const user = userEvent.setup();
      loginUser.mockImplementation(() => new Promise(() => {}));

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
        screen.getByRole("button", { name: /Sign In As Guest/i }),
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
          screen.getByText(/Authentication failed. Please try again./i),
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
    it("navigates to the forgot password page", async () => {
      const user = userEvent.setup();
      render(<LoginForm />);

      await user.click(
        screen.getByRole("button", { name: /Forgot Password/i }),
      );

      expect(mockPush).toHaveBeenCalledWith("/auth/forgot-password");
    });

    it("navigates to the register page", async () => {
      const user = userEvent.setup();
      render(<LoginForm />);

      await user.click(
        screen.getByRole("button", { name: /Create New Account/i }),
      );

      expect(mockPush).toHaveBeenCalledWith("/auth/register");
    });

    it("keeps the restaurant QR context when navigating", async () => {
      const user = userEvent.setup();
      useSearchParams.mockReturnValue(
        new URLSearchParams("locationId=2&tableNumber=7"),
      );

      render(<LoginForm />);
      await user.click(
        screen.getByRole("button", { name: /Create New Account/i }),
      );

      expect(mockPush).toHaveBeenCalledWith(
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
