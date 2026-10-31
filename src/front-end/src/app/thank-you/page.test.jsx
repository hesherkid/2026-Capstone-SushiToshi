import { act, render, screen, fireEvent } from "@testing-library/react";
import ThankYouPage from "./page";
import { useAuth } from "@/hooks/useAuth";
import { logoutCustomer } from "@/utils/auth";

const mockReplace = jest.fn();
jest.mock("next/navigation", () => ({ useRouter: () => ({ replace: mockReplace, push: jest.fn() }) }));
jest.mock("@/hooks/useAuth", () => ({ useAuth: jest.fn() }));
jest.mock("@/utils/auth", () => ({ logoutCustomer: jest.fn() }));

describe("Thank-you page", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    localStorage.clear();
    useAuth.mockReturnValue({ isAuthenticated: true, loading: false });
  });

  it("lets a customer with an account go back home or log out", () => {
    render(<ThankYouPage />);

    fireEvent.click(screen.getByRole("button", { name: "Back to home page" }));
    expect(mockReplace).toHaveBeenCalledWith("/");

    fireEvent.click(screen.getByRole("button", { name: "Log out" }));
    expect(logoutCustomer).toHaveBeenCalled();
  });

  it("tells a guest how to start another visit, and takes them back to sign in", () => {
    localStorage.setItem("guest", "true");

    render(<ThankYouPage />);

    expect(screen.getByText(/scan the QR code on your table/)).toBeInTheDocument();
    expect(screen.getByText(/Make an account to save favourites/)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Back to home page" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Log out" })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Back to sign in" }));
    expect(mockReplace).toHaveBeenCalledWith("/auth/login");
    // Kept, so their next scan is the same guest
    expect(logoutCustomer).not.toHaveBeenCalled();
  });

  it("doesn't give customers with an account the guest's sign-in button", () => {
    render(<ThankYouPage />);

    expect(screen.queryByRole("button", { name: "Back to sign in" })).not.toBeInTheDocument();
    expect(screen.queryByText(/Make an account/)).not.toBeInTheDocument();
  });

  it("keeps the back button on this page instead of the visit that ended", () => {
    const pushState = jest.spyOn(window.history, "pushState");

    render(<ThankYouPage />);
    expect(pushState).toHaveBeenCalledTimes(1);

    // Pressing back: a copy of this page is put back on top
    act(() => {
      window.dispatchEvent(new PopStateEvent("popstate"));
    });
    expect(pushState).toHaveBeenCalledTimes(2);
    expect(pushState).toHaveBeenLastCalledWith(window.history.state, "", window.location.href);

    pushState.mockRestore();
  });
});
