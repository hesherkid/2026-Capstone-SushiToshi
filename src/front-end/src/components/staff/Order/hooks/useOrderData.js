import { useState, useCallback, useEffect, useMemo } from "react";
import api from "@/config/api";
import storage from "@/utils/storage";

const POLL_INTERVAL_MS = 30000;

export const ORDER_STATUS = {
  PENDING: "PENDING",
  PROCESSING: "PROCESSING",
  DELIVERED: "DELIVERED",
  CANCELLED: "CANCELLED",
};

// The API reports approved orders as "Approved" or "Processing"; both are being prepared
export const normalizeStatus = (status) => {
  const upper = String(status || "").toUpperCase();
  return upper === "APPROVED" ? ORDER_STATUS.PROCESSING : upper;
};

const mapOrder = (order) => {
  const items = Array.isArray(order.items)
    ? order.items.map((item) => ({
        orderItemId: item.orderItemId,
        itemId: item.itemId,
        name: item.name,
        quantity: item.quantity,
        price: item.priceAtTime,
        isAddOn: item.isAddOn,
        status: item.status,
      }))
    : [];

  return {
    orderId: order.orderId,
    sessionId: order.sessionId,
    billId: order.billId,
    userName: order.userName,
    status: normalizeStatus(order.status),
    createdAt: order.createdAt,
    completedAt: order.completedAt,
    tableNumbers: order.tableNumbers || [],
    items,
    itemCount: items.reduce((sum, item) => sum + (item.quantity || 0), 0),
    orderTotal: items.reduce((sum, item) => sum + item.price * item.quantity, 0),
  };
};

// Orders for the selected location, refreshed every 30s unless `paused`
export const useOrderData = ({ paused = false } = {}) => {
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);
  const [orders, setOrders] = useState([]);

  // `silent` skips the loading overlay so background polls don't flash the grid
  const fetchOrders = useCallback(async ({ silent = false } = {}) => {
    if (!silent) setIsLoading(true);
    try {
      const locationId = storage.get("branch-location");
      const params = locationId ? { locationId } : {};
      const response = await api.get("/Order/all_orders", { params });
      setOrders(Array.isArray(response.data) ? response.data.map(mapOrder) : []);
      setError(null);
    } catch (err) {
      console.error("Error fetching orders:", err);
      setError("Failed to load orders");
    } finally {
      setIsLoading(false);
    }
  }, []);

  // Pause polling while staff are editing so the open order isn't replaced mid-edit
  useEffect(() => {
    if (paused) return;
    fetchOrders({ silent: true });
    const interval = setInterval(
      () => fetchOrders({ silent: true }),
      POLL_INTERVAL_MS
    );
    return () => clearInterval(interval);
  }, [paused, fetchOrders]);

  const orderCounts = useMemo(() => {
    const count = (status) => orders.filter((o) => o.status === status).length;
    return {
      [ORDER_STATUS.PENDING]: count(ORDER_STATUS.PENDING),
      [ORDER_STATUS.PROCESSING]: count(ORDER_STATUS.PROCESSING),
      [ORDER_STATUS.DELIVERED]: count(ORDER_STATUS.DELIVERED),
    };
  }, [orders]);

  return {
    isLoading,
    error,
    setError,
    orders,
    orderCounts,
    fetchOrders,
  };
};
