import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  DialogActions,
  DialogContent,
  Divider,
  IconButton,
  Paper,
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
  { key: "toddlers", label: "Toddlers", hint: "6 & under" },
];

const MAX_OF_EACH = 50;
const NEW_PARTY = { billName: "", adults: 1, seniors: 0, children: 0, toddlers: 0 };

const guests = (count) => `${count} guest${count === 1 ? "" : "s"}`;
const totalOf = (party) => GUEST_TYPES.reduce((sum, { key }) => sum + party[key], 0);
const billLabel = (bill) => bill.bill_name || `Bill #${bill.bill_id}`;

// A bill's counts in the form's shape
const countsOf = (bill) => ({
  adults: bill?.adult_count ?? 0,
  seniors: bill?.senior_count ?? 0,
  children: bill?.child_count ?? 0,
  toddlers: bill?.tot_count ?? 0,
});

const BILL_STEPS = [
  { icon: "group", text: "One person starts the bill for your party." },
  { icon: "smartphone", text: "Everyone else in the party adds themselves to it from their phone." },
  { icon: "call_split", text: "Paying separately? Start your own bill." },
];

// How a party shares a bill, shown above the choices
const HowBillsWork = () => (
  <Stack component="ul" spacing={1} sx={{ m: 0, mb: 2.5, p: 1.5, listStyle: "none", bgcolor: "action.hover", borderRadius: 3 }}>
    {BILL_STEPS.map(({ icon, text }) => (
      <Box component="li" key={icon} sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
        <MaterialSymbol name={icon} size={20} sx={{ color: "primary.main", flexShrink: 0 }} />
        <Typography variant="body2" color="text.secondary">
          {text}
        </Typography>
      </Box>
    ))}
  </Stack>
);

// − count + for one kind of guest, one row of the party card
const GuestCounter = ({ label, hint, value, onChange, disabled }) => (
  <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 2, px: 2, py: 1 }}>
    <Box id={`count-${label}`}>
      <Typography sx={{ fontWeight: 500 }}>{label}</Typography>
      {hint && (
        <Typography variant="caption" color="text.secondary">
          {hint}
        </Typography>
      )}
    </Box>
    <Box
      role="group"
      aria-labelledby={`count-${label}`}
      sx={{ display: "flex", alignItems: "center", border: 1, borderColor: "divider", borderRadius: 999 }}
    >
      <IconButton
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
        aria-label={`More ${label.toLowerCase()}`}
        onClick={() => onChange(value + 1)}
        disabled={disabled || value >= MAX_OF_EACH}
      >
        <MaterialSymbol name="add" size={20} />
      </IconButton>
    </Box>
  </Box>
);

// The party card: a counter for each kind of guest, with the total underneath
const PartyCounts = ({ party, onChange, disabled }) => {
  const total = totalOf(party);
  return (
    <Box>
      <Typography variant="subtitle2" component="h3" sx={{ mb: 1 }}>
        Who&apos;s in your party?
      </Typography>
      <Paper variant="outlined" sx={{ borderRadius: 3, overflow: "hidden" }}>
        <Stack divider={<Divider />}>
          {GUEST_TYPES.map(({ key, label, hint }) => (
            <GuestCounter
              key={key}
              label={label}
              hint={hint}
              value={party[key]}
              onChange={(value) => onChange({ ...party, [key]: value })}
              disabled={disabled}
            />
          ))}
        </Stack>
        <Typography
          role="status"
          sx={{
            px: 2,
            py: 1.25,
            bgcolor: "action.hover",
            borderTop: 1,
            borderColor: "divider",
            fontWeight: 700,
            color: total > 0 ? "text.primary" : "error.main",
          }}
        >
          {total > 0 ? `${guests(total)} on this bill` : "Add at least one guest"}
        </Typography>
      </Paper>
    </Box>
  );
};

const TITLES = {
  choose: "Which bill are you on?",
  start: "Start your bill",
  guests: "How many in your party?",
};

/**
 * Which bill a guest is on: join one of the table's open bills, or start their party's own.
 * Shown when a seated guest isn't on a bill yet, and from Review order to switch bills.
 * After joining a bill they check its guest count ("guests"), which Review order can also open.
 * Only names and party sizes of other bills are shown, never what's on them.
 */
