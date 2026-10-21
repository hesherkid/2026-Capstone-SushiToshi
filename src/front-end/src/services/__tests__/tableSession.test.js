import api from "@/config/api";
import { fetchMySessionId } from "@/services/tableSession";
import { TABLE_VISIT_ENDED_EVENT } from "@/utils/tableVisit";

jest.mock("@/config/api", () => ({
  __esModule: true,
  default: { get: jest.fn() },
}));

const notFound = () => Promise.reject({ response: { status: 404 } });

describe("fetchMySessionId", () => {
  let ended;

  beforeEach(() => {
    localStorage.clear();
    api.get.mockReset();
    ended = jest.fn();
    window.addEventListener(TABLE_VISIT_ENDED_EVENT, ended);
  });

  afterEach(() => {
    window.removeEventListener(TABLE_VISIT_ENDED_EVENT, ended);
  });

  it("saves the session the customer is seated in", async () => {
    api.get.mockResolvedValue({ data: { session_id: 42 } });

    await expect(fetchMySessionId()).resolves.toBe(42);
    expect(localStorage.getItem("session_id")).toBe("42");
    expect(ended).not.toHaveBeenCalled();
  });

  it("ends the visit when staff close the session this phone was seated in", async () => {
    localStorage.setItem("session_id", "42");
    localStorage.setItem("tableNumber", "4");
    localStorage.setItem("locationId", "1");
    localStorage.setItem("currentBillId", "8");
    localStorage.setItem("menuCart", "{}");
    localStorage.setItem("guest", "true");
    api.get.mockImplementation(notFound);

    await expect(fetchMySessionId()).resolves.toBeNull();

    expect(ended).toHaveBeenCalledTimes(1);
    ["session_id", "tableNumber", "locationId", "currentBillId", "menuCart"].forEach((key) =>
      expect(localStorage.getItem(key)).toBeNull(),
    );
    // Kept so the next scan reuses the same guest
    expect(localStorage.getItem("guest")).toBe("true");
  });

  it("doesn't thank someone who was never seated", async () => {
    api.get.mockImplementation(notFound);

    await expect(fetchMySessionId()).resolves.toBeNull();
    expect(ended).not.toHaveBeenCalled();
  });

  it("doesn't end the visit when the check itself fails", async () => {
    localStorage.setItem("session_id", "42");
    api.get.mockRejectedValue({ response: { status: 500 } });

    await expect(fetchMySessionId()).rejects.toBeDefined();
    expect(ended).not.toHaveBeenCalled();
    expect(localStorage.getItem("session_id")).toBe("42");
  });
});
