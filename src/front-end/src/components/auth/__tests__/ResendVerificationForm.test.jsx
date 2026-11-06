import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useSearchParams } from "next/navigation";
import ResendVerificationForm from "../ResendVerificationForm";
import { resendVerificationEmail } from "@/utils/auth";

jest.mock("next/navigation", () => ({
  useSearchParams: jest.fn(() => new URLSearchParams()),
}));

jest.mock("@/utils/auth", () => ({
  resendVerificationEmail: jest.fn(),
}));

const emailField = () => screen.getByLabelText(/Email Address/i);
const submitButton = () =>
  screen.getByRole("button", { name: /Send Verification Email/i });

describe("ResendVerificationForm", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    useSearchParams.mockReturnValue(new URLSearchParams());
  });

  it("pre-fills the email from the URL", () => {
    useSearchParams.mockReturnValue(
      new URLSearchParams("email=john%40example.com"),
    );

    render(<ResendVerificationForm />);

    expect(emailField()).toHaveValue("john@example.com");
  });

  it("sends the trimmed, lowercased email and shows the server's message", async () => {
    const user = userEvent.setup({ delay: null });
    resendVerificationEmail.mockResolvedValue({
      message: "If this account needs verification, a new email will be sent.",
    });

    render(<ResendVerificationForm />);
    await user.type(emailField(), " John@Example.com ");
    await user.click(submitButton());

    expect(resendVerificationEmail).toHaveBeenCalledWith("john@example.com");
    expect(
      await screen.findByText(/a new email will be sent/i),
    ).toBeInTheDocument();
  });

  it("shows an error if the request fails", async () => {
    const user = userEvent.setup({ delay: null });
    resendVerificationEmail.mockRejectedValue({
      response: { data: { message: "Too many requests" } },
    });

    render(<ResendVerificationForm />);
    await user.type(emailField(), "john@example.com");
    await user.click(submitButton());

    expect(await screen.findByText("Too many requests")).toBeInTheDocument();
  });

  it("links back to the login page", () => {
    render(<ResendVerificationForm />);

    expect(screen.getByRole("link", { name: /Back to Login/i })).toHaveAttribute(
      "href",
      "/auth/login",
    );
  });
});