const BillChoiceDialog = ({ open, onClose, initialView = "choose" }) => {
  const { openBills, myBill, billChangeBlocked, chooseBill, startBill, updateGuestCounts } =
    useTableOrder();
  // "choose" a bill, "start" a new one, or check the "guests" on their own
  const [view, setView] = useState(initialView);
  const [party, setParty] = useState(NEW_PARTY);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  // With no bills at the table yet, starting one is the only choice
  const shown = view === "choose" && openBills.length === 0 ? "start" : view;
  const totalGuests = totalOf(party);
  const canStart = party.billName.trim().length > 0 && totalGuests > 0;
  const countsChanged = Boolean(myBill) && GUEST_TYPES.some(({ key }) => party[key] !== countsOf(myBill)[key]);
  // Staff checked this bill's guest count and locked it, so it's shown but can't be changed
  const countsLocked = Boolean(myBill?.counts_locked);

  const showGuests = (bill) => {
    setParty({ ...NEW_PARTY, ...countsOf(bill) });
    setView("guests");
  };

  // Only as the dialog opens: a background refresh of myBill mustn't undo the guest's edits
  useEffect(() => {
    if (!open) return;
    setError(null);
    if (initialView === "guests" && myBill) {
      showGuests(myBill);
    } else {
      setView(initialView === "guests" ? "choose" : initialView);
      setParty(NEW_PARTY);
    }
  }, [open, initialView]);

  // Runs a server call; `after` gets its result (closing the dialog by default)
  const run = async (action, after = onClose) => {
    setBusy(true);
    setError(null);
    try {
      after(await action());
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

  // Joining a bill goes on to checking its guest count
  const handleChoose = (billId) => run(() => chooseBill(billId), showGuests);

  const handleGuests = (event) => {
    event.preventDefault();
    if (!countsChanged) onClose();
    else if (totalGuests > 0) run(() => updateGuestCounts(party));
  };

  const handleClose = () => {
    if (!busy) onClose();
  };

  const subtitles = {
    choose: "Add yourself to your party's bill so your orders go on it.",
    start: "Your server will check the number of guests.",
    guests: myBill
      ? `You're on ${billLabel(myBill)}.${countsLocked ? "" : " Your server will check the number of guests."}`
      : "",
  };

  // Back to the table's bills, unless they can't move off theirs
  const showBack =
    (shown === "start" && openBills.length > 0) || (shown === "guests" && !billChangeBlocked);

  return (
    <AppDialog open={open} onClose={handleClose} maxWidth="xs">
      <DialogHeader
        title={TITLES[shown]}
        subtitle={subtitles[shown]}
        onClose={handleClose}
        closeDisabled={busy}
      />

      <DialogContent dividers>
        {error && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}
        {billChangeBlocked && shown !== "guests" && (
          <Alert severity="info" sx={{ mb: 2 }}>
            {billChangeBlocked}
          </Alert>
        )}

        {/* Top-left, so the main button can have the whole bottom */}
        {showBack && (
          <Button
            onClick={() => setView("choose")}
            disabled={busy}
            color="inherit"
            size="small"
            startIcon={<MaterialSymbol name="arrow_back" size={18} />}
            sx={{ mb: 1.5, ml: -1 }}
          >
            Back to bills
          </Button>
        )}

        {shown !== "guests" && <HowBillsWork />}

        {shown === "start" && (
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
            <PartyCounts party={party} onChange={setParty} disabled={busy} />
          </Stack>
        )}

        {shown === "guests" && myBill && (
          <Stack component="form" id="guest-count-form" spacing={2.5} onSubmit={handleGuests}>
            {/* More phones on the bill than guests on it: someone probably isn't counted */}
            {countsLocked && (
              <Alert severity="info" icon={<MaterialSymbol name="lock" size={22} />}>
                Your server has confirmed your party. Ask your server if you need your party adjusted.
              </Alert>
            )}
            {!countsLocked && myBill.people_count > totalGuests && (
              <Alert severity="warning">
                {myBill.people_count} people have joined this bill. Make sure everyone is counted.
              </Alert>
            )}
            <PartyCounts party={party} onChange={setParty} disabled={busy || countsLocked} />
          </Stack>
        )}

        {shown === "choose" && (
          <Stack spacing={1.5}>
            {openBills.map((bill) => {
              const isMine = bill.bill_id === myBill?.bill_id;
              const requested = Boolean(bill.requested_at);
              return (
                <Button
                  key={bill.bill_id}
                  variant={isMine ? "contained" : "outlined"}
                  size="large"
                  onClick={() => handleChoose(bill.bill_id)}
                  disabled={busy || requested || (!isMine && !!billChangeBlocked)}
                  aria-current={isMine || undefined}
                  sx={{ justifyContent: "space-between", textAlign: "left", py: 1.5 }}
                  endIcon={isMine ? <MaterialSymbol name="check" size={20} /> : undefined}
                >
                  <Box component="span" sx={{ display: "flex", flexDirection: "column" }}>
                    <Box component="span" sx={{ fontWeight: 700 }}>
                      {billLabel(bill)}
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
                  onClick={() => setView("start")}
                  disabled={busy}
                >
                  Start a new bill
                </Button>
              </>
            )}
          </Stack>
        )}
      </DialogContent>

      {shown !== "choose" && (
        <DialogActions sx={{ px: 3, py: 2 }}>
          <Button
            type="submit"
            form={shown === "start" ? "start-bill-form" : "guest-count-form"}
            variant="contained"
            size="large"
            fullWidth
            disabled={busy || (shown === "start" ? !canStart : totalGuests === 0)}
            sx={{ minHeight: 52, borderRadius: 999, fontSize: "1rem" }}
          >
            {shown === "start" && (busy ? "Starting..." : "Start Bill")}
            {shown === "guests" && (busy ? "Saving..." : countsChanged ? "Save guest count" : "Looks right")}
          </Button>
        </DialogActions>
      )}
    </AppDialog>
  );
};

export default BillChoiceDialog;
