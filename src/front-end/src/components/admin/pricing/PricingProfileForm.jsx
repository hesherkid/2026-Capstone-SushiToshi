import { useState } from "react";
import {
  Alert,
  Box,
  Button,
  Checkbox,
  FormControlLabel,
  FormGroup,
  InputAdornment,
  Stack,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";

export const DAY_TYPES = ["Weekday", "Weekend", "Holiday"];

const GUEST_PRICES = [
  { key: "adult_price", label: "Adult" },
  { key: "senior_price", label: "Senior" },
  { key: "child_price", label: "Child" },
  { key: "toddler_price", label: "Toddler" },
];

const WEEK = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

// 0.05 -> "5" (toFixed avoids 5.000000000000001)
const toPercent = (rate) => String(Number(((rate ?? 0) * 100).toFixed(2)));

// Values are kept as text while editing, so a half-typed "4." isn't rewritten
const toForm = (profile) => ({
  tax: toPercent(profile?.tax_rate),
  gratuity: toPercent(profile?.gratuity_rate),
  gratuityMin: String(profile?.gratuity_min_party_size ?? 0),
  leftoverFee: String(profile?.leftover_fee ?? 0),
  weekendDays: profile?.weekend_days ?? [],
  applyHoliday: profile?.apply_holiday_pricing ?? true,
  rates: Object.fromEntries(
    DAY_TYPES.map((dayType) => {
      const rate = profile?.rates?.find((r) => r.day_type === dayType) ?? {};
      return [dayType, Object.fromEntries(GUEST_PRICES.map(({ key }) => [key, String(rate[key] ?? 0)]))];
    }),
  ),
});

// The body for PUT /pricing/default and PUT /pricing/locations/{id}
const toRequest = (form) => ({
  tax_rate: Number(form.tax) / 100,
  gratuity_rate: Number(form.gratuity) / 100,
  gratuity_min_party_size: Number(form.gratuityMin),
  leftover_fee: Number(form.leftoverFee),
  weekend_days: form.weekendDays,
  apply_holiday_pricing: form.applyHoliday,
  rates: DAY_TYPES.map((dayType) => ({
    day_type: dayType,
    ...Object.fromEntries(GUEST_PRICES.map(({ key }) => [key, Number(form.rates[dayType][key])])),
  })),
});

const isAmount = (text) => text !== "" && Number(text) >= 0;   // NaN >= 0 is false

// The first problem found, or null
const validate = (form) => {
  if (!isAmount(form.tax) || Number(form.tax) > 100) return "GST must be between 0% and 100%.";
  if (!isAmount(form.gratuity) || Number(form.gratuity) > 100) return "Gratuity must be between 0% and 100%.";
  if (!isAmount(form.gratuityMin) || !Number.isInteger(Number(form.gratuityMin)) || Number(form.gratuityMin) > 100) {
    return "The gratuity party size must be a whole number from 0 to 100.";
  }
  if (!isAmount(form.leftoverFee)) return "The leftover fee must be $0 or more.";
  const prices = DAY_TYPES.flatMap((dayType) => Object.values(form.rates[dayType]));
  if (!prices.every(isAmount)) return "Every price must be $0 or more.";
  return null;
};

const percentInput = { input: { endAdornment: <InputAdornment position="end">%</InputAdornment> } };
const dollarInput = { input: { startAdornment: <InputAdornment position="start">$</InputAdornment> } };

/**
 * Tax, gratuity, leftover fee, weekend days and the price grid for one pricing profile.
 * Used for the default pricing and for a location's own pricing.
 * readOnly shows the values without a save button (a location that uses the default).
 * onSubmit gets the API request body; the parent shows any server error.
 */
const PricingProfileForm = ({ profile, onSubmit, submitLabel = "Save pricing", readOnly = false }) => {
  const [form, setForm] = useState(() => toForm(profile));
  const [problem, setProblem] = useState(null);
  const [saving, setSaving] = useState(false);

  const set = (field) => (e) => setForm((f) => ({ ...f, [field]: e.target.value }));

  const setPrice = (dayType, key) => (e) =>
    setForm((f) => ({ ...f, rates: { ...f.rates, [dayType]: { ...f.rates[dayType], [key]: e.target.value } } }));

  const toggleDay = (day) =>
    setForm((f) => ({
      ...f,
      weekendDays: f.weekendDays.includes(day)
        ? f.weekendDays.filter((d) => d !== day)
        : [...f.weekendDays, day],
    }));

  const handleSubmit = async (e) => {
    e.preventDefault();
    const found = validate(form);
    setProblem(found);
    if (found) return;

    setSaving(true);
    try {
      await onSubmit(toRequest(form));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Box component="form" onSubmit={handleSubmit} noValidate sx={{ textAlign: "left" }}>
      <Stack spacing={3}>
        <Box>
          <Typography variant="h6" gutterBottom>
            Tax and fees
          </Typography>
          <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { xs: "1fr", sm: "repeat(4, 1fr)" } }}>
            <TextField
              label="GST"
              type="number"
              value={form.tax}
              onChange={set("tax")}
              disabled={readOnly}
              slotProps={{ ...percentInput, htmlInput: { min: 0, max: 100, step: "0.01" } }}
            />
            <TextField
              label="Gratuity"
              type="number"
              value={form.gratuity}
              onChange={set("gratuity")}
              disabled={readOnly}
              slotProps={{ ...percentInput, htmlInput: { min: 0, max: 100, step: "0.01" } }}
            />
            <TextField
              label="Gratuity from party of"
              type="number"
              value={form.gratuityMin}
              onChange={set("gratuityMin")}
              disabled={readOnly}
              helperText="0 = never"
              slotProps={{ htmlInput: { min: 0, max: 100, step: 1 } }}
            />
            <TextField
              label="Leftover fee"
              type="number"
              value={form.leftoverFee}
              onChange={set("leftoverFee")}
              disabled={readOnly}
              slotProps={{ ...dollarInput, htmlInput: { min: 0, step: "0.01" } }}
            />
          </Box>
        </Box>

        <Box>
          <Typography variant="h6">Weekend days</Typography>
          <Typography variant="body2" color="text.secondary">
            These days get weekend prices; the others get weekday prices.
          </Typography>
          <FormGroup row>
            {WEEK.map((day) => (
              <FormControlLabel
                key={day}
                label={day.slice(0, 3)}
                control={
                  <Checkbox
                    checked={form.weekendDays.includes(day)}
                    onChange={() => toggleDay(day)}
                    disabled={readOnly}
                  />
                }
              />
            ))}
          </FormGroup>
          <FormControlLabel
            label="Use holiday prices on approved holidays"
            control={
              <Switch
                checked={form.applyHoliday}
                onChange={(e) => setForm((f) => ({ ...f, applyHoliday: e.target.checked }))}
                disabled={readOnly}
              />
            }
          />
        </Box>

        <Box>
          <Typography variant="h6" gutterBottom>
            Prices per guest
          </Typography>
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell />
                  {GUEST_PRICES.map(({ label }) => (
                    <TableCell key={label}>{label}</TableCell>
                  ))}
                </TableRow>
              </TableHead>
              <TableBody>
                {DAY_TYPES.map((dayType) => (
                  <TableRow key={dayType}>
                    <TableCell component="th" scope="row">
                      {dayType}
                    </TableCell>
                    {GUEST_PRICES.map(({ key, label }) => (
                      <TableCell key={key}>
                        <TextField
                          size="small"
                          type="number"
                          value={form.rates[dayType][key]}
                          onChange={setPrice(dayType, key)}
                          disabled={readOnly}
                          sx={{ minWidth: 110 }}
                          slotProps={{
                            ...dollarInput,
                            htmlInput: { min: 0, step: "0.01", "aria-label": `${dayType} ${label} price` },
                          }}
                        />
                      </TableCell>
                    ))}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </Box>

        {problem && <Alert severity="error">{problem}</Alert>}

        {!readOnly && (
          <Box>
            <Button type="submit" variant="contained" disabled={saving}>
              {saving ? "Saving…" : submitLabel}
            </Button>
          </Box>
        )}
      </Stack>
    </Box>
  );
};

export default PricingProfileForm;
