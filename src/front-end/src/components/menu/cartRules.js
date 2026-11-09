// Cart rules shared by the guest menu (TableOrderContext) and staff ordering for a table,
// so both stop at the same limits and show the same over-limit charges

// The server takes 1-99 of an item per order (OrderController.AddOrderItems)
export const MAX_PER_ITEM = 99;

// Rename the fields from /Menu/{id}/item so the item looks the same everywhere
export const normalizeItem = (it) => ({
  item_id: it.itemId,
  name: it.name,
  description: it.description,
  price: it.price,
  is_add_on: it.isAddOn,
  item_image_url: it.imageURL,
  category_id: it.categoryID,
  category: it.category,
  tags: it.tags ?? [],
  categoryLimits: it.categoryLimits,
  itemLimits: it.itemLimits,
});

// A row from /Category
export const normalizeCategory = (c) => ({
  category_id: c.category_id,
  name: c.category_name,
});

// Menu items from /Menu/{id}/item, one list per category
export const groupItemsByCategory = (items) =>
  items.map(normalizeItem).reduce((acc, item) => {
    (acc[item.category_id] ||= []).push(item);
    return acc;
  }, {});

// /Order/session/{id}/limits as { items: {id: row}, categories: {id: row} }
export const limitsById = (data) => {
  const byId = (rows, key) =>
    Object.fromEntries(
      (Array.isArray(rows) ? rows : []).map((row) => [String(row[key]), row]),
    );
  return {
    items: byId(data?.items, "item_id"),
    categories: byId(data?.categories, "category_id"),
  };
};

// Most of this item the cart can hold before a limit that blocks: the item's own caps, and
// whatever its category has left after the other items from that category already in the cart.
// Never more than the server takes in one order, even for items with no limit
export const maxFor = (limits, cart, categoryId, itemId) => {
  if (!limits) return MAX_PER_ITEM;
  const lowest = (row) => {
    const caps = [row?.per_order, row?.session_left].filter((n) => n != null);
    return caps.length ? Math.min(...caps) : null;
  };

  const itemCap = lowest(limits.items[String(itemId)]);
  const categoryCap = lowest(limits.categories[String(categoryId)]);
  const othersInCategory = Object.entries(cart[categoryId] || {})
    .filter(([id]) => String(id) !== String(itemId))
    .reduce((total, [, quantity]) => total + quantity, 0);
  const categoryRoom =
    categoryCap == null ? null : Math.max(0, categoryCap - othersInCategory);

  const caps = [itemCap, categoryRoom].filter((n) => n != null);
  return Math.min(MAX_PER_ITEM, ...caps);
};

// The cart with one more (or fewer) of an item; adding stops at the limit, removing always works
export const changeCartQuantity = (limits, cart, categoryId, itemId, delta) => {
  const current = cart[categoryId]?.[itemId] || 0;
  const max = maxFor(limits, cart, categoryId, itemId);
  if (delta > 0 && max != null && current + delta > max) return cart;
  return {
    ...cart,
    [categoryId]: {
      ...cart[categoryId],
      [itemId]: Math.max(0, current + delta),
    },
  };
};

// Every item in the cart with its menu details, in menu order (the order it's sent in).
// extra: units past a Charge limit (e.g. Hot Stone past 1 per person), billed at extraPrice each.
// Worked out like the server does: each item uses up its category's free units in turn
export const buildCartLines = ({ categories, quantities, menuItems, limits }) => {
  const categoryFree = {};
  return categories.flatMap((category) =>
    Object.entries(quantities[category.category_id] || {})
      .filter(([, quantity]) => quantity > 0)
      .map(([itemId, quantity]) => ({
        category,
        quantity,
        item: menuItems[category.category_id]?.find(
          (menuItem) => menuItem.item_id === Number(itemId),
        ),
      }))
      .filter(({ item }) => item)
      .map((line) => {
        const itemRow = limits?.items[String(line.item.item_id)];
        const key = String(category.category_id);
        if (!(key in categoryFree))
          categoryFree[key] = limits?.categories[key]?.free_left ?? null;

        const frees = [itemRow?.free_left, categoryFree[key]].filter(
          (n) => n != null,
        );
        const free = Math.min(line.quantity, ...frees);
        if (categoryFree[key] != null)
          categoryFree[key] = Math.max(0, categoryFree[key] - line.quantity);

        const extra = line.item.is_add_on ? 0 : line.quantity - free;
        return {
          ...line,
          extra,
          extraPrice: extra > 0 ? Number(itemRow?.extra_price) || 0 : 0,
        };
      }),
  );
};

// What the cart costs on top of the buffet: add-ons, plus anything past a limit that charges
export const cartSubtotal = (cartLines) =>
  cartLines.reduce(
    (total, { item, quantity, extra, extraPrice }) =>
      total +
      (item.is_add_on ? (Number(item.price) || 0) * quantity : 0) +
      extra * extraPrice,
    0,
  );

// The items to send with POST /Order. Only OrderItemCreateDto's keys: in development the
// API rejects a request with any other key
export const orderItemsFor = (cartLines, menuId) =>
  cartLines.map(({ item, quantity }) => ({
    menu_id: Number(menuId),
    item_id: Number(item.item_id),
    quantity: Number(quantity),
    price_at_time: item.is_add_on ? Number(item.price) || 0 : 0,
  }));
