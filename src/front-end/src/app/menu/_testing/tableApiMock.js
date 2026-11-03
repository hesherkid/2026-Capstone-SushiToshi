// Shared by the menu and Review order page tests: a guest seated at session 42,
// on menu 7 at location 3, with two items. The underscore keeps this folder out of the routes.
export const MISO_SOUP = {
  item_id: 11,
  name: "Miso Soup",
  description: "Classic miso broth",
  price: 6,
  category_id: 1,
  category: "Appetizers",
  tags: [{ tag_id: 3, name: "Popular", color_code: "#C01E2E" }],
  item_image_url: "",
};

export const SALMON_ROLL = {
  item_id: 12,
  name: "Salmon Roll",
  description: "Salmon and rice",
  price: 8,
  category_id: 2,
  category: "Sushi Rolls",
  tags: [],
  item_image_url: "",
};

// What GET /SessionParticipant/42/bill returns: the guest's bill and the table's open bills
export const myBillResponse = ({ bills = [], myBillId, blocked = null } = {}) => ({
  my_bill: bills.find((bill) => bill.bill_id === myBillId) ?? null,
  open_bills: bills.filter((bill) => String(bill.status ?? "Open").toUpperCase() === "OPEN"),
  change_blocked_reason: blocked,
});

// The guest's bill unless a test says otherwise
export const LEE_BILL = {
  bill_id: 8,
  bill_name: "Lee",
  status: "Open",
  total_count: 2,
  adult_count: 2,
  senior_count: 0,
  child_count: 0,
  tot_count: 0,
  people_count: 1,
  requested_at: null,
};

// `bills` are the table's bills (just Lee's by default). The guest is on `myBillId`, which is
// the first bill unless a test says otherwise (null: not on a bill yet).
// `limits` is what's left before a blocking limit (none by default)
export const tableApi =
  ({
    bills = [LEE_BILL],
    myBillId = bills[0]?.bill_id ?? null,
    blocked = null,
    items = [MISO_SOUP, SALMON_ROLL],
    limits = { items: [], categories: [] },
  } = {}) =>
  (url) => {
    // The page asks the server which session the guest is seated in
    if (url === "/DiningSession/participants/active-session-id") {
      return Promise.resolve({ status: 200, data: { session_id: 42 } });
    }
    if (url === "/SessionParticipant/42/bill") {
      return Promise.resolve({ status: 200, data: myBillResponse({ bills, myBillId, blocked }) });
    }
    if (url.includes("/DiningSession/session-menu/42")) {
      return Promise.resolve({ status: 200, data: { menu_id: 7, location_Id: 3 } });
    }
    if (url.includes("/Category")) {
      return Promise.resolve({
        status: 200,
        data: [
          { category_id: 1, category_name: "Appetizers" },
          { category_id: 2, category_name: "Sushi Rolls" },
        ],
      });
    }
    if (url === "/Order/session/42/limits") {
      return Promise.resolve({ status: 200, data: limits });
    }
    if (url.includes("/Menu/7/item")) {
      return Promise.resolve({ status: 200, data: items });
    }
    return Promise.resolve({ data: [] });
  };
