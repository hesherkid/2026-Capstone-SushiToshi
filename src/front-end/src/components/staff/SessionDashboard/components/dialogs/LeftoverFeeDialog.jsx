import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  InputAdornment,
  TextField,
  Typography,
} from "@mui/material";
import FormDialog from "@/components/common/FormDialog";
import api from "@/config/api";
import { formatCurrency } from "@/components/staff/SessionDashboard/utils/sessionHelpers";

const splitEvenly = (amount, count) => {
  if (count <= 0) return [];
  const cents = Math.round((Number(amount) || 0) * 100);
  const base = Math.floor(cents / count);
  const extra = cents % count;
  return Array.from(
    { length: count },
    (_, i) => (base + (i < extra ? 1 : 0)) / 100,
  );
};

const errorText = (err, fallback) =>
  err?.response?.data?.message ??
  err?.response?.data?.title ??
  err?.message ??
  fallback;

/**
 * Staff add (or change, or remove) the table's leftover fee before closing its bills.
 * Starts on the default fee (or the fee already applied); staff can change it.
 * The fee is for the whole table and is split evenly across its open bills.
 */
const LeftoverFeeDialog = ({ open, onClose, sessionId, onApplied }) => {
  const [info, setInfo] = useState(null);
  const [amount, setAmount] = useState("");
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (!open || !sessionId) return;
    setLoading(true);
    setError(null);
    api
      .get(`/Bill/leftover_fee/${sessionId}`)
      .then(({ data }) => {
        setInfo(data);
        const current = Number(data.amount) || 0;
        setAmount(String(current > 0 ? current : (data.default_fee ?? 0)));
      })
      .catch((err) =>
        setError(errorText(err, "Couldn't load the leftover fee")),
      )
      .finally(() => setLoading(false));
  }, [open, sessionId]);

  const bills = info?.bills ?? [];
  const current = Number(info?.amount) || 0;
  const parsed = Number(amount);
  const valid = amount !== "" && !Number.isNaN(parsed) && parsed >= 0;
  const shares = valid ? splitEvenly(parsed, bills.length) : [];

  const save = async (value) => {
    setSaving(true);
    setError(null);
    try {
      const { data } = await api.put(`/Bill/leftover_fee/${sessionId}`, {
        amount: value,
      });
      onApplied?.(data);
      onClose();
    } catch (err) {
      setError(errorText(err, "Couldn't apply the leftover fee"));
    } finally {
      setSaving(false);
    }
  };

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title="Leftover fee"
      maxWidth="xs"
      error={error}
      onErrorClose={() => setError(null)}
      onSubmit={() => save(parsed)}
      submitLabel="Apply fee"
      busyLabel="Applying..."
      busy={saving}
      submitDisabled={loading || !valid || bills.length === 0}
      extraActions={
        current > 0 && (
          <Button color="error" onClick={() => save(0)} disabled={saving} sx={{ mr: "auto" }}>
            Remove fee
          </Button>
        )
      }
    >
      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
          <CircularProgress />
        </Box>
      ) : (
        <>
          <Typography variant="body2" color="text.secondary">
            For tables that leave too much food. One fee for the whole table,
            split evenly across its open bills and taxed with the bill. Apply
            it before closing the bills; closed bills aren't changed.
          </Typography>

          <TextField
            label="Fee for the table"
            type="number"
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            fullWidth
            autoFocus
            helperText={`Default ${formatCurrency(info?.default_fee ?? 0)}`}
            error={amount !== "" && !valid}
            slotProps={{
              input: {
                startAdornment: <InputAdornment position="start">$</InputAdornment>,
              },
              htmlInput: { min: 0, step: "0.01" },
            }}
          />

          {bills.length === 0 ? (
            <Alert severity="info">This table has no open bills.</Alert>
          ) : (
            <Box>
              <Typography variant="subtitle2" gutterBottom>
                Split evenly across {bills.length} open{" "}
                {bills.length === 1 ? "bill" : "bills"}
              </Typography>
              {bills.map((bill, i) => (
                <Box
                  key={bill.bill_id}
                  sx={{ display: "flex", justifyContent: "space-between", py: 0.25 }}
                >
                  <Typography variant="body2">
                    {bill.bill_name || `Bill #${bill.bill_id}`}
                  </Typography>
                  <Typography variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
                    {formatCurrency(shares[i] ?? 0)}
                  </Typography>
                </Box>
              ))}
            </Box>
          )}
        </>
      )}
    </FormDialog>
  );
};

export default LeftoverFeeDialog;
