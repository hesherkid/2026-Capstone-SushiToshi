"use client";
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import api from "@/config/api";
import { fetchMySessionId, NOT_SEATED_MESSAGE } from "@/services/tableSession";
import storage from "@/utils/storage";

// localStorage key for the items picked but not yet ordered
const CART_STORAGE_KEY = "menuCart";

export const MENU_PATH = "/menu/full-menu";
export const REVIEW_ORDER_PATH = "/menu/review";

export const BILL_REQUESTED_MESSAGE =
  "You've asked for your bill, so no more orders can be added.";

// Clean up the field names so the item looks the same everywhere
const normalizeItem = (it) => ({
  item_id: it.item_id ?? it.itemId ?? it.Item_Id,
  name: it.name,
  description: it.description,
  price: it.price,
  is_add_on: it.is_add_on ?? it.isAddOn,
  item_image_url: it.item_image_url ?? it.imageURL ?? it.imageUrl,
  category_id: it.categoryID ?? it.categoryId ?? it.category_id,
  category: it.category,
  tags: it.tags ?? [],
  categoryLimits: it.categoryLimits,
  itemLimits: it.itemLimits,
});

const apiMessage = (err, fallback) =>
  err?.response?.data?.detail ??
  err?.response?.data?.message ??
  err?.message ??
  fallback;

const TableOrderContext = createContext(null);

/**
 * The guest's table: which session they're in, its menu and bills, and the cart.
 * Shared by the menu and Review order pages, so going between them keeps the cart and
 * shows the menu straight away. Nothing loads until a customer page calls refresh(),
 * which re-checks the table and bills each time but keeps the menu it already has.
 */
