import { useState, useEffect } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Box,
  Typography,
  Divider,
  Alert,
  CircularProgress,
  IconButton,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Chip,
} from "@mui/material";
import { X, Receipt, Bell, Lock } from "lucide-react";
import { useSession } from "../../context/SessionContext";
import api from "@/config/api";
import { useAuth } from "@/hooks/useAuth";
import ConfirmDialog from "@/components/common/ConfirmDialog";
import {
  formatCurrency,
  formatDateTime,
  formatPricingType,
  formatGuestBreakdown,
  formatBillStatus,
  getBillStatusColor,
} from "../../utils/sessionHelpers";

// Customers see "Ready for Bill" (asks staff to bring it); staff and admins see "Close Bill"
const BillSummaryDialog = ({ open, sessionId, billId, onClose }) => {
  const { getBillSummary, closeBill } = useSession();
  const { userRole } = useAuth();
  const isStaff = userRole === "staff" || userRole === "admin";
  const [confirmCloseOpen, setConfirmCloseOpen] = useState(false);
  const [summary, setSummary] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [requestingBill, setRequestingBill] = useState(false);
  const [requestSuccess, setRequestSuccess] = useState(false);
  const [hasPendingRequest, setHasPendingRequest] = useState(false);

  useEffect(() => {
    if (open && sessionId && billId) {
      fetchSummary();
      checkPendingRequest();
      setRequestSuccess(false);
    }
  }, [open, sessionId, billId]);

  // Check if there's already a pending "Ready for Bill" request
  useEffect(() => {
    const checkPendingRequest = async () => {
      try {
        const response = await api.get(
          `/ServiceRequest/by-session/${sessionId}`
        );
        const pendingBillRequests = response.data.filter(
          (req) =>
            req.notes.includes("Ready for bill") && req.status === "Pending"
        );
        setHasPendingRequest(pendingBillRequests.length > 0);
      } catch (err) {
        console.error("Error checking pending requests:", err);
      }
    };
    if (open && sessionId) {
      checkPendingRequest();
    }
  }, [open, sessionId]);

  const fetchSummary = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await getBillSummary(sessionId, billId);
      setSummary(data);
    } catch (err) {
      console.error("Error fetching bill summary:", err);
      setError(err.message || "Failed to load bill summary");
    } finally {
      setLoading(false);
    }
  };

  const checkPendingRequest = async () => {
    if (!sessionId) return;

    try {
      const response = await api.get(`/ServiceRequest/by-session/${sessionId}`);

      // Check if there's already a pending "Ready for bill" request
      const pendingBillRequests = response.data.filter(
        (req) =>
          req.notes?.toLowerCase().includes("ready for bill") &&
          req.status?.toLowerCase() === "pending"
      );

      setHasPendingRequest(pendingBillRequests.length > 0);
    } catch (err) {
      console.error("Error checking pending requests:", err);
      // Don't block the user if check fails
      setHasPendingRequest(false);
    }
  };

  const handleRequestBill = async () => {
    try {
      setRequestingBill(true);
      setError(null);

      // Create service request for "Ready for Bill"
      const response = await api.post(`/ServiceRequest/${sessionId}`, {
        notes: `Ready for bill: ${summary?.bill_name || `Bill #${billId}`}`,
      });

      if (response.status !== 200) {
        throw new Error(response.data?.message || "Failed to submit request");
      }

      setRequestSuccess(true);
      setHasPendingRequest(true);

      // Auto-close success message after 8 seconds
      setTimeout(() => {
        setRequestSuccess(false);
      }, 8000);
    } catch (err) {
      console.error("Error requesting bill:", err);
      setError(
        err.response.data || "Failed to request bill. Please try again."
      );
    } finally {
      setRequestingBill(false);
    }
  };

  const handleCloseBill = async () => {
    setError(null);
    const closed = await closeBill(sessionId, billId);
    if (closed) {
      // Reload so the status shows Closed and the button goes away
      await fetchSummary();
    } else {
      setError("Failed to close the bill. Please try again.");
    }
  };

  const handleClose = () => {
    setSummary(null);
    setError(null);
    setRequestSuccess(false);
    setHasPendingRequest(false);
    onClose();
  };

  const getButtonText = () => {
    if (hasPendingRequest) return "Request Already Sent";
    if (requestingBill) return "Requesting...";
    if (requestSuccess) return "Request Sent!";
    return "Ready for Bill";
  };

  if (loading) {
    return (
      <Dialog open={open} onClose={handleClose} maxWidth="md" fullWidth>
        <DialogContent>
          <Box
            display="flex"
            justifyContent="center"
            alignItems="center"
            minHeight="200px"
          >
            <CircularProgress />
          </Box>
        </DialogContent>
      </Dialog>
    );
  }

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="md" fullWidth>
      <DialogTitle>
        <Box display="flex" alignItems="center" gap={1}>
          <Receipt size={24} />
          Bill Summary
        </Box>
        <IconButton
          onClick={handleClose}
          sx={{ position: "absolute", right: 8, top: 8 }}
        >
          <X />
        </IconButton>
      </DialogTitle>

      <DialogContent>
        {error && (
          <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        {isStaff && hasPendingRequest && summary?.status?.toUpperCase() === "OPEN" && (
          <Alert severity="info" icon={<Bell size={20} />} sx={{ mb: 2 }}>
            The guests have asked for this bill.
          </Alert>
        )}

        {requestSuccess && (
          <Alert
            severity="success"
            sx={{ mb: 2 }}
            onClose={() => setRequestSuccess(false)}
          >
            Bill request sent successfully! Staff will bring your bill shortly.
          </Alert>
        )}

        {summary && (
          <Box>
            {/* Bill Header */}
            <Box sx={{ mb: 3 }}>
              <Typography variant="h6" gutterBottom>
                {summary.bill_name}
              </Typography>
              <Box display="flex" gap={2} flexWrap="wrap" alignItems="center">
                <Chip
                  label={formatBillStatus(summary.status)}
                  color={getBillStatusColor(summary.status)}
                  size="small"
                />
                <Chip
                  label={formatPricingType(summary.pricing_type)}
                  variant="outlined"
                  size="small"
                />
                <Typography variant="body2" color="text.secondary">
                  Created: {formatDateTime(summary.created_at)}
                </Typography>
                {summary.table_numbers?.length > 0 && (
                  <Typography variant="body2" color="text.secondary">
                    Tables: {summary.table_numbers.join(", ")}
                  </Typography>
                )}
              </Box>
            </Box>

            <Divider sx={{ my: 2 }} />

            {/* Guest Breakdown */}
            <Box sx={{ mb: 3 }}>
              <Typography variant="subtitle1" fontWeight="600" gutterBottom>
                Guest Breakdown
              </Typography>
              <TableContainer component={Paper} variant="outlined">
                <Table size="small">
                  <TableHead
                    sx={{
                      backgroundColor: "primary.main",
                      "& .MuiTableCell-head": {
                        color: "white",
                        fontWeight: 600,
                      },
                    }}
                  >
                    <TableRow>
                      <TableCell>Type</TableCell>
                      <TableCell align="right">Count</TableCell>
                      <TableCell align="right">Price Each</TableCell>
                      <TableCell align="right">Subtotal</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {summary.adult_count > 0 && (
                      <TableRow>
                        <TableCell>Adults</TableCell>
                        <TableCell align="right">
                          {summary.adult_count}
                        </TableCell>
                        <TableCell align="right">
                          {formatCurrency(summary.adult_base_price)}
                        </TableCell>
                        <TableCell align="right">
                          {formatCurrency(
                            summary.adult_count * summary.adult_base_price
                          )}
                        </TableCell>
                      </TableRow>
                    )}
                    {summary.senior_count > 0 && (
                      <TableRow>
                        <TableCell>Seniors</TableCell>
                        <TableCell align="right">
                          {summary.senior_count}
                        </TableCell>
                        <TableCell align="right">
                          {formatCurrency(summary.senior_base_price)}
                        </TableCell>
                        <TableCell align="right">
                          {formatCurrency(
                            summary.senior_count * summary.senior_base_price
                          )}
                        </TableCell>
                      </TableRow>
                    )}
                    {summary.child_count > 0 && (
                      <TableRow>
                        <TableCell>Children</TableCell>
                        <TableCell align="right">
                          {summary.child_count}
                        </TableCell>
                        <TableCell align="right">
                          {formatCurrency(summary.child_base_price)}
                        </TableCell>
                        <TableCell align="right">
                          {formatCurrency(
                            summary.child_count * summary.child_base_price
                          )}
                        </TableCell>
                      </TableRow>
                    )}
                    {summary.tot_count > 0 && (
                      <TableRow>
                        <TableCell>Toddlers</TableCell>
                        <TableCell align="right">{summary.tot_count}</TableCell>
                        <TableCell align="right">
                          {formatCurrency(summary.toddler_base_price)}
                        </TableCell>
                        <TableCell align="right">
                          {formatCurrency(
                            summary.tot_count * summary.toddler_base_price
                          )}
                        </TableCell>
                      </TableRow>
                    )}
                    <TableRow>
                      <TableCell
                        colSpan={3}
                        align="right"
                        sx={{ fontWeight: 600 }}
                      >
                        Base Charges Subtotal:
                      </TableCell>
                      <TableCell align="right" sx={{ fontWeight: 600 }}>
                        {formatCurrency(summary.base_charges_subtotal)}
                      </TableCell>
                    </TableRow>
                  </TableBody>
                </Table>
              </TableContainer>
            </Box>

            {/* Add-on Items */}
            {summary.addon_items?.length > 0 && (
              <Box sx={{ mb: 3 }}>
                <Typography variant="subtitle1" fontWeight="600" gutterBottom>
                  Premium Add-ons
                </Typography>
                <TableContainer component={Paper} variant="outlined">
                  <Table size="small">
                    <TableHead
                      sx={{
                        backgroundColor: "primary.main",
                        "& .MuiTableCell-head": {
                          color: "white",
                          fontWeight: 600,
                        },
                      }}
                    >
                      <TableRow>
                        <TableCell>Item</TableCell>
                        <TableCell align="right">Qty</TableCell>
                        <TableCell align="right">Price</TableCell>
                        <TableCell align="right">Total</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {summary.addon_items.map((item) => (
                        <TableRow key={item.order_item_id}>
                          <TableCell>{item.item_name}</TableCell>
                          <TableCell align="right">{item.quantity}</TableCell>
                          <TableCell align="right">
                            {formatCurrency(item.price_per_unit)}
                          </TableCell>
                          <TableCell align="right">
                            {formatCurrency(item.line_total)}
                          </TableCell>
                        </TableRow>
                      ))}
                      <TableRow>
                        <TableCell
                          colSpan={3}
                          align="right"
                          sx={{ fontWeight: 600 }}
                        >
                          Add-ons Subtotal:
                        </TableCell>
                        <TableCell align="right" sx={{ fontWeight: 600 }}>
                          {formatCurrency(summary.addon_subtotal)}
                        </TableCell>
                      </TableRow>
                    </TableBody>
                  </Table>
                </TableContainer>
              </Box>
            )}

            <Divider sx={{ my: 2 }} />

            {/* Total Summary */}
            <Box>
              <Box display="flex" justifyContent="space-between" sx={{ mb: 1 }}>
                <Typography variant="body1">Subtotal:</Typography>
                <Typography variant="body1">
                  {formatCurrency(summary.subtotal)}
                </Typography>
              </Box>
              <Box display="flex" justifyContent="space-between" sx={{ mb: 1 }}>
                <Typography variant="body1">
                  Tax ({(summary.tax_rate * 100).toFixed(0)}%):
                </Typography>
                <Typography variant="body1">
                  {formatCurrency(summary.tax_amount)}
                </Typography>
              </Box>
              <Divider sx={{ my: 1 }} />
              <Box display="flex" justifyContent="space-between">
                <Typography variant="h6" fontWeight="700">
                  Total Amount:
                </Typography>
                <Typography variant="h6" fontWeight="700" color="primary">
                  {formatCurrency(summary.total_amount)}
                </Typography>
              </Box>
            </Box>
          </Box>
        )}
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2 }}>

        {summary?.status?.toUpperCase() === "OPEN" && isStaff && (
          <Button
            variant="contained"
            color="error"
            onClick={() => setConfirmCloseOpen(true)}
            startIcon={<Lock size={16} />}
          >
            Close Bill
          </Button>
        )}

        {summary?.status?.toUpperCase() === "OPEN" && !isStaff && (
          <Button
            variant="contained"
            onClick={handleRequestBill}
            disabled={requestingBill || requestSuccess || hasPendingRequest}
            startIcon={
              requestingBill ? (
                <CircularProgress size={16} />
              ) : (
                <Bell size={16} />
              )
            }
          >
            {getButtonText()}
          </Button>
        )}
      </DialogActions>

      <ConfirmDialog
        open={confirmCloseOpen}
        title="Close bill"
        message={`Close ${summary?.bill_name || "this bill"}? Guests won't be able to order on it.`}
        confirmLabel="Close Bill"
        onConfirm={handleCloseBill}
        onClose={() => setConfirmCloseOpen(false)}
      />
    </Dialog>
  );
};

export default BillSummaryDialog;
