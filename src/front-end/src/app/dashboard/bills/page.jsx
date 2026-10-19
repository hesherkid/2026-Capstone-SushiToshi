"use client";
import React, { useState, useEffect } from "react";
import {
  Box,
  Card,
  Typography,
  Alert,
  Container,
  IconButton,
  Dialog,
  Button,
} from "@mui/material";
import { Plus, Receipt } from "lucide-react";
import { styled } from "@mui/material/styles";
import api from "@/config/api";
import { fetchMySessionId, NOT_SEATED_MESSAGE } from "@/services/tableSession";
import { setPageLoading } from "@/utils/pageLoading";
import NewBillDialog from "@/components/staff/SessionDashboard/components/dialogs/NewBillDialog";
import CustomerBillDialog from "@/components/customer/CustomerBillDialog";
import { SessionProvider } from "@/components/staff/SessionDashboard/context/SessionContext";
import { useNotification } from "@/contexts/NotificationContext";
import { getStatusColorValue, formatDateTime, formatBillStatus, getTableDescription, sortBillsByStatus } from "@/components/staff/SessionDashboard/utils/sessionHelpers";

const AddButton = styled(IconButton)(({ theme }) => ({
  backgroundColor: theme.palette.primary.main,
  color: "white",
  padding: theme.spacing(1),
  minWidth: "auto",
  "&:hover": {
    backgroundColor: theme.palette.primary.dark,
  },
  "& svg": {
    width: 20,
    height: 20,
  },
}));

const StatusChip = styled(Typography, {
  shouldForwardProp: (prop) => prop !== '$statusColor',
})(({ theme, $statusColor }) => ({
  color: "white",
  backgroundColor: $statusColor,
  padding: `${theme.spacing(0.5)} ${theme.spacing(1)}`,
  borderRadius: theme.shape.borderRadius,
  alignSelf: "flex-start",
  fontWeight: "medium",
  fontSize: "0.875rem",
  userSelect: "none",
}));

const BillCard = styled(Card, {
  shouldForwardProp: (prop) => prop !== '$statusColor',
})(({ theme, $statusColor }) => ({
  padding: theme.spacing(3),
  display: "flex",
  flexDirection: "column",
  gap: theme.spacing(1),
  backgroundColor: "white",
  borderRadius: theme.shape.borderRadius,
  borderLeft: `6px solid ${$statusColor}`,
  minHeight: "200px",
  position: "relative",
  boxShadow: theme.shadows[2],
  transition: "all 0.3s cubic-bezier(0.4, 0, 0.2, 1)",
  "&:hover": {
    boxShadow: theme.shadows[4],
    transform: "translateY(-4px)",
  },
}));

