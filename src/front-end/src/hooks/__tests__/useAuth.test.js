import { renderHook, waitFor } from "@testing-library/react";
import { useAuth } from "../useAuth";

const tokenFor = (claims) => `x.${btoa(JSON.stringify(claims))}.x`;
const IN_AN_HOUR = Math.floor(Date.now() / 1000) + 3600;

describe("useAuth isGuest", () => {
  beforeEach(() => localStorage.clear());

  it("is true for a Continue as Guest account, which is still a customer", async () => {
    localStorage.setItem("authToken", tokenFor({ role: "Customer", guest: "true", exp: IN_AN_HOUR }));

    const { result } = renderHook(() => useAuth());

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.isGuest).toBe(true);
    expect(result.current.userRole).toBe("customer");
  });

  it("is false for a customer with an account", async () => {
    localStorage.setItem("authToken", tokenFor({ role: "Customer", exp: IN_AN_HOUR }));

    const { result } = renderHook(() => useAuth());

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.isGuest).toBe(false);
  });

  it("is false once a guest's sign-in has expired", async () => {
    localStorage.setItem("authToken", tokenFor({ role: "Customer", guest: "true", exp: 1 }));

    const { result } = renderHook(() => useAuth());

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.isGuest).toBe(false);
    expect(result.current.isAuthenticated).toBe(false);
  });
});
