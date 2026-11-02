import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  DialogActions,
  DialogContent,
  Divider,
  IconButton,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import AppDialog from "@/components/common/AppDialog";
import DialogHeader from "@/components/common/DialogHeader";
import MaterialSymbol from "@/components/common/MaterialSymbol";
import { useTableOrder } from "@/contexts/TableOrderContext";

const GUEST_TYPES = [
  { key: "adults", label: "Adults" },
  { key: "seniors", label: "Seniors" },
  { key: "children", label: "Children" },
  { key: "toddlers", label: "Toddlers (6 & under)" },
];

const MAX_OF_EACH = 50;
const NEW_PARTY = { billName: "", adults: 1, seniors: 0, children: 0, toddlers: 0 };

const guests = (count) => `${count} guest${count === 1 ? "" : "s"}`;

// How a party shares a bill, shown above the choices
const HowBillsWork = () => (
  <Box component="ul" sx={{ m: 0, mb: 2.5, pl: 2.5, color: "text.secondary", "& li + li": { mt: 0.5 } }}>
    <li>One person starts the bill for your party.</li>
    <li>Everyone else in the party adds themselves to it from their phone.</li>
    <li>Paying separately? Start your own bill.</li>
  </Box>
);

// − count + for one kind of guest
const GuestCounter = ({ label, value, onChange, disabled }) => (
  <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 2 }}>
    <Typography id={`count-${label}`}>{label}</Typography>
    <Box
      role="group"
      aria-labelledby={`count-${label}`}
      sx={{ display: "flex", alignItems: "center", border: 1, borderColor: "divider", borderRadius: 999 }}
    >
      <IconButton
        size="small"
        aria-label={`Fewer ${label.toLowerCase()}`}
        onClick={() => onChange(value - 1)}
        disabled={disabled || value <= 0}
      >
        <MaterialSymbol name="remove" size={20} />
      </IconButton>
      <Typography sx={{ minWidth: 28, textAlign: "center", fontWeight: 700 }} aria-live="polite">
        {value}
      </Typography>
      <IconButton
        size="small"
        aria-label={`More ${label.toLowerCase()}`}
        onClick={() => onChange(value + 1)}
        disabled={disabled || value >= MAX_OF_EACH}
      >
        <MaterialSymbol name="add" size={20} />
      </IconButton>
    </Box>
  </Box>
);

/**
 * Which bill a guest is on: join one of the table's open bills, or start their party's own.
 * Shown when a seated guest isn't on a bill yet, and from Review order to switch bills.
 * Only names and party sizes of other bills are shown, never what's on them.
 */
const BillChoiceDialog = ({ open, onClose }) => {
  const { openBills, myBill, billChangeBlocked, chooseBill, startBill } = useTableOrder();
  const [starting, setStarting] = useState(false);
  const [party, setParty] = useState(NEW_PARTY);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  // With no bills at the table yet, starting one is the only choice
  const showForm = starting || openBills.length === 0;
  const totalGuests = GUEST_TYPES.reduce((sum, { key }) => sum + party[key], 0);
  const canStart = party.billName.trim().length > 0 && totalGuests > 0;

  useEffect(() => {
    if (!open) return;
    setStarting(false);
    setParty(NEW_PARTY);
    setError(null);
  }, [open]);

  const run = async (action) => {
    setBusy(true);
    setError(null);
    try {
      await action();
      onClose();
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  };

  const handleStart = (event) => {
    event.preventDefault();
    if (canStart) run(() => startBill(party));
  };

  const handleClose = () => {
    if (!busy) onClose();
  };

  return (
    <AppDialog open={open} onClose={handleClose} maxWidth="xs">
      <DialogHeader
        title={showForm ? "Start your bill" : "Which bill are you on?"}
        subtitle={
          showForm
            ? "Your server will check the number of guests."
            : "Add yourself to your party's bill so your orders go on it."
        }
        onClose={handleClose}
        closeDisabled={busy}
      />

      <DialogContent dividers>
        {error && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}
        {billChangeBlocked && (
          <Alert severity="info" sx={{ mb: 2 }}>
            {billChangeBlocked}
          </Alert>
        )}

        {/* Back to the table's bills, so the Start Bill button can have the whole bottom */}
        {showForm && openBills.length > 0 && (
          <Button
            onClick={() => setStarting(false)}
            disabled={busy}
            color="inherit"
            size="small"
            startIcon={<MaterialSymbol name="arrow_back" size={18} />}
            sx={{ mb: 1.5, ml: -1 }}
          >
            Back to bills
          </Button>
        )}

        <HowBillsWork />

        {showForm ? (
          <Stack component="form" id="start-bill-form" spacing={2.5} onSubmit={handleStart}>
            <TextField
              label="Name for the bill"
              placeholder="e.g. your first name"
              value={party.billName}
              onChange={(event) => setParty((prev) => ({ ...prev, billName: event.target.value }))}
              disabled={busy}
              required
              fullWidth
              autoFocus
              slotProps={{ htmlInput: { maxLength: 100 } }}
            />
            <Stack spacing={1.5}>
              {GUEST_TYPES.map(({ key, label }) => (
                <GuestCounter
                  key={key}
                  label={label}
                  value={party[key]}
                  onChange={(value) => setParty((prev) => ({ ...prev, [key]: value }))}
                  disabled={busy}
                />
              ))}
            </Stack>
            <Typography role="status" color="text.secondary">
              {guests(totalGuests)} on this bill
            </Typography>
          </Stack>
        ) : (
          <Stack spacing={1.5}>
            {openBills.map((bill) => {
              const isMine = bill.bill_id === myBill?.bill_id;
              const requested = Boolean(bill.requested_at);
              return (
                <Button
                  key={bill.bill_id}
                  variant={isMine ? "contained" : "outlined"}
                  size="large"
                  onClick={() => run(() => chooseBill(bill.bill_id))}
                  disabled={busy || requested || (!isMine && !!billChangeBlocked)}
                  aria-current={isMine || undefined}
                  sx={{ justifyContent: "space-between", textAlign: "left", py: 1.5 }}
                  endIcon={isMine ? <MaterialSymbol name="check" size={20} /> : undefined}
                >
                  <Box component="span" sx={{ display: "flex", flexDirection: "column" }}>
                    <Box component="span" sx={{ fontWeight: 700 }}>
                      {bill.bill_name || `Bill #${bill.bill_id}`}
                    </Box>
                    <Box component="span" sx={{ typography: "body2" }}>
                      {requested ? "Has asked for the bill" : guests(bill.total_count ?? 0)}
                    </Box>
                  </Box>
                </Button>
              );
            })}
            {!billChangeBlocked && (
              <>
                <Divider>or</Divider>
                <Button
                  variant="text"
                  startIcon={<MaterialSymbol name="add" size={20} />}
                  onClick={() => setStarting(true)}
                  disabled={busy}
                >
                  Start a new bill
                </Button>
              </>
            )}
          </Stack>
        )}
      </DialogContent>

      {showForm && (
        <DialogActions>
          <Button
            type="submit"
            form="start-bill-form"
            variant="contained"
            size="large"
            fullWidth
            disabled={busy || !canStart}
          >
            {busy ? "Starting..." : "Start Bill"}
          </Button>
        </DialogActions>
      )}
    </AppDialog>
  );
};

export default BillChoiceDialog;
