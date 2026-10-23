"use client";
import { useState, useEffect } from "react";
import { Alert, Box, Tab, Tabs, Typography } from "@mui/material";
import api from "@/config/api";
import { fetchMySessionId, NOT_SEATED_MESSAGE } from "@/services/tableSession";
import { setPageLoading } from "@/utils/pageLoading";
import BillDetails from "@/components/customer/BillDetails";
import CustomerPageContainer from "@/components/customer/CustomerPageContainer";
import useAutoRefresh from "@/hooks/useAutoRefresh";
import { useNotification } from "@/contexts/NotificationContext";
import { sortBillsByStatus } from "@/components/staff/SessionDashboard/utils/sessionHelpers";

const billName = (bill) => bill.bill_name || `Bill #${bill.bill_id}`;

// The bill this phone orders on (set on the menu and Review order), if it's still at the table
const myBillId = (bills) => {
  // Also runs while rendering, which happens on the server too
  if (typeof window === "undefined") return null;
  const saved = Number(localStorage.getItem("currentBillId"));
  return bills.some((bill) => bill.bill_id === saved) ? saved : null;
};

// The guest's bill, right on the page: the total, where it's at and Ready for bill, then the
// receipt. A table with several bills gets tabs, starting on the one this phone orders on
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

      const res = await api.get(`/Bill/get_bills/${sid}`);
      if (res.status !== 200) throw new Error("Failed to fetch bills");

      // Cancelled bills are nothing to pay; open ones first
      const list = sortBillsByStatus(
        (Array.isArray(res.data) ? res.data : []).filter(
          (bill) => bill?.bill_id && String(bill.status ?? "").toUpperCase() !== "CANCELLED",
        ),
      );
      setBills(list);

      // Keep the bill being looked at; otherwise this phone's bill, otherwise the first
      setSelectedBillId((current) =>
        list.some((bill) => bill.bill_id === current) ? current : myBillId(list) ?? list[0]?.bill_id ?? null,
      );
    } catch (err) {
      console.error("Error fetching bills:", err);

      // 404 means no bills exist - not an error
      if (err?.response?.status === 404) {
        setBills([]);
        setSelectedBillId(null);
      } else if (!quiet) {
        notifyError(err?.response?.data?.detail || err?.response?.data?.message || "Error loading bills");
      }
    } finally {
      if (!quiet) setLoading(false);
    }
  };

  useEffect(() => {
    if (sessionId) fetchBills(sessionId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sessionId]);

  // Shows a bill as soon as staff open it, and its status as it changes
  useAutoRefresh(() => fetchBills(sessionId, { quiet: true }), { enabled: !!sessionId });

  const ordersGoOn = myBillId(bills);

  return (
    <CustomerPageContainer sx={{ maxWidth: 720 }}>
      <Typography variant="h5" component="h1" sx={{ fontWeight: 700, mt: { xs: 3, sm: 2 }, mb: 3 }}>
        {bills.length > 1 ? "Your table's bills" : "Your bill"}
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
              No bills yet
            </Typography>
            <Typography color="text.secondary" sx={{ mt: 0.5 }}>
              Your server will open a bill for your table, and it will show up here.
            </Typography>
          </Box>
        )
      ) : (
        <>
          {bills.length > 1 && (
            <Tabs
              value={selectedBillId ?? false}
              onChange={(_, billId) => setSelectedBillId(billId)}
              variant="scrollable"
              scrollButtons={false}
              aria-label="Bills at your table"
              sx={{ mb: 2, borderBottom: 1, borderColor: "divider" }}
            >
              {bills.map((bill) => (
                <Tab
                  key={bill.bill_id}
                  value={bill.bill_id}
                  label={bill.bill_id === ordersGoOn ? `${billName(bill)} (yours)` : billName(bill)}
                  sx={{ textTransform: "none", fontWeight: 600, fontSize: "0.95rem" }}
                />
              ))}
            </Tabs>
          )}

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
