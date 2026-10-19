import { useState, useEffect } from "react";
import { Alert, Button } from "@mui/material";
import { ChevronRight } from "lucide-react";
import api from "@/config/api";
import BillDialogFrame from "@/components/bills/BillDialogFrame";
import ConfirmDialog from "@/components/common/ConfirmDialog";
import { useBillSummary } from "@/components/bills/useBillSummary";
import { formatTime } from "@/components/staff/SessionDashboard/utils/sessionHelpers";

const SUCCESS_MESSAGE_MS = 8000;

// Customer view of their bill: check the total, then ask staff to bring it.
// Asking locks the bill: the server refuses new orders on it (staff can still add).
// Staff use the session dashboard's BillSummaryDialog, which closes bills instead.
const CustomerBillDialog = ({ open, sessionId, billId, onClose }) => {
  const { summary, loading, error, setError, reload, isOpen } = useBillSummary({ open, sessionId, billId });
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [requestSent, setRequestSent] = useState(false);

  const requestedAt = summary?.requested_at;

  // Fresh dialog, fresh success message
  useEffect(() => {
    if (open) setRequestSent(false);
  }, [open, billId]);

  useEffect(() => {
    if (!requestSent) return;
    const timer = setTimeout(() => setRequestSent(false), SUCCESS_MESSAGE_MS);
    return () => clearTimeout(timer);
  }, [requestSent]);

  const handleRequestBill = async () => {
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
  };

  return (
    <>
      <BillDialogFrame
        open={open}
        onClose={onClose}
        summary={summary}
        loading={loading}
        error={error}
        onErrorClose={() => setError(null)}
        notices={
          <>
            {requestSent && (
              <Alert severity="success" sx={{ mb: 2 }} onClose={() => setRequestSent(false)}>
                Bill request sent successfully! Staff will bring your bill shortly.
              </Alert>
            )}
            {isOpen && requestedAt && !requestSent && (
              <Alert severity="info" sx={{ mb: 2 }}>
                You asked for this bill at {formatTime(requestedAt)}. Staff will bring it shortly,
                and no more orders can be added to it.
              </Alert>
            )}
          </>
        }
        actions={
          isOpen && (
            // Full width: label on the left, arrow on the right
            <Button
              variant="contained"
              fullWidth
              size="large"
              onClick={() => setConfirmOpen(true)}
              disabled={!!requestedAt}
              endIcon={requestedAt ? null : <ChevronRight size={20} />}
              sx={{ justifyContent: "space-between", textAlign: "left", minHeight: 56, px: 2.5, fontSize: "1.05rem" }}
            >
              {requestedAt ? "Bill Requested" : "Ready for Bill"}
            </Button>
          )
        }
      />

      <ConfirmDialog
        open={confirmOpen}
        title="Ready for your bill?"
        message="Staff will bring your bill. After this, no more orders can be added to it."
        confirmLabel="Ask for Bill"
        onConfirm={handleRequestBill}
        onClose={() => setConfirmOpen(false)}
      />
    </>
  );
};

export default CustomerBillDialog;
