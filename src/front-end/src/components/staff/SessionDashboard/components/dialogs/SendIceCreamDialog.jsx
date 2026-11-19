import { useEffect, useState } from "react";
import { Box, Checkbox, FormControlLabel, IconButton, Stack, Typography } from "@mui/material";
import { Minus, Plus } from "lucide-react";
import FormDialog from "@/components/common/FormDialog";
import api from "@/config/api";

// The free flavours, in the order the kitchen sees them (the server's FreeIceCream.Flavours)
export const ICE_CREAM_FLAVOURS = ["Strawberry", "Chocolate", "Mango", "Vanilla"];

const none = () => Object.fromEntries(ICE_CREAM_FLAVOURS.map((f) => [f, 0]));

const guests = (n) => (n === 1 ? "1 guest" : `${n} guests`);

const errorText = (err, fallback) =>
  err?.response?.data?.message ?? err?.response?.data?.title ?? err?.message ?? fallback;

/**
 * Staff send free ice cream to the kitchen for the bills at a table that are ready for it.
 * Guests said yes on their bill when they asked for it; the server goes over, ticks who it's for
 * (the bills that said yes start ticked, and any other party there can be added), asks which
 * flavours, and sends them as one pending $0 order. Each bill gets it once; a bill that says yes
 * later gets its own turn. One each for the ticked bills' guests at most.
 * `bills`: the table's bills that haven't had ice cream yet.
 */
const SendIceCreamDialog = ({ open, onClose, sessionId, tableName, bills = [], onSent }) => {
  const [chosen, setChosen] = useState([]);
  const [counts, setCounts] = useState(none);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  // Fresh choices each time, starting with the bills that said yes. Only on opening: the list
  // refreshes in the background while the dialog is open
  useEffect(() => {
    if (!open) return;
    setChosen(bills.filter((b) => b.wants_ice_cream).map((b) => b.bill_id));
    setCounts(none());
    setError(null);
  }, [open]);

  const chosenBills = bills.filter((b) => chosen.includes(b.bill_id));
  const guestCount = chosenBills.reduce((sum, b) => sum + (b.total_guests ?? 0), 0);
  const max = guestCount > 0 ? guestCount : Infinity;
  const total = Object.values(counts).reduce((sum, n) => sum + n, 0);

  const toggle = (billId) =>
    setChosen((c) => (c.includes(billId) ? c.filter((id) => id !== billId) : [...c, billId]));
  const change = (flavour, by) => setCounts((c) => ({ ...c, [flavour]: Math.max(0, c[flavour] + by) }));

  const send = async () => {
    setSaving(true);
    setError(null);
    try {
      await api.post(`/Bill/send-ice-cream/${sessionId}`, {
        bill_ids: chosen,
        flavours: ICE_CREAM_FLAVOURS.filter((f) => counts[f] > 0).map((f) => ({ flavour: f, quantity: counts[f] })),
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
      submitDisabled={chosen.length === 0 || total === 0 || total > max}
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
        {total > max && (
          <Typography variant="body2" color="error" sx={{ mb: 1 }}>
            That&apos;s more than one each. Take some off, or tick another bill.
          </Typography>
        )}
        <Stack spacing={1}>
          {ICE_CREAM_FLAVOURS.map((flavour) => (
            <Box
              key={flavour}
              sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 2 }}
            >
              <Typography id={`ice-cream-${flavour}`} fontWeight={600}>
                {flavour}
              </Typography>
              <Box
                role="group"
                aria-labelledby={`ice-cream-${flavour}`}
                sx={{ display: "flex", alignItems: "center", gap: 1 }}
              >
                <IconButton
                  aria-label={`Fewer ${flavour}`}
                  onClick={() => change(flavour, -1)}
                  disabled={counts[flavour] === 0}
                  sx={{ border: 1, borderColor: "divider" }}
                >
                  <Minus size={18} />
                </IconButton>
                <Typography
                  aria-label={`${flavour} count`}
                  sx={{ minWidth: 24, textAlign: "center", fontWeight: 600, fontVariantNumeric: "tabular-nums" }}
                >
                  {counts[flavour]}
                </Typography>
                <IconButton
                  aria-label={`More ${flavour}`}
                  onClick={() => change(flavour, 1)}
                  disabled={chosen.length === 0 || total >= max}
                  sx={{ border: 1, borderColor: "divider" }}
                >
                  <Plus size={18} />
                </IconButton>
              </Box>
            </Box>
          ))}
        </Stack>
      </Box>
    </FormDialog>
  );
};

export default SendIceCreamDialog;
