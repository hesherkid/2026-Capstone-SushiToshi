import { useState, useCallback, useMemo } from "react";
import api from "@/config/api";
import storage from "@/utils/storage";
import { mapOrder } from "./useOrderData";

export const MAX_HISTORY_RANGE_DAYS = 31;

const DAY_MS = 24 * 60 * 60 * 1000;

export const toDateInput = (date) => {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, "0");
  const d = String(date.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
};

const initialFilters = () => {
  const today = toDateInput(new Date());
  return { from: today, to: today, tableNumber: "", name: "" };
};

export const validateHistoryFilters = ({ from, to }) => {
  const errors = {};
  if (!from) errors.from = "Choose a start date.";
  if (!to) errors.to = "Choose an end date.";
  if (from && to) {
    if (from > to) {
      errors.from = "The start date must be on or before the end date.";
    } else {
      const days = (Date.parse(to) - Date.parse(from)) / DAY_MS + 1;
      if (days > MAX_HISTORY_RANGE_DAYS) {
        errors.to = `Choose a range of ${MAX_HISTORY_RANGE_DAYS} days or fewer.`;
      }
    }
  }
  return errors;
};

const byCreatedAt = (a, b) => new Date(a.createdAt) - new Date(b.createdAt);

// One entry per dining session, with its orders split by bill (oldest first)
export const groupOrdersBySession = (orders) => {
  const sessions = new Map();

  for (const order of [...orders].sort(byCreatedAt)) {
    if (!sessions.has(order.sessionId)) {
      sessions.set(order.sessionId, {
        sessionId: order.sessionId,
        tableNumbers: order.tableNumbers,
        firstOrderAt: order.createdAt,
        orderCount: 0,
        itemCount: 0,
        total: 0,
        bills: new Map(),
      });
    }
    const session = sessions.get(order.sessionId);
    session.orderCount += 1;
    session.itemCount += order.itemCount;
    session.total += order.orderTotal;

    if (!session.bills.has(order.billId)) {
      session.bills.set(order.billId, {
        billId: order.billId,
        billName: order.billName,
        orders: [],
        itemCount: 0,
        total: 0,
      });
    }
    const bill = session.bills.get(order.billId);
    bill.orders.push(order);
    bill.itemCount += order.itemCount;
    bill.total += order.orderTotal;
  }

  return [...sessions.values()].map((session) => {
    const bills = [...session.bills.values()];
    return {
      ...session,
      bills,
      billNames: bills.map((b) => b.billName || `Bill #${b.billId}`),
    };
  });
};

// Past orders for the selected location, fetched only when staff press Search (no polling)
export const useOrderHistory = () => {
  const [filters, setFilters] = useState(initialFilters);
  const [orders, setOrders] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState(null);
  const [fieldErrors, setFieldErrors] = useState({});
  const [hasSearched, setHasSearched] = useState(false);

  const setFilter = useCallback((key, value) => {
    setFilters((prev) => ({ ...prev, [key]: value }));
    setFieldErrors((prev) => ({ ...prev, [key]: undefined }));
  }, []);

  const search = useCallback(async () => {
    const clientErrors = validateHistoryFilters(filters);
    if (Object.keys(clientErrors).length > 0) {
      setFieldErrors(clientErrors);
      return;
    }

    setIsLoading(true);
    setError(null);
    setFieldErrors({});
    try {
      const params = { from: filters.from, to: filters.to };
      const locationId = storage.get("branch-location");
      if (locationId) params.locationId = locationId;
      if (filters.tableNumber !== "") params.tableNumber = filters.tableNumber;
      if (filters.name.trim()) params.name = filters.name.trim();

      const response = await api.get("/Order/history", { params });
      setOrders(
        Array.isArray(response.data) ? response.data.map(mapOrder) : [],
      );
    } catch (err) {
      const apiErrors =
        err.response?.status === 400 ? err.response.data?.errors : null;
      if (apiErrors) {
        setFieldErrors(
          Object.fromEntries(
            Object.entries(apiErrors).map(([key, messages]) => [
              key.toLowerCase(),
              Array.isArray(messages) ? messages[0] : String(messages),
            ]),
          ),
        );
      } else {
        console.error("Error fetching order history:", err);
        setError("Failed to load order history");
      }
      setOrders([]);
    } finally {
      setIsLoading(false);
      setHasSearched(true);
    }
  }, [filters]);

  const sessions = useMemo(() => groupOrdersBySession(orders), [orders]);

  return {
    filters,
    setFilter,
    orders,
    sessions,
    isLoading,
    error,
    setError,
    fieldErrors,
    hasSearched,
    search,
  };
};
