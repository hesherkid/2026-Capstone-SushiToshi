import { render, screen } from "@testing-library/react";
import DevResetButton, { resetBrowserSession } from "../DevResetButton";

describe("Dev reset button", () => {
  const originalEnv = process.env.NODE_ENV;

  afterEach(() => {
    process.env.NODE_ENV = originalEnv;
    localStorage.clear();
    sessionStorage.clear();
  });

  it("shows when running in development", () => {
    process.env.NODE_ENV = "development";

    render(<DevResetButton />);

    expect(screen.getByRole("button", { name: "Dev: reset and sign out" })).toBeInTheDocument();
  });

  it.each(["production", "test"])("isn't there in %s", (env) => {
    process.env.NODE_ENV = env;

    render(<DevResetButton />);

    expect(screen.queryByRole("button", { name: /reset/i })).not.toBeInTheDocument();
  });

  it("forgets the sign-in, guest, table visit and cart", () => {
    ["authToken", "guest", "session_id", "tableNumber", "currentBillId", "menuCart"].forEach((key) =>
      localStorage.setItem(key, "x"),
    );
    sessionStorage.setItem("anything", "x");
    // jsdom can't really navigate; the full page load to sign-in is the last step
    const navigationError = jest.spyOn(console, "error").mockImplementation(() => {});

    resetBrowserSession();

    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
    navigationError.mockRestore();
  });
});
