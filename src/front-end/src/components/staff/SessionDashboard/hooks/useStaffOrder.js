import { useCallback, useEffect, useMemo, useState } from "react";
import api from "@/config/api";
import orderService from "@/services/orderService";
import {
  buildCartLines,
  cartSubtotal,
  changeCartQuantity,
  groupItemsByCategory,
  limitsById,
  maxFor,
  normalizeCategory,
  orderItemsFor,
} from "@/components/menu/cartRules";

// The session's menu, its limits and a cart, for staff ordering on a table's bill.
// Same limits as the guests' menu (the server checks them either way).
// Loads when `session` is set, starting with an empty cart each time
export const useStaffOrder = (session) => {
  const sessionId = session?.session_id;
  const menuId = session?.menu_id;
  const locationId = session?.location_id;

  const [categories, setCategories] = useState([]);
  const [menuItems, setMenuItems] = useState({});
  // null until loaded (or if it failed)
  const [limits, setLimits] = useState(null);
  const [quantities, setQuantities] = useState({});
  const [isLoading, setIsLoading] = useState(false);
  const [loadError, setLoadError] = useState(null);

  useEffect(() => {
    if (!sessionId || !menuId) return;
    let cancelled = false;

    setQuantities({});
    setIsLoading(true);
    setLoadError(null);

    Promise.all([
      api.get("/Category"),
      api.get(`/Menu/${menuId}/item`, { params: locationId ? { location_id: locationId } : {} }),
      // Without limits the cart doesn't stop early, but the server still checks
      api.get(`/Order/session/${sessionId}/limits`).catch(() => null),
    ])
      .then(([categoriesRes, itemsRes, limitsRes]) => {
        if (cancelled) return;
        setCategories((Array.isArray(categoriesRes.data) ? categoriesRes.data : []).map(normalizeCategory));
        setMenuItems(groupItemsByCategory(Array.isArray(itemsRes.data) ? itemsRes.data : []));
        setLimits(limitsRes ? limitsById(limitsRes.data) : null);
      })
      .catch((err) => {
        console.error("Error loading the menu for staff ordering:", err);
        if (!cancelled) setLoadError("Couldn't load the menu. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [sessionId, menuId, locationId]);

  const changeQuantity = useCallback(
    (categoryId, itemId, delta) =>
      setQuantities((prev) => changeCartQuantity(limits, prev, categoryId, itemId, delta)),
    [limits],
  );

  const atLimit = useCallback(
    (categoryId, itemId) =>
      (quantities[categoryId]?.[itemId] || 0) >= maxFor(limits, quantities, categoryId, itemId),
    [limits, quantities],
  );

  const cartLines = useMemo(
    () => buildCartLines({ categories, quantities, menuItems, limits }),
    [categories, quantities, menuItems, limits],
  );

  const placeOrder = (billId) =>
    orderService.createOrder({
      session_id: sessionId,
      bill_id: billId,
      items: orderItemsFor(cartLines, menuId),
    });

  return {
    categories,
    menuItems,
    isLoading,
    loadError,
    quantities,
    changeQuantity,
    atLimit,
    cartLines,
    unitCount: cartLines.reduce((total, { quantity }) => total + quantity, 0),
    subtotal: cartSubtotal(cartLines),
    placeOrder,
  };
};
