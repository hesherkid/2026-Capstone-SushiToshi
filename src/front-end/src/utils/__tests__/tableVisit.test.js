import { startTableVisit, TABLE_VISIT_EVENT } from "@/utils/tableVisit";

describe("startTableVisit", () => {
  beforeEach(() => localStorage.clear());

  it("saves the scanned table and forgets the last visit's session and bill", () => {
    localStorage.setItem("tableNumber", "18");
    localStorage.setItem("session_id", "3");
    localStorage.setItem("currentBillId", "40");
    const listener = jest.fn();
    window.addEventListener(TABLE_VISIT_EVENT, listener);

    startTableVisit({ locationId: "1", tableNumber: "3" });

    expect(localStorage.getItem("tableNumber")).toBe("3");
    expect(localStorage.getItem("locationId")).toBe("1");
    expect(localStorage.getItem("session_id")).toBeNull();
    expect(localStorage.getItem("currentBillId")).toBeNull();
    expect(listener).toHaveBeenCalledTimes(1);
    window.removeEventListener(TABLE_VISIT_EVENT, listener);
  });

  it("does nothing without full QR details", () => {
    localStorage.setItem("session_id", "3");

    startTableVisit({ locationId: "1", tableNumber: null });

    expect(localStorage.getItem("session_id")).toBe("3");
  });
});
