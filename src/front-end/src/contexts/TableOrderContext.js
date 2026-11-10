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
import {
  buildCartLines,
  changeCartQuantity,
  groupItemsByCategory,
  limitsById,
  maxFor as cartMaxFor,
  normalizeCategory,
} from "@/components/menu/cartRules";

// localStorage key for the items picked but not yet ordered
const CART_STORAGE_KEY = "menuCart";

export const MENU_PATH = "/menu/full-menu";
export const REVIEW_ORDER_PATH = "/menu/review";

export const BILL_REQUESTED_MESSAGE =
  "You've asked for your bill, so no more orders can be added.";

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
  // The table's open bills (names and party size only) and the one this guest is on.
  // The server decides which bill is theirs; guests only see and order on that one
  const [openBills, setOpenBills] = useState([]);
  const [myBill, setMyBill] = useState(null);
  // False until the server says which bill (if any) this guest is on
  const [billChecked, setBillChecked] = useState(false);
  // Why they can't move themselves to another bill (staff can), or null
  const [billChangeBlocked, setBillChangeBlocked] = useState(null);
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

  // Returns the guest's own bill, now that it's saved
  const applyMyBill = useCallback((data) => {
    const toBill = (bill) => ({
      bill_id: Number(bill.bill_id),
      bill_name: bill.bill_name,
      total_count: bill.total_count,
      // How many people at the table are on it
      people_count: bill.people_count ?? 0,
      status: bill.status,
      requested_at: bill.requested_at,
    });
    // Only the guest's own bill comes with its counts by kind of guest
    const mine = data?.my_bill
      ? {
          ...toBill(data.my_bill),
          adult_count: data.my_bill.adult_count ?? 0,
          senior_count: data.my_bill.senior_count ?? 0,
          child_count: data.my_bill.child_count ?? 0,
          tot_count: data.my_bill.tot_count ?? 0,
          // Staff checked the counts and locked them; only staff can change them now
          counts_locked: Boolean(data.my_bill.counts_locked),
        }
      : null;
    setOpenBills((data?.open_bills ?? []).map(toBill));
    setMyBill(mine);
    setBillChangeBlocked(data?.change_blocked_reason ?? null);
    setBillChecked(true);
    return mine;
  }, []);

  const loadBills = useCallback(
    async (forSession) => {
      try {
        const { data } = await api.get(`/SessionParticipant/${forSession}/bill`);
        applyMyBill(data);
      } catch (err) {
        console.error("Error fetching your bill:", err);
      }
    },
    [applyMyBill],
  );

  // Join one of the table's open bills and return it. Throws with the server's reason if it can't
  const chooseBill = useCallback(
    async (billId) => {
      try {
        const { data } = await api.put(`/SessionParticipant/${sessionId}/bill`, {
          bill_id: billId,
        });
        return applyMyBill(data);
      } catch (err) {
        throw new Error(apiMessage(err, "Couldn't put you on that bill."));
      }
    },
    [sessionId, applyMyBill],
  );

  // Change the guest counts on this guest's own bill
  const updateGuestCounts = useCallback(
    async ({ adults, seniors, children, toddlers }) => {
      try {
        const { data } = await api.put(`/SessionParticipant/${sessionId}/bill/guests`, {
          adult_count: adults,
          senior_count: seniors,
          child_count: children,
          tot_count: toddlers,
        });
        applyMyBill(data);
      } catch (err) {
        throw new Error(apiMessage(err, "Couldn't update your guest count."));
      }
    },
    [sessionId, applyMyBill],
  );

  // Start a new bill for this guest's party; they're put on it
  const startBill = useCallback(
    async ({ billName, adults, seniors, children, toddlers }) => {
      try {
        await api.post(`/Bill/create_Bill/${sessionId}`, {
          bill_name: billName.trim(),
          adult_count: adults,
          senior_count: seniors,
          child_count: children,
          tot_count: toddlers,
        });
      } catch (err) {
        throw new Error(apiMessage(err, "Couldn't start your bill."));
      }
      await loadBills(sessionId);
    },
    [sessionId, loadBills],
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
      const foundMenuId = Number(raw.menu_id);

      if (Number.isNaN(foundMenuId) || foundMenuId < 1) {
        console.warn("Menu ID is invalid:", raw);
        setError("No menu for this session");
        return;
      }

      // Location decides which prices this table sees
      const loc = Number(raw.location_id);
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
      setLimits(limitsById(data));
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
      setCategories(data.map(normalizeCategory));
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
      setMyBill(null);
      setBillChecked(false);
      setBillChangeBlocked(null);
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
          params: locationId ? { location_id: locationId } : {},
        });
        const items = Array.isArray(response.data) ? response.data : [];
        setMenuItems(groupItemsByCategory(items));
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

  // How the cart stands against the limit for one item
  const limitStatus = useCallback(
    (categoryId, itemId) => {
      const max = cartMaxFor(limits, quantities, categoryId, itemId);
      const quantity = quantities[categoryId]?.[itemId] || 0;
      return {
        max,
        atLimit: max != null && quantity >= max,
        overLimit: max != null && quantity > max,
      };
    },
    [limits, quantities],
  );

  // Adding stops at the limit (the + button is hidden there too); removing works
  const changeQuantity = useCallback(
    (categoryId, itemId, delta) =>
      setQuantities((prev) =>
        changeCartQuantity(limits, prev, categoryId, itemId, delta),
      ),
    [limits],
  );

  const clearCart = useCallback(() => setQuantities({}), []);

  // Every item in the cart with its menu details, in menu order (see buildCartLines)
  const cartLines = useMemo(
    () => buildCartLines({ categories, quantities, menuItems, limits }),
    [categories, quantities, menuItems, limits],
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

  // Orders go on the guest's own bill, while it's open
  const selectedBillId =
    myBill && String(myBill.status).toUpperCase() === "OPEN" ? myBill.bill_id : "";

  // The guests pressed "Ready for Bill" on this bill, so the server won't take more orders on it
  const billRequested = Boolean(selectedBillId && myBill?.requested_at);

  // Seated, but not on a bill yet: they pick their party's bill or start one before ordering
  const needsBill = Boolean(sessionId && billChecked && !myBill);

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
    myBill,
    selectedBillId,
    needsBill,
    billChangeBlocked,
    chooseBill,
    startBill,
    updateGuestCounts,
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
