import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useRouter, useSearchParams } from "next/navigation";
import RegisterForm from "../RegisterForm";
import { registerUser, resendVerificationEmail } from "@/utils/auth";

// Mock dependencies
jest.mock("next/navigation", () => ({
  useRouter: jest.fn(),
  useSearchParams: jest.fn(() => new URLSearchParams()),
  usePathname: jest.fn(() => "/"),
}));

jest.mock("@/utils/auth", () => ({
  registerUser: jest.fn(),
  resendVerificationEmail: jest.fn(),
}));

// Field helpers
const passwordField = () => screen.getByLabelText(/^Password/i);
const confirmPasswordField = () => screen.getByLabelText(/Confirm Password/i);
const submitButton = () =>
  screen.getByRole("button", { name: /^Create Account$/i });

// Fills every field with valid values (the form starts empty)
const fillRegisterForm = async (user, overrides = {}) => {
  const values = {
    firstName: "John",
    lastName: "Doe",
    email: "john@example.com",
    password: "Password123",
    confirmPassword: "Password123",
    ...overrides,
  };

  await user.type(screen.getByLabelText(/First Name/i), values.firstName);
  await user.type(screen.getByLabelText(/Last Name/i), values.lastName);
  await user.type(screen.getByLabelText(/Email Address/i), values.email);
  await user.type(passwordField(), values.password);
  await user.type(confirmPasswordField(), values.confirmPassword);
};

// Register successfully and wait for the "Verify Your Email" screen
const registerSuccessfully = async (user) => {
  registerUser.mockResolvedValue({});
  render(<RegisterForm />);
  await fillRegisterForm(user);
  await user.click(submitButton());
  await waitFor(() => {
    expect(
      screen.getByRole("heading", { name: /Verify Your Email/i }),
    ).toBeInTheDocument();
  });
};

