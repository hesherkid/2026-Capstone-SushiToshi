import { useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Checkbox,
  FormControlLabel,
  FormGroup,
  InputAdornment,
  InputBase,
  Paper,
  Stack,
  Switch,
  TextField,
  Typography,
} from "@mui/material";
import ManagementGrid from "@/components/common/ManagementGrid";

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

// What a save would send, with the weekend days in week order, so unticking and re-ticking
// a day (or typing 40.950 for 40.95) doesn't count as a change
const comparable = (form) => {
  const request = toRequest(form);
  return JSON.stringify({ ...request, weekend_days: WEEK.filter((d) => request.weekend_days.includes(d)) });
};

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

// A price cell's input: an outlined box like a small TextField, so it's clear you can type in it
const priceInput = {
  width: "100%",
  fontSize: "inherit",
  minHeight: 40,
  px: 1.25,
  py: 1,
  border: 1,
  borderColor: "grey.400",
  borderRadius: 1,
  bgcolor: "background.paper",
  transition: "border-color 150ms",
  "&:hover": { borderColor: "text.primary" },
  "&.Mui-focused": { borderColor: "primary.main", boxShadow: (theme) => `inset 0 0 0 1px ${theme.palette.primary.main}` },
  "& input": { p: 0 },
  // Read-only pricing has no box: it can't be typed in
  "&.Mui-disabled": { borderColor: "transparent", px: 0 },
  // Hide the number spinners
  "& input::-webkit-outer-spin-button, & input::-webkit-inner-spin-button": { WebkitAppearance: "none", m: 0 },
  "& input[type=number]": { MozAppearance: "textfield" },
  // Read-only pricing stays readable instead of greyed out
  "&.Mui-disabled input": { WebkitTextFillColor: "inherit", color: "text.primary" },
};

// The price grid's columns: the day type, then one input per guest type.
// onChange(dayType, key, value) is the only way a price changes
const getPriceColumns = ({ onChange, readOnly }) => [
  { field: "id", headerName: "Day", flex: 1, minWidth: 110 },
  ...GUEST_PRICES.map(({ key, label }) => ({
    field: key,
    headerName: label,
    flex: 1,
    minWidth: 110,
    renderCell: ({ row, value }) => (
      <InputBase
        type="number"
        value={value}
        onChange={(e) => onChange(row.id, key, e.target.value)}
        // Keep the grid's keyboard navigation from taking arrow keys and space while typing
        onKeyDown={(e) => e.stopPropagation()}
        disabled={readOnly}
        startAdornment={<InputAdornment position="start">$</InputAdornment>}
        inputProps={{ min: 0, step: "0.01", "aria-label": `${row.id} ${label} price` }}
        sx={priceInput}
      />
    ),
  })),
];

const percentInput = { input: { endAdornment: <InputAdornment position="end">%</InputAdornment> } };
const dollarInput = { input: { startAdornment: <InputAdornment position="start">$</InputAdornment> } };

// One part of the form, framed like the price grid so every section matches
const Section = ({ title, description, children }) => (
  <Paper variant="outlined" sx={{ p: 2, bgcolor: "background.paper" }}>
    <Typography variant="h6">{title}</Typography>
    {description && (
      <Typography variant="body2" color="text.secondary">
        {description}
      </Typography>
    )}
    <Box sx={{ mt: 1.5 }}>{children}</Box>
  </Paper>
);

/**
 * Tax, gratuity, leftover fee, weekend days and the price grid for one pricing profile.
 * Used for the default pricing and for a location's own pricing.
 * readOnly shows the values without a save button (a location that uses the default).
 * onSubmit gets the API request body. The parent passes back how the save went as
 * saveResult ({ severity, message }), shown next to the save button.
 * Save is enabled once something changes, or straight away with canSaveUnchanged (a location
 * switching to its own pricing, where saving the default prices as they are is the change).
 */
const PricingProfileForm = ({
  profile,
  onSubmit,
  submitLabel = "Save pricing",
  readOnly = false,
  saveResult = null,
  onSaveResultClose,
  canSaveUnchanged = false,
}) => {
  const [form, setForm] = useState(() => toForm(profile));
  // The parent remounts the form after a save, so this is always what's stored
  const [saved] = useState(() => comparable(toForm(profile)));
  const unsaved = canSaveUnchanged || comparable(form) !== saved;
  const [problem, setProblem] = useState(null);
  const [saving, setSaving] = useState(false);

  const set = (field) => (e) => setForm((f) => ({ ...f, [field]: e.target.value }));

  // Stable columns, so a price input isn't remounted (and loses focus) on every keystroke
  const priceColumns = useMemo(
    () =>
      getPriceColumns({
        readOnly,
        onChange: (dayType, key, value) =>
          setForm((f) => ({ ...f, rates: { ...f.rates, [dayType]: { ...f.rates[dayType], [key]: value } } })),
      }),
    [readOnly],
  );

  const toggleDay = (day) =>
    setForm((f) => ({
      ...f,
      weekendDays: f.weekendDays.includes(day)
        ? f.weekendDays.filter((d) => d !== day)
        : [...f.weekendDays, day],
    }));

  const handleSubmit = async (e) => {
    e.preventDefault();
    onSaveResultClose?.();
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
        <Section title="Prices per guest">
          <ManagementGrid
            rows={DAY_TYPES.map((dayType) => ({ id: dayType, ...form.rates[dayType] }))}
            columns={priceColumns}
            showToolbar={false}
            hideFooter
            disableColumnMenu
            disableColumnSorting
          />
        </Section>

        <Section
          title="When each price applies"
          description="Checked days get Weekend prices and approved holidays get Holiday prices. Every other day gets Weekday prices."
        >
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
        </Section>

        <Section title="Tax and fees">
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
        </Section>

        {!readOnly && (
          // Pinned to the bottom while scrolling, so Save and its result are always in view
          <Paper
            variant="outlined"
            sx={{ position: "sticky", bottom: 0, zIndex: 2, p: 2, bgcolor: "background.paper" }}
          >
            <Stack spacing={1.5}>
              {problem && <Alert severity="error">{problem}</Alert>}
              {saveResult && (
                <Alert severity={saveResult.severity} onClose={onSaveResultClose}>
                  {saveResult.message}
                </Alert>
              )}
              <Stack direction="row" spacing={2} alignItems="center" justifyContent="flex-end">
                {unsaved && (
                  <Typography variant="body2" color="warning.main" role="status">
                    Unsaved changes
                  </Typography>
                )}
                <Button type="submit" variant="contained" disabled={saving || !unsaved}>
                  {saving ? "Saving…" : submitLabel}
                </Button>
              </Stack>
            </Stack>
          </Paper>
        )}
      </Stack>
    </Box>
  );
};

export default PricingProfileForm;
