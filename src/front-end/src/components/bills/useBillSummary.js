import { useState, useEffect, useCallback } from "react";
import api from "@/config/api";

/**
 * Loads a bill's priced summary (guests, add-ons, tax, total) while the dialog is open.
 * Calls the API directly so customer pages don't need the staff SessionProvider.
 */
export const useBillSummary = ({ open, sessionId, billId }) => {
  const [summary, setSummary] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const reload = useCallback(async () => {
    if (!sessionId || !billId) return;
    try {
      setLoading(true);
      setError(null);
      const { data } = await api.get(`/Bill/summary/${billId}`, {
        params: { _session_id: sessionId },
      });
      setSummary(data);
    } catch (err) {
      console.error("Error fetching bill summary:", err);
      setError(err.response?.data?.message || "Failed to load bill summary");
    } finally {
      setLoading(false);
    }
  }, [sessionId, billId]);

  useEffect(() => {
    if (open) {
      reload();
    } else {
      setSummary(null);
      setError(null);
    }
  }, [open, reload]);

  const isOpen = summary?.status?.toUpperCase() === "OPEN";

  return { summary, loading, error, setError, reload, isOpen };
};
