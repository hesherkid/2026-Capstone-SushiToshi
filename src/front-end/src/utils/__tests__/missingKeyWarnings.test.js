import { warnOnMissingKeys, endpointLabel } from "../missingKeyWarnings";

// Each test uses its own label, because each endpoint and key only warns once
let label = 0;
const nextLabel = () => `GET /test/${++label}`;

describe("warnOnMissingKeys", () => {
  let warn;
  beforeEach(() => {
    warn = jest.spyOn(console, "warn").mockImplementation(() => {});
  });
  afterEach(() => warn.mockRestore());

  it("warns when a key isn't in the response", () => {
    const bill = warnOnMissingKeys({ bill_id: 1 }, nextLabel());

    expect(bill.bill_Id).toBeUndefined();
    expect(warn).toHaveBeenCalledWith(expect.stringContaining('no "bill_Id" key'));
  });

  it("doesn't warn for keys that are there, even when null", () => {
    const bill = warnOnMissingKeys({ bill_id: 1, closed_at: null }, nextLabel());

    expect(bill.bill_id).toBe(1);
    expect(bill.closed_at).toBeNull();
    expect(warn).not.toHaveBeenCalled();
  });

  it("warns for nested objects and items in lists", () => {
    const session = warnOnMissingKeys({ bills: [{ bill_id: 1 }], location: { name: "South" } }, nextLabel());

    session.bills.map((b) => b.bill_name);
    expect(session.location.city).toBeUndefined();
    expect(warn).toHaveBeenCalledTimes(2);
  });

  it("warns once per endpoint and key", () => {
    const l = nextLabel();
    warnOnMissingKeys({}, l).table_Id;
    warnOnMissingKeys({}, l).table_Id;

    expect(warn).toHaveBeenCalledTimes(1);
  });

  it("returns the same wrapper each time, so React sees the same value", () => {
    const session = warnOnMissingKeys({ bills: [] }, nextLabel());

    expect(session.bills).toBe(session.bills);
  });

  it("doesn't warn for an empty list's first item, promises or JSON.stringify", async () => {
    const data = warnOnMissingKeys({ items: [] }, nextLabel());

    expect(data.items[0]).toBeUndefined();
    expect(JSON.stringify(data)).toBe('{"items":[]}');
    await Promise.resolve(data);
    expect(warn).not.toHaveBeenCalled();
  });

  it("leaves strings, numbers and files alone", () => {
    const file = new Blob(["png"]);

    expect(warnOnMissingKeys("ok", nextLabel())).toBe("ok");
    expect(warnOnMissingKeys(5, nextLabel())).toBe(5);
    expect(warnOnMissingKeys(file, nextLabel())).toBe(file);
  });
});

describe("endpointLabel", () => {
  it("replaces ids, so each endpoint warns once", () => {
    expect(endpointLabel({ method: "get", url: "/Bill/get_bills/12?_table_id=3" })).toBe("GET /Bill/get_bills/:id");
    expect(endpointLabel({ method: "put", url: "/DiningSession/5/close" })).toBe("PUT /DiningSession/:id/close");
  });
});
