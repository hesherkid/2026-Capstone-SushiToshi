import api from "@/config/api";
import { loginAsGuest } from "@/utils/auth";

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
