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

// `bills` is what /Bill/get_bills/42 returns (none open by default);
// `limits` is what's left before a blocking limit (none by default)
export const tableApi =
  ({ bills = [], items = [MISO_SOUP, SALMON_ROLL], limits = { items: [], categories: [] } } = {}) =>
  (url) => {
    // The page asks the server which session the guest is seated in
    if (url === "/DiningSession/participants/active-session-id") {
      return Promise.resolve({ status: 200, data: { session_id: 42 } });
    }
    if (url.includes("/Bill/get_bills/42")) {
      return Promise.resolve({ status: 200, data: bills });
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
