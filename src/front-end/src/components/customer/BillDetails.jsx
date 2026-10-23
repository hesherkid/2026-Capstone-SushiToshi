import { Alert, Box, Button, Chip, Paper, Skeleton, Typography } from "@mui/material";
import BillBreakdown from "@/components/bills/BillBreakdown";
import { useBillSummary } from "@/components/bills/useBillSummary";
import ConfirmDialog from "@/components/common/ConfirmDialog";
import MaterialSymbol from "@/components/common/MaterialSymbol";
import { useBillRequest } from "@/components/customer/useBillRequest";
import useAutoRefresh from "@/hooks/useAutoRefresh";
import { formatCurrency, formatTime } from "@/components/staff/SessionDashboard/utils/sessionHelpers";

const GUESTS = [
  { count: "adult_count", one: "adult", many: "adults" },
  { count: "senior_count", one: "senior", many: "seniors" },
  { count: "child_count", one: "child", many: "children" },
  { count: "tot_count", one: "toddler", many: "toddlers" },
];

// "2 adults · 1 child"
const guestsLine = (summary) =>
  GUESTS.filter(({ count }) => summary[count] > 0)
    .map(({ count, one, many }) => `${summary[count]} ${summary[count] === 1 ? one : many}`)
    .join(" · ");

// Where the bill is at, in the guest's words
const billState = (summary) => {
  const status = String(summary.status ?? "").toUpperCase();
  if (status === "PAID") {
    return { chip: "Paid", color: "success", message: "This bill is paid. Thank you!" };
  }
  if (status === "CLOSED") {
    return { chip: "Closed", color: "default", message: "This bill is closed. Thanks for dining with us!" };
  }
  if (summary.requested_at) {
    return {
      chip: "Bill requested",
      color: "warning",
      message: `You asked for the bill at ${formatTime(summary.requested_at)}. Your server is bringing it, and no more orders can be added to it.`,
    };
  }
  return {
    chip: "Open",
    color: "success",
    message: "Keep ordering as long as you like. When done, tap Ready for bill and your server will bring it.",
  };
};

/**
 * One bill on the customer Bill page: the total and where it's at up top, with the
 * Ready for bill button, then the full receipt. Keeps itself current as orders come in.
 */
const BillDetails = ({ sessionId, billId }) => {
  const { summary, loading, error, setError, reload, isOpen } = useBillSummary({
    open: true,
    sessionId,
    billId,
  });
  const { confirmOpen, openConfirm, closeConfirm, requestSent, setRequestSent, requestBill } =
    useBillRequest({ sessionId, billId, reload, setError });

  // Add-ons, over-limit charges and leftovers change the total as the meal goes on
  useAutoRefresh(() => reload({ quiet: true }), { enabled: !!summary });

  if (loading && !summary) {
    return (
      <Box aria-label="Loading bill" role="status">
        <Skeleton variant="rounded" height={220} sx={{ borderRadius: 3, mb: 2 }} />
        <Skeleton variant="rounded" height={320} sx={{ borderRadius: 3 }} />
      </Box>
    );
  }

  if (!summary) {
    return error ? <Alert severity="error">{error}</Alert> : null;
  }

  const state = billState(summary);
  const guests = guestsLine(summary);
  const canRequest = isOpen && !summary.requested_at;

  return (
    <>
      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {/* ---- The total and where the bill is at ---- */}
      <Paper variant="outlined" sx={{ p: { xs: 2.5, sm: 3 }, mb: 2, borderRadius: 3 }}>
        <Box sx={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: 2 }}>
          <Box sx={{ minWidth: 0, textAlign: "left" }}>
            <Typography variant="h6" component="h2" sx={{ fontWeight: 700, lineHeight: 1.3 }}>
              {summary.bill_name}
            </Typography>
            {guests && (
              <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25 }}>
                {guests}
              </Typography>
            )}
          </Box>
          <Chip label={state.chip} color={state.color} size="small" sx={{ fontWeight: 600, flexShrink: 0 }} />
        </Box>

        <Box sx={{ mt: 3 }}>
          <Typography variant="body2" color="text.secondary">
            {summary.gratuity_amount > 0
              ? `Total, including GST and ${Number((summary.gratuity_rate * 100).toFixed(2))}% gratuity`
              : "Total, including GST"}
          </Typography>
          <Typography
            component="p"
            sx={{
              fontSize: { xs: "2.25rem", sm: "2.5rem" },
              fontWeight: 700,
              lineHeight: 1.15,
              fontVariantNumeric: "tabular-nums",
            }}
          >
            {formatCurrency(summary.total_amount)}
          </Typography>
          {/* Staff count leftovers at the end of the meal, so they can still be added */}
          {isOpen && (
            <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 0.5 }}>
              Additional leftover fees may apply.
            </Typography>
          )}
        </Box>


        {requestSent ? (
          <Alert
            severity="success"
            icon={<MaterialSymbol name="check_circle" filled size={22} />}
            sx={{ mt: 2.5 }}
            onClose={() => setRequestSent(false)}
          >
            Done! Your server will bring your bill shortly.
          </Alert>
        ) : (
          <Typography color="text.secondary" sx={{ mt: 2.5, fontSize: "0.9rem", lineHeight: 1.5 }}>
            {state.message}
          </Typography>
        )}

        {isOpen && (
          <Button
            variant="contained"
            fullWidth
            size="large"
            onClick={openConfirm}
            disabled={!canRequest}
            startIcon={<MaterialSymbol name="receipt_long" size={22} />}
            sx={{ mt: 2.5, minHeight: 52, borderRadius: 999, fontSize: "1rem" }}
          >
            {canRequest ? "Ready for bill" : "Bill requested"}
          </Button>
        )}
      </Paper>

      {/* ---- Bill breakdown ---- */}
      <Paper variant="outlined" sx={{ p: { xs: 2.5, sm: 3 }, borderRadius: 3 }}>
        <Typography variant="subtitle1" component="h2" sx={{ fontWeight: 700, mb: 1 }}>
          Details
        </Typography>
        <BillBreakdown summary={summary} showHeader={false} />
      </Paper>

      <ConfirmDialog
        open={confirmOpen}
        title="Ready for your bill?"
        message="Your server will bring your bill. After this, no more orders can be added to it."
        confirmLabel="Ask for bill"
        onConfirm={requestBill}
        onClose={closeConfirm}
      />
    </>
  );
};

export default BillDetails;
