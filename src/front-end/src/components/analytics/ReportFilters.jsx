"use client";

import { useState } from "react";
import {
  Button,
  Chip,
  Stack,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
} from "@mui/material";
import { toInputDate } from "@/components/analytics/reportUtils";

const RANGE_OPTIONS = [
  { value: "yesterday", label: "Yesterday" },
  { value: "today", label: "Today" },
  { value: "last7", label: "Last 7 full days before today" },
  { value: "last30", label: "Last 30 full days before today" },
  { value: "custom", label: "Date Range" },
];

// Range presets, plus From/To dates and a Show results button for a custom range.
export const DateRangeFilter = ({ value, onChange, onApplyCustom }) => {
  const [customFrom, setCustomFrom] = useState("");
  const [customTo, setCustomTo] = useState("");

  const todayInput = toInputDate(new Date());
  const customRangeInvalid =
    !customFrom || !customTo || customFrom > customTo || customTo > todayInput;

  return (
    <>
      <ToggleButtonGroup
        value={value}
        exclusive
        onChange={(_event, next) => next && onChange(next)}
        size="small"
        aria-label="Date range"
        sx={{ flexWrap: "wrap" }}
      >
        {RANGE_OPTIONS.map((option) => (
          <ToggleButton key={option.value} value={option.value}>
            {option.label}
          </ToggleButton>
        ))}
      </ToggleButtonGroup>

      {value === "custom" && (
        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={2}
          alignItems={{ sm: "center" }}
        >
          <TextField
            label="From"
            type="date"
            size="small"
            value={customFrom}
            onChange={(e) => setCustomFrom(e.target.value)}
            slotProps={{
              inputLabel: { shrink: true },
              htmlInput: { max: customTo || todayInput },
            }}
          />
          <TextField
            label="To"
            type="date"
            size="small"
            value={customTo}
            onChange={(e) => setCustomTo(e.target.value)}
            slotProps={{
              inputLabel: { shrink: true },
              htmlInput: { min: customFrom || undefined, max: todayInput },
            }}
          />
          <Button
            variant="contained"
            disabled={customRangeInvalid}
            onClick={() => onApplyCustom({ from: customFrom, to: customTo })}
          >
            Show results
          </Button>
        </Stack>
      )}
    </>
  );
};

// Chip for the saved location, which can be removed to see all locations.
export const LocationFilter = ({
  homeLocation,
  locationName,
  enabled,
  onChange,
}) => {
  if (!homeLocation) return <Chip variant="outlined" label="All locations" />;

  if (enabled) {
    return (
      <Chip
        color="primary"
        label={`Location: ${locationName ?? `#${homeLocation}`}`}
        onDelete={() => onChange(false)}
      />
    );
  }

  return (
    <Stack direction="row" spacing={1} alignItems="center">
      <Chip variant="outlined" label="All locations" />
      <Button size="small" onClick={() => onChange(true)}>
        Show my location only
      </Button>
    </Stack>
  );
};