const BillsDashboard = () => {
  const { notifySuccess, notifyError } = useNotification();
  const [bills, setBills] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [summaryDialogOpen, setSummaryDialogOpen] = useState(false);
  const [selectedBillId, setSelectedBillId] = useState(null)
  const [sessionId, setSessionId] = useState(null);

  const isGuest = () => localStorage.getItem("guest") === "true";

  // The app bar's spinner is the page's only loading indicator
  useEffect(() => {
    setPageLoading(loading);
  }, [loading]);
  useEffect(() => () => setPageLoading(false), []);

  // Fetch active session ID first
  useEffect(() => {
    const fetchActiveSession = async () => {
      try {
        setLoading(true);
        setError(null);

        // Ask the server (guests included) rather than trusting a saved session_id
        const mySessionId = await fetchMySessionId();
        if (mySessionId) {
          // Still loading: the bills are fetched next
          setSessionId(mySessionId);
        } else {
          setError(NOT_SEATED_MESSAGE);
          setLoading(false);
        }
      } catch (err) {
        console.error("Error fetching active session:", err);
        setError(
          err?.response?.data?.detail ||
          err?.response?.data?.message ||
          "Unable to fetch your active session"
        );
        setLoading(false);
      }
    };

    fetchActiveSession();
  }, []);

  // Fetch bills when sessionId is available
  useEffect(() => {
    if (sessionId) {
      fetchBills(sessionId);
    }
  }, [sessionId]);

  const fetchBills = async (sid) => {

    const apiClient = api;

    try {
      setLoading(true);
      setError(null);

      const res = await apiClient.get(`/Bill/get_bills/${sid}`);

      if (res.status !== 200) {
        throw new Error("Failed to fetch bills");
      }

      const billsData = Array.isArray(res.data) ? res.data : [];

      setBills(billsData);
    } catch (err) {
      console.error("Error fetching bills:", err);

      // 404 means no bills exist - not an error
      if (err?.response?.status === 404) {
        setBills([]);
        setError(null);
      } else {
        notifyError(
          err?.response?.data?.detail ||
          err?.response?.data?.message ||
          "Error loading bills"
        );
      }
    } finally {
      setLoading(false);
    }
  };

  const handleBillCreated = async () => {
    if (sessionId) {
      await fetchBills(sessionId);
    }
    setDialogOpen(false);
  };

  const handleViewSummary = (billId) => {
    setSelectedBillId(billId);
    setSummaryDialogOpen(true);
  };

  const sortedBills = sortBillsByStatus(bills || []);

  return (
    <SessionProvider sessionId={sessionId}>
      <Container maxWidth="lg">
        <Box sx={{ py: 3 }}>
          <Box
            sx={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              mb: 4,
            }}
          >
            <Typography variant="h4">Your Table's Bills</Typography>

          </Box>

          {error && (
            <Alert severity="error" sx={{ mb: 3 }} onClose={() => setError(null)}>
              {typeof error === "string" ? error : "Error loading bills"}
            </Alert>
          )}

          {/* While loading, the app bar shows the spinner and nothing is listed yet */}
          {loading ? null : !bills || bills.length === 0 ? (
            <Alert severity="info">No bills yet. Create your first bill to get started.</Alert>
          ) : (
            <Box
              sx={{
                display: "grid",
                gridTemplateColumns: "repeat(auto-fill, minmax(280px, 1fr))",
                gap: 2,
                width: "100%",
              }}
            >
              {sortedBills.map((bill) => {
                if (!bill?.bill_id) return null;
                const statusColor = getStatusColorValue(bill.status);

                return (
                  <BillCard key={bill.bill_id} $statusColor={statusColor}>
                    <Typography variant="h6" component="div" noWrap>
                      {bill.bill_name || `Bill #${bill.bill_id}`}
                    </Typography>

                    <StatusChip $statusColor={statusColor}>
                      {formatBillStatus(bill.status)}
                    </StatusChip>

                    <Typography variant="body2" sx={{ color: "text.secondary", fontWeight: "medium" }}>
                      Total: {bill.total_count || 0} guest{bill.total_count !== 1 ? 's' : ''}
                    </Typography>

                    {bill.created_at && (
                      <Typography variant="body2" sx={{ color: "text.secondary" }}>
                        Created: {formatDateTime(bill.created_at)}
                      </Typography>
                    )}

                    {bill.table_numbers?.length > 0 && (
                      <Typography variant="body2" sx={{ color: "text.secondary" }}>
                        {getTableDescription(bill.table_numbers)}
                      </Typography>
                    )}

                    <Button
                      variant="outlined"
                      size="small"
                      startIcon={<Receipt size={16} />}
                      onClick={() => handleViewSummary(bill.bill_id)}
                      sx={{ mt: 1 }}
                    >
                      View Summary
                    </Button>
                  </BillCard>
                );
              })}
            </Box>
          )}

          {/* Bill Summary Dialog */}
          <CustomerBillDialog
            open={summaryDialogOpen}
            sessionId={sessionId}
            billId={selectedBillId}
            onClose={() => {
              setSummaryDialogOpen(false);
              setSelectedBillId(null);
            }}
          />
        </Box>
      </Container>
    </SessionProvider>
  );
};

export default BillsDashboard;
