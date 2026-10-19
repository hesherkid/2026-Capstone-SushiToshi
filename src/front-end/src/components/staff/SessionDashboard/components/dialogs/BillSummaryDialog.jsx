import { useState } from "react";
import { Alert, Button } from "@mui/material";
import { Bell, Lock } from "lucide-react";
import { useSession } from "../../context/SessionContext";
import ConfirmDialog from "@/components/common/ConfirmDialog";
import BillDialogFrame from "@/components/bills/BillDialogFrame";
import { useBillSummary } from "@/components/bills/useBillSummary";
import { formatTime } from "../../utils/sessionHelpers";

// Staff view of a bill: review the total, then close it. Customers use CustomerBillDialog.
const BillSummaryDialog = ({ open, sessionId, billId, onClose }) => {
  const { closeBill } = useSession();
  const { summary, loading, error, setError, isOpen } = useBillSummary({ open, sessionId, billId });
  const [confirmCloseOpen, setConfirmCloseOpen] = useState(false);

  const handleCloseBill = async () => {
    setError(null);
    if (await closeBill(sessionId, billId)) {
      // Done with this bill; the confirm dialog closes itself
      onClose();
    } else {
      setError("Failed to close the bill. Please try again.");
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
          isOpen &&
          summary?.requested_at && (
            <Alert severity="info" icon={<Bell size={20} />} sx={{ mb: 2 }}>
              The guests asked for this bill at {formatTime(summary.requested_at)}. They can't add
              more orders to it, but you still can.
            </Alert>
          )
        }
        actions={
          isOpen && (
            <Button
              variant="contained"
              color="error"
              onClick={() => setConfirmCloseOpen(true)}
              startIcon={<Lock size={16} />}
            >
              Close Bill
            </Button>
          )
        }
      />

      <ConfirmDialog
        open={confirmCloseOpen}
        title="Close bill"
        message={`Close ${summary?.bill_name || "this bill"}? Guests won't be able to order on it.`}
        confirmLabel="Close Bill"
        onConfirm={handleCloseBill}
        onClose={() => setConfirmCloseOpen(false)}
      />
    </>
  );
};

export default BillSummaryDialog;
