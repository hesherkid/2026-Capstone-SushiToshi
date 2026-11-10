"use client";
import { useState, useEffect } from "react";
import { Alert, Box, Button, Typography } from "@mui/material";
import Link from "next/link";
import api from "@/config/api";
import {
  fetchMySessionId,
  isSeatGone,
  recheckSeat,
  NOT_SEATED_MESSAGE,
} from "@/services/tableSession";
import { setPageLoading } from "@/utils/pageLoading";
import BillDetails from "@/components/customer/BillDetails";
import CustomerPageContainer from "@/components/customer/CustomerPageContainer";
import useAutoRefresh from "@/hooks/useAutoRefresh";
import { useNotification } from "@/contexts/NotificationContext";
import { MENU_PATH } from "@/contexts/TableOrderContext";

// The guest's bill, right on the page: the total, where it's at and Ready for bill, then the
// receipt. The server only returns the bill this guest is on, never other parties' bills
const BillsPage = () => {
  const { notifyError } = useNotification();
  const [bills, setBills] = useState([]);
  const [loading, setLoading] = useState(true);
  const [notSeated, setNotSeated] = useState(false);
  const [error, setError] = useState(null);
  const [sessionId, setSessionId] = useState(null);
  const [selectedBillId, setSelectedBillId] = useState(null);

  // The app bar's spinner is the page's only loading indicator
  useEffect(() => {
    setPageLoading(loading);
  }, [loading]);
  useEffect(() => () => setPageLoading(false), []);

  useEffect(() => {
    const fetchActiveSession = async () => {
      try {
        // Ask the server (guests included) rather than trusting a saved session_id
        const mySessionId = await fetchMySessionId();
        if (mySessionId) {
          // Still loading: the bills are fetched next
          setSessionId(mySessionId);
        } else {
          setNotSeated(true);
          setLoading(false);
        }
      } catch (err) {
        console.error("Error fetching active session:", err);
        setError(
          err?.response?.data?.detail ||
            err?.response?.data?.message ||
            "Unable to fetch your active session",
        );
        setLoading(false);
      }
    };

    fetchActiveSession();
  }, []);

  // quiet: a background refresh. No spinner, and if it fails the bills already shown stay up
  const fetchBills = async (sid, { quiet = false } = {}) => {
    try {
      if (!quiet) setLoading(true);

      const res = await api.get(`/Bill/get-bills/${sid}`);
      if (res.status !== 200) throw new Error("Failed to fetch bills");

      // Cancelled bills are nothing to pay
      const list = (Array.isArray(res.data) ? res.data : []).filter(
        (bill) =>
          bill?.bill_id &&
          String(bill.status ?? "").toUpperCase() !== "CANCELLED",
      );
      setBills(list);
      setSelectedBillId(list[0]?.bill_id ?? null);
    } catch (err) {
      if (isSeatGone(err)) {
        const stillSeatedAt = await recheckSeat();
        setBills([]);
        setSelectedBillId(null);
        setSessionId(stillSeatedAt);
        if (!stillSeatedAt) setNotSeated(true);
        return;
      }

      console.error("Error fetching bills:", err);

      // 404 means no bills exist - not an error
      if (err?.response?.status === 404) {
        setBills([]);
        setSelectedBillId(null);
      } else if (!quiet) {
        notifyError(
          err?.response?.data?.detail ||
            err?.response?.data?.message ||
            "Error loading bills",
        );
      }
    } finally {
      if (!quiet) setLoading(false);
    }
  };

  useEffect(() => {
    if (sessionId) fetchBills(sessionId);
  }, [sessionId]);

  // Shows a bill as soon as staff open it, and its status as it changes
  useAutoRefresh(() => fetchBills(sessionId, { quiet: true }), {
    enabled: !!sessionId,
  });

  return (
    <CustomerPageContainer sx={{ maxWidth: 720 }}>
      <Typography
        variant="h5"
        component="h1"
        sx={{ fontWeight: 700, mt: { xs: 3, sm: 2 }, mb: 3 }}
      >
        Your bill
      </Typography>

      {error && (
        <Alert severity="error" sx={{ mb: 3 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {/* While loading, the app bar shows the spinner and nothing is listed yet */}
      {loading ? null : notSeated ? (
        <Alert severity="info">{NOT_SEATED_MESSAGE}</Alert>
      ) : bills.length === 0 ? (
        !error && (
          <Box sx={{ textAlign: "center", py: 6 }}>
            <Typography variant="h6" component="h2">
              You&apos;re not on a bill yet
            </Typography>
            <Typography color="text.secondary" sx={{ mt: 0.5, mb: 2 }}>
              Choose your party&apos;s bill, or start one, from the menu.
            </Typography>
            <Button component={Link} href={MENU_PATH} variant="contained">
              Go to the menu
            </Button>
          </Box>
        )
      ) : (
        <>
          {selectedBillId && (
            <BillDetails
              key={selectedBillId}
              sessionId={sessionId}
              billId={selectedBillId}
            />
          )}
        </>
      )}
    </CustomerPageContainer>
  );
};

export default BillsPage;