describe("RegisterForm", () => {
  const mockPush = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
    useRouter.mockReturnValue({ push: mockPush });
    useSearchParams.mockReturnValue(new URLSearchParams());
  });

  describe("Rendering", () => {
    it("renders the registration form with all elements", () => {
      render(<RegisterForm />);

      expect(
        screen.getByRole("heading", { name: /Create Account/i }),
      ).toBeInTheDocument();
      expect(screen.getByLabelText(/First Name/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/Last Name/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/Email Address/i)).toBeInTheDocument();
      expect(passwordField()).toBeInTheDocument();
      expect(confirmPasswordField()).toBeInTheDocument();
      expect(submitButton()).toBeInTheDocument();
    });

    it("starts with all fields empty", () => {
      render(<RegisterForm />);

      expect(screen.getByLabelText(/First Name/i)).toHaveValue("");
      expect(screen.getByLabelText(/Last Name/i)).toHaveValue("");
      expect(screen.getByLabelText(/Email Address/i)).toHaveValue("");
      expect(passwordField()).toHaveValue("");
      expect(confirmPasswordField()).toHaveValue("");
    });

    it("renders the UserPlus icon", () => {
      const { container } = render(<RegisterForm />);

      expect(container.querySelector("svg")).toBeInTheDocument();
    });

    it("shows a link to the login page", () => {
      render(<RegisterForm />);

      const link = screen
        .getByText(/Already have an account\? Sign in/i)
        .closest("a");
      expect(link).toHaveAttribute("href", "/auth/login");
    });

    it("keeps the restaurant QR context in the login link", () => {
      useSearchParams.mockReturnValue(
        new URLSearchParams("locationId=2&tableNumber=7"),
      );

      render(<RegisterForm />);

      const link = screen
        .getByText(/Already have an account\? Sign in/i)
        .closest("a");
      expect(link).toHaveAttribute(
        "href",
        "/auth/login?locationId=2&tableNumber=7",
      );
    });
  });

  describe("Form Interaction", () => {
    it("allows users to type in all fields", async () => {
      const user = userEvent.setup();
      render(<RegisterForm />);

      await fillRegisterForm(user);

      expect(screen.getByLabelText(/First Name/i)).toHaveValue("John");
      expect(screen.getByLabelText(/Last Name/i)).toHaveValue("Doe");
      expect(screen.getByLabelText(/Email Address/i)).toHaveValue(
        "john@example.com",
      );
      expect(passwordField()).toHaveValue("Password123");
      expect(confirmPasswordField()).toHaveValue("Password123");
    });
  });

  describe("Form Submission", () => {
    it("submits the entered data (email lower-cased)", async () => {
      const user = userEvent.setup();
      registerUser.mockResolvedValue({});

      render(<RegisterForm />);
      await fillRegisterForm(user, { email: "John@Example.com" });
      await user.click(submitButton());

      await waitFor(() => {
        expect(registerUser).toHaveBeenCalledWith({
          email: "john@example.com",
          password: "Password123",
          firstName: "John",
          lastName: "Doe",
        });
      });
    });

    it("shows a loading spinner and disables the button while submitting", async () => {
      const user = userEvent.setup();
      registerUser.mockImplementation(() => new Promise(() => {})); // never resolves

      render(<RegisterForm />);
      await fillRegisterForm(user);
      const button = submitButton();
      await user.click(button);

      expect(screen.getByRole("progressbar")).toBeInTheDocument();
      expect(button).toBeDisabled();
    });

    it("shows the verify-email screen after successful registration", async () => {
      const user = userEvent.setup();
      await registerSuccessfully(user);

      expect(screen.getByText(/Registration successful!/i)).toBeInTheDocument();
      expect(screen.getByText("john@example.com")).toBeInTheDocument();
      expect(screen.queryByLabelText(/First Name/i)).not.toBeInTheDocument();
    });

    it("navigates to the login page from the success screen", async () => {
      const user = userEvent.setup();
      await registerSuccessfully(user);

      await user.click(screen.getByRole("button", { name: /Go to Login/i }));

      expect(mockPush).toHaveBeenCalledWith("/auth/login");
    });

    it("resends the verification email from the success screen", async () => {
      const user = userEvent.setup();
      resendVerificationEmail.mockResolvedValue({});
      await registerSuccessfully(user);

      await user.click(
        screen.getByRole("button", { name: /Resend Verification Email/i }),
      );

      await waitFor(() => {
        expect(resendVerificationEmail).toHaveBeenCalledWith(
          "john@example.com",
        );
      });
      expect(
        await screen.findByText(/a new verification email will be sent/i),
      ).toBeInTheDocument();
    });

    it("shows an error if resending the verification email fails", async () => {
      const user = userEvent.setup();
      resendVerificationEmail.mockRejectedValue({ response: { data: {} } });
      await registerSuccessfully(user);

      await user.click(
        screen.getByRole("button", { name: /Resend Verification Email/i }),
      );

      expect(
        await screen.findByText(/Unable to resend verification email/i),
      ).toBeInTheDocument();
    });
  });

  describe("Password Validation", () => {
    it("shows an error and does not submit when passwords do not match", async () => {
      const user = userEvent.setup();
      render(<RegisterForm />);

      await fillRegisterForm(user, { confirmPassword: "DifferentPassword" });
      await user.click(submitButton());

      expect(
        await screen.findByText(/Passwords do not match/i),
      ).toBeInTheDocument();
      expect(registerUser).not.toHaveBeenCalled();
    });

    it("shows an error and does not submit when the password is too short", async () => {
      const user = userEvent.setup();
      render(<RegisterForm />);

      await fillRegisterForm(user, {
        password: "short",
        confirmPassword: "short",
      });
      fireEvent.submit(submitButton().closest("form"));

      expect(
        await screen.findByText(/at least 8 characters/i),
      ).toBeInTheDocument();
      expect(registerUser).not.toHaveBeenCalled();
    });
  });

  describe("Error Handling", () => {
    const submitWithError = async (user, rejection) => {
      registerUser.mockRejectedValue(rejection);
      render(<RegisterForm />);
      await fillRegisterForm(user);
      await user.click(submitButton());
    };

    it("displays the server message for a 400 response", async () => {
      const user = userEvent.setup();
      await submitWithError(user, {
        response: { status: 400, data: { detail: "Invalid email format" } },
      });

      expect(
        await screen.findByText("Invalid email format"),
      ).toBeInTheDocument();
    });

    it("displays the server message for a 422 response", async () => {
      const user = userEvent.setup();
      await submitWithError(user, {
        response: { status: 422, data: { message: "Validation error" } },
      });

      expect(await screen.findByText("Validation error")).toBeInTheDocument();
    });

    it("displays a fallback message for a 400 response without details", async () => {
      const user = userEvent.setup();
      await submitWithError(user, { response: { status: 400, data: {} } });

      expect(
        await screen.findByText(/Please check your registration information/i),
      ).toBeInTheDocument();
    });

    it('displays an "already registered" error for a 409 response', async () => {
      const user = userEvent.setup();
      await submitWithError(user, { response: { status: 409 } });

      expect(
        await screen.findByText(/This email address is already registered/i),
      ).toBeInTheDocument();
    });

    it("displays a generic error for other status codes", async () => {
      const user = userEvent.setup();
      await submitWithError(user, { response: { status: 500 } });

      expect(
        await screen.findByText(/Registration failed. Please try again./i),
      ).toBeInTheDocument();
    });

    it("displays a connection error when the server does not respond", async () => {
      const user = userEvent.setup();
      await submitWithError(user, { request: {}, message: "Network Error" });

      expect(
        await screen.findByText(/Unable to connect to the server/i),
      ).toBeInTheDocument();
    });

    it("clears the previous error on a new submission", async () => {
      const user = userEvent.setup();
      registerUser
        .mockRejectedValueOnce({
          response: { status: 400, data: { detail: "Error" } },
        })
        .mockResolvedValueOnce({});

      render(<RegisterForm />);
      await fillRegisterForm(user);

      await user.click(submitButton());
      expect(await screen.findByText("Error")).toBeInTheDocument();

      await user.click(submitButton());
      await waitFor(() => {
        expect(screen.queryByText("Error")).not.toBeInTheDocument();
      });
    });
  });

  describe("Accessibility and Validation", () => {
    it("email input has type email", () => {
      render(<RegisterForm />);

      expect(screen.getByLabelText(/Email Address/i)).toHaveAttribute(
        "type",
        "email",
      );
    });

    it("password inputs have type password", () => {
      render(<RegisterForm />);

      expect(passwordField()).toHaveAttribute("type", "password");
      expect(confirmPasswordField()).toHaveAttribute("type", "password");
    });

    it("all fields are required", () => {
      render(<RegisterForm />);

      expect(screen.getByLabelText(/First Name/i)).toBeRequired();
      expect(screen.getByLabelText(/Last Name/i)).toBeRequired();
      expect(screen.getByLabelText(/Email Address/i)).toBeRequired();
      expect(passwordField()).toBeRequired();
      expect(confirmPasswordField()).toBeRequired();
    });

    it("has proper autocomplete attributes", () => {
      render(<RegisterForm />);

      expect(screen.getByLabelText(/First Name/i)).toHaveAttribute(
        "autocomplete",
        "given-name",
      );
      expect(screen.getByLabelText(/Last Name/i)).toHaveAttribute(
        "autocomplete",
        "family-name",
      );
      expect(screen.getByLabelText(/Email Address/i)).toHaveAttribute(
        "autocomplete",
        "email",
      );
      expect(passwordField()).toHaveAttribute("autocomplete", "new-password");
      expect(confirmPasswordField()).toHaveAttribute(
        "autocomplete",
        "new-password",
      );
    });

    it("password requires at least 8 characters", () => {
      render(<RegisterForm />);

      expect(passwordField()).toHaveAttribute("minlength", "8");
      expect(screen.getByText(/Minimum 8 characters/i)).toBeInTheDocument();
    });
  });
});
