import { useState } from "react";
import { Alert, Button } from "@mui/material";
import { Bell, Lock, Utensils } from "lucide-react";
import { useSession } from "../../context/SessionContext";
import ConfirmDialog from "@/components/common/ConfirmDialog";
import BillDialogFrame from "@/components/bills/BillDialogFrame";
import { useBillSummary } from "@/components/bills/useBillSummary";
import { formatCurrency, formatTime } from "../../utils/sessionHelpers";
import LeftoverFeeDialog from "./LeftoverFeeDialog";

// Staff view of a bill: review the total, add a leftover fee if needed, then close it.
// Customers use CustomerBillDialog.
const BillSummaryDialog = ({ open, sessionId, billId, onClose }) => {
  const { closeBill } = useSession();
  const { summary, loading, error, setError, reload, isOpen } = useBillSummary({
    open,
    sessionId,
    billId,
  });
  const [confirmCloseOpen, setConfirmCloseOpen] = useState(false);
  const [leftoverOpen, setLeftoverOpen] = useState(false);

  const handleCloseBill = async () => {
    setError(null);
    if (await closeBill(sessionId, billId)) {
      // Done with this bill; the confirm dialog closes itself
      onClose();
    } else {
      setError("Failed to close the bill. Please try again.");
    }
  };

  const leftoverCharge = Number(summary?.leftover_charge) || 0;

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
              The guests asked for this bill at{" "}
              {formatTime(summary.requested_at)}. They can't add more orders to
              it, but you still can.
            </Alert>
          )
        }
        actions={
          isOpen && (
            <>
              {/* The fee is for the whole table, so it has to go on before the bills close */}
              <Button
                variant="outlined"
                onClick={() => setLeftoverOpen(true)}
                startIcon={<Utensils size={16} />}
              >
                {leftoverCharge > 0
                  ? "Change leftover fee"
                  : "Add leftover fee"}
              </Button>
              <Button
                variant="contained"
                color="error"
                onClick={() => setConfirmCloseOpen(true)}
                startIcon={<Lock size={16} />}
              >
                Close Bill
              </Button>
            </>
          )
        }
      />

      <LeftoverFeeDialog
        open={leftoverOpen}
        onClose={() => setLeftoverOpen(false)}
        sessionId={sessionId}
        onApplied={reload}
      />

      <ConfirmDialog
        open={confirmCloseOpen}
        title="Close bill"
        message={
          `Close ${summary?.bill_name || "this bill"}? Guests won't be able to order on it.` +
          (leftoverCharge > 0
            ? ` It includes a ${formatCurrency(leftoverCharge)} leftover charge.`
            : " No leftover fee has been added; it can't be added to this bill once it's closed.")
        }
        confirmLabel="Close Bill"
        onConfirm={handleCloseBill}
        onClose={() => setConfirmCloseOpen(false)}
      />
    </>
  );
};

export default BillSummaryDialog;