export const TableOrderProvider = ({ children }) => {
  const [sessionId, setSessionId] = useState(null);
  // False until the server says which table (if any) this person is at
  const [sessionChecked, setSessionChecked] = useState(false);
  const [error, setError] = useState(null);
  const [categories, setCategories] = useState([]);
  const [menuItems, setMenuItems] = useState({});
  // False until the menu's items have been fetched (or the fetch failed)
  const [itemsLoaded, setItemsLoaded] = useState(false);
  const [menuId, setMenuId] = useState("");
  const [locationId, setLocationId] = useState(null);
  // The table's open bills, newest first; the orderer picks which one this order goes on
  const [openBills, setOpenBills] = useState([]);
  const [selectedBillId, setSelectedBillId] = useState("");
  const [quantities, setQuantities] = useState({});
  // What the table can still order before a limit that blocks: { items: {id: ...}, categories: {id: ...} }.
  // null until loaded (or if it failed); the server still checks every order either way
  const [limits, setLimits] = useState(null);
  // Where the guest was on the menu, so coming back from Review order puts them there again
  const menuScrollYRef = useRef(0);
  // True when Review order was opened from the menu, so its back arrow can just go back
  const reviewOpenedFromMenuRef = useRef(false);

  const currentSession = useRef(null);
  const categoriesLoaded = useRef(false);
  const cartRestoredFor = useRef(null);

  const chooseBill = useCallback((billId) => {
    setSelectedBillId(billId);
    storage.set("currentBillId", String(billId));
  }, []);

  const loadBills = useCallback(
    async (forSession) => {
      try {
        const response = await api.get(`/Bill/get_bills/${forSession}`);
        const bills = Array.isArray(response.data) ? response.data : [];

        // Only open bills can take orders; newest first
        const open = bills
          .filter((bill) => String(bill.status ?? "").toUpperCase() === "OPEN")
          .map((bill) => ({
            bill_id: Number(bill.bill_id),
            bill_name: bill.bill_name,
            requested_at: bill.requested_at,
            created_at: bill.created_at,
          }))
          .sort((a, b) => new Date(b.created_at) - new Date(a.created_at));
        setOpenBills(open);

        // Keep the bill this phone ordered on last, so a guest doesn't drift onto another
        // party's bill; otherwise the newest bill that can still take orders
        const lastUsed = Number(storage.get("currentBillId"));
        const current =
          open.find((bill) => bill.bill_id === lastUsed) ??
          open.find((bill) => !bill.requested_at) ??
          open[0];

        if (current) {
          chooseBill(current.bill_id);
        } else {
          setSelectedBillId("");
        }
      } catch (err) {
        console.error("Error fetching current bill:", err);
      }
    },
    [chooseBill],
  );

  const loadMenu = useCallback(async (forSession) => {
    try {
      const response = await api.get(
        `/DiningSession/session-menu/${forSession}`,
      );
      if (
        !(response?.status >= 200 && response.status < 300 && response.data)
      ) {
        console.warn("No menu found", response?.data);
        setError("No menu found for this session");
        return;
      }

      const raw = response.data;
      const id =
        raw.menu_id ??
        raw.menuId ??
        raw.Menu_Id ??
        raw.menu_Id ??
        raw.MenuID ??
        raw;
      const foundMenuId =
        typeof id === "number" ? id : parseInt(String(id), 10);

      if (Number.isNaN(foundMenuId) || foundMenuId < 1) {
        console.warn("Menu ID is invalid:", raw);
        setError("No menu for this session");
        return;
      }

      // Location decides which prices this table sees
      const loc = Number(
        raw.location_Id ?? raw.location_id ?? raw.locationId ?? raw.Location_Id,
      );
      setLocationId(Number.isNaN(loc) || loc < 1 ? null : loc);
      setMenuId(foundMenuId);
    } catch (err) {
      console.error("Error fetching menu:", err);
      setError(apiMessage(err, "Error fetching menu"));
    }
  }, []);

  const loadLimits = useCallback(async (forSession) => {
    try {
      const { data } = await api.get(`/Order/session/${forSession}/limits`);
      const byId = (rows, key) =>
        Object.fromEntries(
          (Array.isArray(rows) ? rows : []).map((row) => [
            String(row[key]),
            row,
          ]),
        );
      setLimits({
        items: byId(data?.items, "item_id"),
        categories: byId(data?.categories, "category_id"),
      });
    } catch (err) {
      console.error("Error fetching order limits:", err);
      setLimits(null);
    }
  }, []);

  const loadCategories = useCallback(async () => {
    if (categoriesLoaded.current) return;
    try {
      const response = await api.get("/Category");
      const data = Array.isArray(response.data) ? response.data : [];
      setCategories(
        data.map((c) => ({
          category_id: c.category_id ?? c.Category_id,
          name: c.name ?? c.category_name ?? c.Category_name,
        })),
      );
      categoriesLoaded.current = true;
    } catch (err) {
      console.error("Error fetching categories:", err);
      setError(apiMessage(err, "Error fetching categories"));
    }
  }, []);

  const refresh = useCallback(async () => {
    setError(null);
    loadCategories();

    let mySessionId;
    try {
      // Ask the server (guests included) rather than trusting a saved session_id
      mySessionId = await fetchMySessionId();
    } catch (err) {
      console.error("Error fetching session:", err);
      setError(err.response?.data?.detail || "Error fetching session");
      setSessionChecked(true);
      return;
    }

    // A different table (or a new guest on this phone): drop everything from the old one
    if (mySessionId !== currentSession.current) {
      currentSession.current = mySessionId;
      setMenuItems({});
      setItemsLoaded(false);
      setMenuId("");
      setLocationId(null);
      setOpenBills([]);
      setSelectedBillId("");
      setLimits(null);
      menuScrollYRef.current = 0;
    }

    setSessionId(mySessionId);
    setSessionChecked(true);

    if (!mySessionId) {
      setError(NOT_SEATED_MESSAGE);
      return;
    }

    await Promise.all([
      loadBills(mySessionId),
      loadMenu(mySessionId),
      loadLimits(mySessionId),
    ]);
  }, [loadBills, loadCategories, loadLimits, loadMenu]);

  // Menu items for the session's menu, priced for its location
  useEffect(() => {
    if (!menuId) return;

    const loadItems = async () => {
      try {
        const response = await api.get(`/Menu/${menuId}/item`, {
          params: locationId ? { locationId } : {},
        });
        const items = Array.isArray(response.data) ? response.data : [];

        // Each category gets its own list
        const grouped = items.map(normalizeItem).reduce((acc, item) => {
          (acc[item.category_id] ||= []).push(item);
          return acc;
        }, {});

        setMenuItems(grouped);
      } catch (err) {
        console.error(`Error fetching menu items for menu ${menuId}:`, err);
      } finally {
        setItemsLoaded(true);
      }
    };

    loadItems();
  }, [menuId, locationId]);

  // The cart survives a page refresh. It's saved with its session so a cart from an
  // earlier visit is never restored onto a different table.
  useEffect(() => {
    if (!sessionId || cartRestoredFor.current === sessionId) return;
    cartRestoredFor.current = sessionId;
    try {
      const saved = JSON.parse(
        storage.get(CART_STORAGE_KEY) || "null",
      );
      setQuantities(
        saved?.sessionId === sessionId && saved.quantities
          ? saved.quantities
          : {},
      );
    } catch {
      storage.remove(CART_STORAGE_KEY);
      setQuantities({});
    }
  }, [sessionId]);

  useEffect(() => {
    // Wait until the saved cart has been read, or the empty starting cart would replace it
    if (!sessionId || cartRestoredFor.current !== sessionId) return;
    storage.set(
      CART_STORAGE_KEY,
      JSON.stringify({ sessionId, quantities }),
    );
  }, [sessionId, quantities]);

  // Most of this item the cart can hold before a limit that blocks: the item's own caps, and
  // whatever its category has left after the other items from that category already in the cart.
  // null = no limit that blocks
  const maxFor = useCallback(
    (cart, categoryId, itemId) => {
      if (!limits) return null;
      const lowest = (row) => {
        const caps = [row?.per_order, row?.session_left].filter(
          (n) => n != null,
        );
        return caps.length ? Math.min(...caps) : null;
      };

      const itemCap = lowest(limits.items[String(itemId)]);
      const categoryCap = lowest(limits.categories[String(categoryId)]);
      const othersInCategory = Object.entries(cart[categoryId] || {})
        .filter(([id]) => String(id) !== String(itemId))
        .reduce((total, [, quantity]) => total + quantity, 0);
      const categoryRoom =
        categoryCap == null
          ? null
          : Math.max(0, categoryCap - othersInCategory);

      const caps = [itemCap, categoryRoom].filter((n) => n != null);
      return caps.length ? Math.min(...caps) : null;
    },
    [limits],
  );

  // How the cart stands against the limit for one item
  const limitStatus = useCallback(
    (categoryId, itemId) => {
      const max = maxFor(quantities, categoryId, itemId);
      const quantity = quantities[categoryId]?.[itemId] || 0;
      return {
        max,
        atLimit: max != null && quantity >= max,
        overLimit: max != null && quantity > max,
      };
    },
    [maxFor, quantities],
  );

  // Adding stops at the limit (the + button is hidden there too); removing works
  const changeQuantity = useCallback(
    (categoryId, itemId, delta) => {
      setQuantities((prev) => {
        const current = prev[categoryId]?.[itemId] || 0;
        const max = maxFor(prev, categoryId, itemId);
        if (delta > 0 && max != null && current + delta > max) return prev;
        return {
          ...prev,
          [categoryId]: {
            ...prev[categoryId],
            [itemId]: Math.max(0, current + delta),
          },
        };
      });
    },
    [maxFor],
  );

  const clearCart = useCallback(() => setQuantities({}), []);

  // Every item in the cart with its menu details, in menu order
  const cartLines = useMemo(
    () =>
      categories.flatMap((category) =>
        Object.entries(quantities[category.category_id] || {})
          .filter(([, quantity]) => quantity > 0)
          .map(([itemId, quantity]) => ({
            category,
            quantity,
            item: menuItems[category.category_id]?.find(
              (menuItem) => menuItem.item_id === Number(itemId),
            ),
          }))
          .filter(({ item }) => item),
      ),
    [categories, quantities, menuItems],
  );

  // Total units picked (2 Salmon + 1 Tuna = 3), counted from the saved cart so it's
  // right even before the menu has loaded
  const cartCount = useMemo(
    () =>
      Object.values(quantities).reduce(
        (total, items) =>
          total +
          Object.values(items).reduce((sum, quantity) => sum + quantity, 0),
        0,
      ),
    [quantities],
  );

  // The guests pressed "Ready for Bill" on this bill, so the server won't take more orders on it
  const billRequested = Boolean(
    openBills.find((bill) => bill.bill_id === selectedBillId)?.requested_at,
  );

  const value = {
    sessionId,
    sessionChecked,
    error,
    setError,
    refresh,
    categories,
    menuItems,
    itemsLoaded,
    menuId,
    openBills,
    selectedBillId,
    chooseBill,
    billRequested,
    quantities,
    changeQuantity,
    limitStatus,
    clearCart,
    cartLines,
    cartCount,
    menuScrollYRef,
    reviewOpenedFromMenuRef,
  };

  return (
    <TableOrderContext.Provider value={value}>
      {children}
    </TableOrderContext.Provider>
  );
};

export const useTableOrder = () => {
  const context = useContext(TableOrderContext);
  if (!context)
    throw new Error("useTableOrder must be used inside TableOrderProvider");
  return context;
};
