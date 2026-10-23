import { useState, useEffect, useCallback } from "react";
import api from "@/config/api";

const SUCCESS_MESSAGE_MS = 8000;

/**
 * "Ready for bill": asks staff to bring the bill. Asking locks it: the server refuses new
 * orders on it (staff can still add). Used by the Bill page and the bill dialog.
 * `reload` refreshes the bill afterwards so it shows as requested.
 */
export const useBillRequest = ({ sessionId, billId, reload, setError }) => {
  const [confirmOpen, setConfirmOpen] = useState(false);
  // Shows the success message for a few seconds
  const [requestSent, setRequestSent] = useState(false);

  // Another bill, fresh message
  useEffect(() => {
    setRequestSent(false);
  }, [billId]);

  useEffect(() => {
    if (!requestSent) return undefined;
    const timer = setTimeout(() => setRequestSent(false), SUCCESS_MESSAGE_MS);
    return () => clearTimeout(timer);
  }, [requestSent]);

  const requestBill = useCallback(async () => {
    setError(null);
    try {
      // Marks the bill as requested and lets staff know
      await api.post(`/Bill/request_bill/${billId}`, null, { params: { _session_id: sessionId } });
      setRequestSent(true);
      await reload();
    } catch (err) {
      console.error("Error requesting bill:", err);
      setError(err.response?.data?.message || "Failed to request bill. Please try again.");
    }
  }, [billId, sessionId, reload, setError]);

  return {
    confirmOpen,
    openConfirm: () => setConfirmOpen(true),
    closeConfirm: () => setConfirmOpen(false),
    requestSent,
    setRequestSent,
    requestBill,
  };
};

export default useBillRequest;
