import api from "@/config/api";
import { loginAsGuest, getReusableGuestToken } from "@/utils/auth";

jest.mock("@/config/api", () => ({
  __esModule: true,
  default: { post: jest.fn() },
}));

describe("loginAsGuest", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    localStorage.clear();
  });

  it("asks the server for a new guest and saves the guest's token", async () => {
    api.post.mockResolvedValue({
      data: {
        access_token: "guest-token",
        expires_in: 21600,
        user: { first_name: "Guest", last_name: "K7P2" },
      },
    });

    const data = await loginAsGuest();

    expect(api.post).toHaveBeenCalledWith("/auth/guest");
    expect(data.access_token).toBe("guest-token");
    expect(localStorage.getItem("authToken")).toBe("guest-token");
    expect(localStorage.getItem("access_token")).toBe("guest-token");
    expect(JSON.parse(localStorage.getItem("user"))).toEqual({ first_name: "Guest", last_name: "K7P2" });
    // 6 hours from now
    expect(Number(localStorage.getItem("tokenExpiresAt"))).toBeGreaterThan(Date.now() + 21500 * 1000);
  });
});

// Builds an unsigned JWT; only the payload matters to the front end
const tokenWith = (claims) =>
  ["header", btoa(JSON.stringify(claims)), "signature"].join(".");

const EMAIL_CLAIM = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress";
const hoursFromNow = (hours) => Math.floor(Date.now() / 1000) + hours * 3600;

describe("getReusableGuestToken", () => {
  beforeEach(() => localStorage.clear());

  it("reuses a guest token with time left", () => {
    const token = tokenWith({ guest: "true", [EMAIL_CLAIM]: "guest-abc@guest.sushitoshi.invalid", exp: hoursFromNow(5) });
    localStorage.setItem("authToken", token);

    expect(getReusableGuestToken()).toBe(token);
  });

  it("recognises guest tokens issued before the guest claim existed", () => {
    const token = tokenWith({ [EMAIL_CLAIM]: "guest-abc@guest.sushitoshi.invalid", exp: hoursFromNow(5) });
    localStorage.setItem("authToken", token);

    expect(getReusableGuestToken()).toBe(token);
  });

  it("doesn't reuse a guest token that's about to expire", () => {
    localStorage.setItem("authToken", tokenWith({ guest: "true", exp: hoursFromNow(0.2) }));

    expect(getReusableGuestToken()).toBeNull();
  });

  it("doesn't reuse a signed-in customer's token", () => {
    localStorage.setItem("authToken", tokenWith({ [EMAIL_CLAIM]: "mia@example.com", exp: hoursFromNow(1) }));

    expect(getReusableGuestToken()).toBeNull();
  });

  it("returns nothing when no one is signed in", () => {
    expect(getReusableGuestToken()).toBeNull();
  });
});
