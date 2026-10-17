import { useState, useCallback, useEffect, useMemo } from "react";
import api from "@/config/api";
import storage from "@/utils/storage";

const POLL_INTERVAL_MS = 10000;

const NO_LOCATION_ERROR =
  "Invalid location. Make sure you have selected a location in the navigation bar dropdown at the top of the page.";

// Active sessions and their pending service requests for the selected location.
// With `live`, both refresh every 10s while the tab is visible.
export const useSessionData = ({ live = false } = {}) => {
  const [sessions, setSessions] = useState([]);
  const [requests, setRequests] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);

  // `silent` skips the loading overlay so background polls don't flash the grid
  const fetchSessions = useCallback(async ({ silent = false } = {}) => {
    const locationId = storage.get("branch-location");
    if (!locationId) {
      if (localStorage.getItem("guest") !== "true") setError(NO_LOCATION_ERROR);
      setIsLoading(false);
      return;
    }

    if (!silent) setIsLoading(true);
    try {
      const [sessionsRes, requestsRes] = await Promise.all([
        api.get(`/Dashboard/sessions?locationId=${locationId}`),
        api.get("/ServiceRequest/pending"),
      ]);

      // 204 means no active sessions at this location
      const active = Array.isArray(sessionsRes.data) ? sessionsRes.data : [];
      const sessionIds = new Set(active.map((s) => s.session_Id));
      const pending = (
        Array.isArray(requestsRes.data) ? requestsRes.data : []
      ).filter((r) => sessionIds.has(r.session_id));

      setSessions(active);
      setRequests(pending);
      setError(null);
    } catch (err) {
      console.error("Error fetching sessions:", err);
      setError("Failed to load sessions");
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchSessions({ silent: true });
    if (!live) return;
    const interval = setInterval(() => {
      if (!document.hidden) fetchSessions({ silent: true });
    }, POLL_INTERVAL_MS);
    return () => clearInterval(interval);
  }, [live, fetchSessions]);

  const requestsBySession = useMemo(() => {
    const grouped = {};
    for (const request of requests) {
      (grouped[request.session_id] ??= []).push(request);
    }
    return grouped;
  }, [requests]);

  const sessionSummary = useMemo(
    () => ({
      activeSessions: sessions.length,
      tablesInUse: sessions.reduce(
        (sum, s) => sum + (s.table_Numbers?.length || 0),
        0,
      ),
      pendingRequests: requests.length,
    }),
    [sessions, requests],
  );

  return {
    sessions,
    requests,
    requestsBySession,
    sessionSummary,
    isLoading,
    error,
    setError,
    fetchSessions,
  };
};
