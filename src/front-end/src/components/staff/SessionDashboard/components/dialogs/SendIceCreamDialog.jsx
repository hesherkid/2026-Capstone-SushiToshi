import { useEffect, useState } from "react";
import { Box, IconButton, Stack, Typography } from "@mui/material";
import { Minus, Plus } from "lucide-react";
import FormDialog from "@/components/common/FormDialog";
import api from "@/config/api";

// The free flavours, in the order the kitchen sees them (the server's FreeIceCream.Flavours)
export const ICE_CREAM_FLAVOURS = ["Strawberry", "Chocolate", "Mango", "Vanilla"];

const none = () => Object.fromEntries(ICE_CREAM_FLAVOURS.map((f) => [f, 0]));

const errorText = (err, fallback) =>
  err?.response?.data?.message ?? err?.response?.data?.title ?? err?.message ?? fallback;

/**
 * Staff send a bill's free ice cream to the kitchen. The guests said yes when they asked for the
 * bill; the server asks the table which flavours and how many, then sends them here. It goes to
 * the kitchen as a pending $0 order, and works on closed bills too. One each for the party at most.
 */
const SendIceCreamDialog = ({ open, onClose, sessionId, bill, onSent }) => {
  const [counts, setCounts] = useState(none);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  // Fresh choices each time
  useEffect(() => {
    if (!open) return;
    setCounts(none());
    setError(null);
  }, [open]);

  const partySize = bill?.total_guests ?? 0;
  const max = partySize > 0 ? partySize : Infinity;
  const total = Object.values(counts).reduce((sum, n) => sum + n, 0);

  const change = (flavour, by) => setCounts((c) => ({ ...c, [flavour]: Math.max(0, c[flavour] + by) }));

  const send = async () => {
    setSaving(true);
    setError(null);
    try {
      await api.post(
        `/Bill/send-ice-cream/${bill.bill_id}`,
        { flavours: ICE_CREAM_FLAVOURS.filter((f) => counts[f] > 0).map((f) => ({ flavour: f, quantity: counts[f] })) },
        { params: { session_id: sessionId } },
      );
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
      title={`Free ice cream for ${bill?.bill_name ?? "this bill"}`}
      maxWidth="xs"
      error={error}
      onErrorClose={() => setError(null)}
      onSubmit={send}
      submitLabel={total === 1 ? "Send 1 to kitchen" : `Send ${total} to kitchen`}
      busyLabel="Sending..."
      busy={saving}
      submitDisabled={total === 0}
    >
      <Typography variant="body2" color="text.secondary">
        Ask the table which flavours they&apos;d like.
        {partySize > 0 && ` One each, up to ${partySize}.`}
      </Typography>

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
                disabled={total >= max}
                sx={{ border: 1, borderColor: "divider" }}
              >
                <Plus size={18} />
              </IconButton>
            </Box>
          </Box>
        ))}
      </Stack>
    </FormDialog>
  );
};

export default SendIceCreamDialog;
