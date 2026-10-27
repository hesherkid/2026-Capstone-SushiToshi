import { useState, useEffect, useCallback } from "react";
import api from "@/config/api";
import { isSeatGone, recheckSeat } from "@/services/tableSession";

/**
 * Loads a bill's priced summary (guests, add-ons, tax, total) while the dialog is open.
 * Calls the API directly so customer pages don't need the staff SessionProvider.
 */
export const useBillSummary = ({ open, sessionId, billId }) => {
  const [summary, setSummary] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  // quiet: a background refresh. No loading state, and if it fails the summary shown stays up
  const reload = useCallback(
    async ({ quiet = false } = {}) => {
      if (!sessionId || !billId) return;
      try {
        if (!quiet) {
          setLoading(true);
          setError(null);
        }
        const { data } = await api.get(`/Bill/summary/${billId}`, {
          params: { _session_id: sessionId },
        });
        setSummary(data);
      } catch (err) {
        // The table was closed or they moved: let the seat check show the thank-you screen.
        if (isSeatGone(err)) {
          const stillSeatedAt = await recheckSeat();
          if (stillSeatedAt) setError("This bill is no longer available.");
          return;
        }
        console.error("Error fetching bill summary:", err);
        if (!quiet)
          setError(
            err.response?.data?.message || "Failed to load bill summary",
          );
      } finally {
        if (!quiet) setLoading(false);
      }
    },
    [sessionId, billId],
  );

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
