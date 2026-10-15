"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import api from "@/config/api";
import storage from "@/utils/storage";

const POLL_INTERVAL_MS = 10000;
const OPEN_ORDER_STATUSES = new Set(["Pending", "Processing"]);

// Ids in `items` that weren't in the previous poll (none on the first load)
const useNewIds = (getId) => {
  const seen = useRef(null);
  const [newIds, setNewIds] = useState(new Set());

  const track = useCallback(
    (items) => {
      const ids = items.map(getId);
      if (seen.current) {
        setNewIds(new Set(ids.filter((id) => !seen.current.has(id))));
      }
      seen.current = new Set(ids);
    },
    [getId]
  );

  return [newIds, track];
};

const getRequestId = (request) => request.request_id;
const getOrderId = (order) => order.orderId;

/**
 * Live data for the staff/admin home pages: active dining sessions at the
 * user's location, how many tables they cover, and pending service requests.
 * With `includeOrders`, also open (pending/processing) kitchen orders.
 */
export default function useRestaurantOverview({ includeOrders = false } = {}) {
  const [sessions, setSessions] = useState([]);
  const [requests, setRequests] = useState([]);
  const [orders, setOrders] = useState([]);
  const [newRequestIds, trackRequests] = useNewIds(getRequestId);
  const [newOrderIds, trackOrders] = useNewIds(getOrderId);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const refresh = useCallback(async () => {
    try {
      // Same location the staff Orders page uses (app bar location picker)
      const locationId = storage.get("branch-location");

      const [sessionsResponse, requestsResponse, ordersResponse] = await Promise.all([
        api.get("/DiningSession/get_list_dining_sessions", {
          params: { ActiveOnly: true },
        }),
        api.get("/ServiceRequest/pending"),
        includeOrders
          ? api.get("/Order/all_orders", { params: locationId ? { locationId } : {} })
          : Promise.resolve({ data: [] }),
      ]);

      const activeSessions = Array.isArray(sessionsResponse.data)
        ? sessionsResponse.data
        : [];
      // Sessions are already limited to the user's location; keep only requests from them
      const sessionIds = new Set(activeSessions.map((s) => s.session_Id));
      const pending = (Array.isArray(requestsResponse.data) ? requestsResponse.data : [])
        .filter((request) => sessionIds.has(request.session_id));

      // Oldest first, so the order that has waited longest is at the top
      const openOrders = (Array.isArray(ordersResponse.data) ? ordersResponse.data : [])
        .filter((order) => OPEN_ORDER_STATUSES.has(order.status))
        .filter((order) => locationId || sessionIds.has(order.sessionId))
        .sort((a, b) => new Date(a.createdAt) - new Date(b.createdAt));

      trackRequests(pending);
      trackOrders(openOrders);
      setSessions(activeSessions);
      setRequests(pending);
      setOrders(openOrders);
      setError(null);
    } catch (err) {
      setError("Couldn't load live restaurant data. Retrying…");
    } finally {
      setLoading(false);
    }
  }, [includeOrders, trackRequests, trackOrders]);

  useEffect(() => {
    refresh();
    const interval = setInterval(() => {
      if (!document.hidden) refresh();
    }, POLL_INTERVAL_MS);
    return () => clearInterval(interval);
  }, [refresh]);

  const activeTables = sessions.reduce(
    (total, session) => total + (session.table_Numbers?.length || 0),
    0
  );

  return {
    sessions,
    requests,
    newRequestIds,
    orders,
    newOrderIds,
    activeTables,
    loading,
    error,
    refresh,
  };
}
