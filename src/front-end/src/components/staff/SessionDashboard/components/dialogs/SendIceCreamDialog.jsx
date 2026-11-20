import { useEffect, useState } from "react";
import { Box, Checkbox, CircularProgress, FormControlLabel, Stack, Typography } from "@mui/material";
import FormDialog from "@/components/common/FormDialog";
import QuantityStepper from "@/components/menu/QuantityStepper";
import api from "@/config/api";

// "Mango Ice Cream" -> "Mango": every row is ice cream already
const flavourLabel = (name) => name.replace(/\s+ice\s*cream$/i, "") || name;

const guests = (n) => (n === 1 ? "1 guest" : `${n} guests`);

const errorText = (err, fallback) =>
  err?.response?.data?.message ?? err?.response?.data?.title ?? err?.message ?? fallback;

/**
 * Staff send free ice cream to the kitchen for the bills at a table that are ready for it.
 * Guests said yes on their bill when they asked for it; the server goes over, ticks who it's for
 * (the bills that said yes start ticked, and any other party there can be added), asks which
 * flavours, and sends them as one pending $0 order. Each bill gets it once; a bill that says yes
 * later gets its own turn. One each for the ticked bills' guests at most, so each ticked bill
 * needs a guest count.
 * The flavours come from the server: the Available items in categories marked "Free with the bill".
 * `bills`: the table's bills that haven't had ice cream yet.
 */
const SendIceCreamDialog = ({ open, onClose, sessionId, tableName, bills = [], onSent }) => {
  const [chosen, setChosen] = useState([]);
  const [flavours, setFlavours] = useState([]);
  const [loading, setLoading] = useState(false);
  // How many of each flavour, by item id
  const [counts, setCounts] = useState({});
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  // Fresh choices each time, starting with the bills that said yes. Only on opening: the list
  // refreshes in the background while the dialog is open
  useEffect(() => {
    if (!open) return;
    setChosen(bills.filter((b) => b.wants_ice_cream).map((b) => b.bill_id));
    setCounts({});
    setError(null);
    setLoading(true);
    api
      .get("/Bill/ice-cream-flavours")
      .then(({ data }) => setFlavours(Array.isArray(data) ? data : []))
      .catch((err) => setError(errorText(err, "Couldn't load the ice cream flavours")))
      .finally(() => setLoading(false));
  }, [open]);

  const chosenBills = bills.filter((b) => chosen.includes(b.bill_id));
  const guestCount = chosenBills.reduce((sum, b) => sum + (b.total_guests ?? 0), 0);
  // One each needs a count: a ticked bill with no guests has to be fixed with Edit Guests first
  const uncounted = chosenBills.filter((b) => !(b.total_guests > 0)).map((b) => b.bill_name);
  const max = uncounted.length > 0 ? 0 : guestCount;
  const total = Object.values(counts).reduce((sum, n) => sum + n, 0);

  const toggle = (billId) =>
    setChosen((c) => (c.includes(billId) ? c.filter((id) => id !== billId) : [...c, billId]));
  const countOf = (itemId) => counts[itemId] ?? 0;
  const change = (itemId, by) => setCounts((c) => ({ ...c, [itemId]: Math.max(0, (c[itemId] ?? 0) + by) }));

  const send = async () => {
    setSaving(true);
    setError(null);
    try {
      await api.post(`/Bill/send-ice-cream/${sessionId}`, {
        bill_ids: chosen,
        flavours: flavours
          .filter((f) => countOf(f.item_id) > 0)
          .map((f) => ({ item_id: f.item_id, quantity: countOf(f.item_id) })),
      });
      await onSent?.();
      onClose();
    } catch (err) {
      setError(errorText(err, "Couldn't send the ice cream"));
    } finally {
      setSaving(false);
    }
  };

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={`Free ice cream for ${tableName || "the table"}`}
      maxWidth="xs"
      error={error}
      onErrorClose={() => setError(null)}
      onSubmit={send}
      submitLabel={total === 1 ? "Send 1 to kitchen" : `Send ${total} to kitchen`}
      busyLabel="Sending..."
      busy={saving}
      submitDisabled={chosen.length === 0 || uncounted.length > 0 || total === 0 || total > max}
    >
      <Box component="fieldset" sx={{ border: 0, m: 0, p: 0 }}>
        <Typography component="legend" variant="subtitle2" sx={{ mb: 0.5 }}>
          Who&apos;s it for?
        </Typography>
        <Stack>
          {bills.map((bill) => (
            <FormControlLabel
              key={bill.bill_id}
              control={<Checkbox checked={chosen.includes(bill.bill_id)} onChange={() => toggle(bill.bill_id)} />}
              label={
                <Box component="span">
                  <Typography component="span" fontWeight={600}>
                    {bill.bill_name}
                  </Typography>
                  <Typography component="span" variant="body2" color="text.secondary">
                    {` · ${guests(bill.total_guests ?? 0)}${bill.wants_ice_cream ? " · said yes" : ""}`}
                  </Typography>
                </Box>
              }
            />
          ))}
        </Stack>
      </Box>

      <Box>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
          Ask which flavours they&apos;d like.
          {guestCount > 0 && ` One each, up to ${guestCount}.`}
        </Typography>
        {uncounted.length > 0 && (
          <Typography variant="body2" color="error" sx={{ mb: 1 }}>
            {uncounted.join(", ")} {uncounted.length === 1 ? "has" : "have"} no guests yet. Fix the count with
            Edit Guests first.
          </Typography>
        )}
        {uncounted.length === 0 && total > max && (
          <Typography variant="body2" color="error" sx={{ mb: 1 }}>
            That&apos;s more than one each. Take some off, or tick another bill.
          </Typography>
        )}
        {loading ? (
          <Box sx={{ display: "flex", justifyContent: "center", py: 2 }}>
            <CircularProgress size={28} aria-label="Loading flavours" />
          </Box>
        ) : flavours.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            No ice cream flavours are available. An admin can add them as menu items in a category
            marked &quot;Free with the bill&quot;.
          </Typography>
        ) : (
          <Stack spacing={1}>
            {flavours.map(({ item_id: itemId, name }) => {
              const label = flavourLabel(name);
              return (
                <Box
                  key={itemId}
                  sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 2 }}
                >
                  <Typography fontWeight={600}>{label}</Typography>
                  {/* The same − qty + as the menu and Add Order; + greys out at one each */}
                  <QuantityStepper
                    itemName={label}
                    quantity={countOf(itemId)}
                    onDecrease={() => change(itemId, -1)}
                    onIncrease={() => change(itemId, 1)}
                    canIncrease={chosen.length > 0 && total < max}
                  />
                </Box>
              );
            })}
          </Stack>
        )}
      </Box>
    </FormDialog>
  );
};

export default SendIceCreamDialog;
