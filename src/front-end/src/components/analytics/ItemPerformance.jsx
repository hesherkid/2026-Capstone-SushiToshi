"use client";

import { useEffect, useMemo, useState, useSyncExternalStore } from "react";
import {
  Alert,
  AlertTitle,
  Autocomplete,
  Box,
  Button,
  Chip,
  CircularProgress,
  FormControl,
  Grid,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from "@mui/material";
import api from "@/config/api";
import storage from "@/utils/storage";
import ReportItemsGrid, {
  rankColumn,
} from "@/components/analytics/ReportItemsGrid";
import {
  ExportMenu,
  downloadCsv,
  exportFileName,
  printReport,
} from "@/components/analytics/ReportExport";

const TOP_N = 5;

const RANGE_OPTIONS = [
  { value: "yesterday", label: "Yesterday" },
  { value: "today", label: "Today" },
  { value: "last7", label: "Last 7 full days before today" },
  { value: "last30", label: "Last 30 full days before today" },
  { value: "custom", label: "Date Range" },
];

const RANGE_NOTES = {
  today: "today so far",
  yesterday: "yesterday",
  last7: "the last 7 full days",
  last30: "the last 30 full days",
  custom: "both dates included",
};

// "yyyy-mm-dd" -> Date at local midnight. new Date("yyyy-mm-dd") would parse as UTC and show the wrong day/
const parseLocalDate = (value) => {
  const [y, m, d] = value.split("-").map(Number);
  return new Date(y, m - 1, d);
};

const formatDate = (value) =>
  parseLocalDate(value).toLocaleDateString(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric",
  });

const toInputDate = (date) => {
  const pad = (n) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
};

const describeRange = (range) => {
  if (!range) return "";
  const span =
    range.from === range.to
      ? formatDate(range.from)
      : `${formatDate(range.from)} - ${formatDate(range.to)}`;
  return `${span} (${RANGE_NOTES[range.preset] ?? range.preset}), ${range.time_zone} time`;
};

// Handles { message }, ASP.NET ValidationProblem { errors }, plain strings and network failures.
const getErrorMessage = (err) => {
  if (!err.response) {
    return "Couldn't reach the server. Check your connection and try again.";
  }
  const { status, data } = err.response;
  if (status === 401)
    return "Your session has expired. Log in again to continue.";
  if (data?.errors) return Object.values(data.errors).flat().join(" ");
  if (typeof data === "string" && data) return data;
  return (
    data?.message ||
    data?.title ||
    "Something went wrong loading item performance."
  );
};

const byName = (a, b) => a.name.localeCompare(b.name);

// Badges for item current state. No longer available items are shown because they sold in this period.
const StatusChips = ({ item }) => (
  <>
    {item.status === "Seasonal" && (
      <Chip label="Seasonal" size="small" variant="outlined" sx={{ ml: 1 }} />
    )}
    {!item.currently_available && (
      <Chip
        label="No longer available"
        size="small"
        variant="outlined"
        color="default"
        sx={{ ml: 1 }}
      />
    )}
  </>
);

// Same wording as StatusChips, as text so it also lands in the CSV.
const itemStatus = (item) => {
  if (!item.currently_available) return "No longer available";
  if (item.status === "Seasonal") return "Seasonal";
  return "Available";
};

// "All items" grid columns. Screen shows "#3" and "12.5%"; the CSV gets plain numbers.
const ALL_ITEMS_COLUMNS = [
  rankColumn("rank"),
  { field: "name", headerName: "Item", flex: 1, minWidth: 180 },
  {
    field: "availability",
    headerName: "Status",
    width: 170,
    valueGetter: (_value, row) => itemStatus(row),
    renderCell: (params) =>
      params.value === "Available" ? (
        params.value
      ) : (
        <Chip label={params.value} size="small" variant="outlined" />
      ),
  },
  { field: "category_name", headerName: "Category", flex: 1, minWidth: 140 },
  { field: "units_sold", headerName: "Units sold", type: "number", width: 110 },
  {
    field: "add_on_units",
    headerName: "Add-on units",
    type: "number",
    width: 120,
  },
  {
    field: "share_of_units",
    headerName: "Share (%)",
    type: "number",
    width: 110,
    renderCell: (params) => `${params.value}%`,
  },
];

// Item name with its status badges. The CSV gets the name only; the All items grid has a Status column.
const NAME_COLUMN = {
  field: "name",
  headerName: "Item",
  flex: 1,
  minWidth: 180,
  renderCell: (params) => (
    <>
      {params.value}
      <StatusChips item={params.row} />
    </>
  ),
};

// Top / bottom 5 tables.
const RANK_TABLE_COLUMNS = [
  rankColumn("rank"),
  NAME_COLUMN,
  { field: "units_sold", headerName: "Units sold", type: "number", width: 110 },
];

// Add-on table. Screen shows "3 of 7"; the CSV gets the number of days, with the total in the header.
const addOnColumns = (days) => [
  NAME_COLUMN,
  {
    field: "add_on_days",
    headerName: `Days added on (of ${days})`,
    type: "number",
    width: 170,
    renderCell: (params) => `${params.value} of ${days}`,
  },
  {
    field: "add_on_units",
    headerName: "Add-on units",
    type: "number",
    width: 120,
  },
  {
    field: "add_on_share",
    headerName: "Share of item's units (%)",
    type: "number",
    width: 190,
    renderCell: (params) => `${params.value}%`,
  },
];

// ---------- Export ----------

// Names of the selected category and tag filters, for the top of the CSV.
const filterNames = (data, categoryId, tagId) => ({
  category:
    data.filters?.categories?.find((c) => String(c.id) === String(categoryId))
      ?.name ?? "All categories",
  tag:
    data.filters?.tags?.find((t) => String(t.tag_id) === String(tagId))?.name ??
    "All tags",
});

// The report's period, location and item filters, then its summary figures.
const buildSummaryRows = (data, filters) => {
  const summary = data.summary;
  return [
    ["Report", "Item performance"],
    ["Period", describeRange(data.range)],
    ["Location", data.location?.name ?? "All locations"],
    ["Category", filters.category],
    ["Tag", filters.tag],
    [],
    ["Measure", "Value"],
    ["Days in period", data.range.days],
    ["Units sold", summary.total_units_sold],
    ["Add-on units", summary.total_add_on_units],
    ["Items considered", summary.items_considered],
    ["Items with sales", summary.items_with_sales],
    ["Items with no sales", summary.items_with_zero_sales],
  ];
};

// Hydration-safe browser reads: the server render and the first client render both see the
// "server" value, then React switches to the real value. Avoids a server/client markup mismatch.
const noopSubscribe = () => () => {};
const subscribeToStorage = (callback) => {
  window.addEventListener("storage", callback);
  return () => window.removeEventListener("storage", callback);
};

// useSyncExternalStore for hydration-safe client detection and storage subscription
const ItemPerformance = () => {
  const isClient = useSyncExternalStore(
    noopSubscribe,
    () => true,
    () => false,
  );
  const homeLocation = useSyncExternalStore(
    subscribeToStorage,
    () => storage.get("branch-location"), // same key OrderTiming uses
    () => null,
  );

  // Filters
  const [rangePreset, setRangePreset] = useState("last7");
  const [customFrom, setCustomFrom] = useState("");
  const [customTo, setCustomTo] = useState("");
  const [appliedCustom, setAppliedCustom] = useState(null);
  const [useLocationFilter, setUseLocationFilter] = useState(true);
  const [categoryId, setCategoryId] = useState("");
  const [tagId, setTagId] = useState("");

  // Table + item finder
  const [selectedItemId, setSelectedItemId] = useState(null);

  // Last finished request. Loading is derived from it, so state is only set in request callbacks.
  const [result, setResult] = useState({ key: null, data: null, error: null });

  const todayInput = toInputDate(new Date());

  // Everything the request depends on, as one string. null = nothing to fetch yet.
  const requestKey = useMemo(() => {
    if (!isClient) return null;
    if (rangePreset === "custom" && !appliedCustom) return null;

    const params = { range: rangePreset, top: TOP_N };
    if (rangePreset === "custom") {
      params.from = appliedCustom.from;
      params.to = appliedCustom.to;
    }
    if (useLocationFilter && homeLocation) params.locationId = homeLocation;
    if (categoryId) params.categoryId = categoryId;
    if (tagId) params.tagId = tagId;
    return JSON.stringify(params);
  }, [
    isClient,
    rangePreset,
    appliedCustom,
    useLocationFilter,
    homeLocation,
    categoryId,
    tagId,
  ]);

  useEffect(() => {
    if (!requestKey) return;

    const controller = new AbortController();
    api
      .get("analytics/item-performance", {
        params: JSON.parse(requestKey),
        signal: controller.signal,
      })
      .then((response) =>
        setResult({ key: requestKey, data: response.data, error: null }),
      )
      .catch((err) => {
        if (controller.signal.aborted) return; // a newer request replaced this one
        console.error("Error fetching item performance:", err);
        // 401s are redirected to login by the response interceptor in src/config/api.js.
        setResult((prev) => ({
          key: requestKey,
          data: prev.data,
          error: getErrorMessage(err),
        }));
      });

    return () => controller.abort();
  }, [requestKey]);

  const loading = requestKey !== null && result.key !== requestKey;
  const data = result.data;
  const error = result.key === requestKey ? result.error : null;

  const items = data?.items ?? [];
  const selectedItem = items.find((i) => i.item_id === selectedItemId) ?? null;

  const handleRangeChange = (_event, value) => {
    if (!value) return; // ToggleButtonGroup sends null when the active button is clicked again
    setRangePreset(value);
    if (value !== "custom") setAppliedCustom(null);
  };

  const customRangeInvalid =
    !customFrom || !customTo || customFrom > customTo || customTo > todayInput;

  const summary = data?.summary;

  const exportOptions = data && [
    {
      label: "Summary (CSV)",
      description: "The figures at the top of this report",
      onClick: () =>
        downloadCsv(
          exportFileName("item-performance", "summary", data.range),
          buildSummaryRows(data, filterNames(data, categoryId, tagId)),
        ),
    },
    {
      label: "Print or save as PDF",
      description: "The whole report, without the filters",
      onClick: printReport,
    },
  ];

  return (
    <Box sx={{ p: 1 }}>
      <Stack
        direction="row"
        justifyContent="space-between"
        alignItems="center"
        spacing={2}
        sx={{ mb: 1 }}
      >
        <Typography variant="h4" component="h1">
          Item performance
        </Typography>
        {data && !error && !loading && (
          <ExportMenu label="Export report" options={exportOptions} />
        )}
      </Stack>

      {/* ---------- Filters (not printed) ---------- */}
      <Paper sx={{ p: 2, mb: 3, displayPrint: "none" }}>
        <Stack spacing={2}>
          <ToggleButtonGroup
            value={rangePreset}
            exclusive
            onChange={handleRangeChange}
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

          {rangePreset === "custom" && (
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
                onClick={() =>
                  setAppliedCustom({ from: customFrom, to: customTo })
                }
              >
                Show results
              </Button>
            </Stack>
          )}

          <Stack
            direction={{ xs: "column", md: "row" }}
            spacing={2}
            alignItems={{ md: "center" }}
          >
            {homeLocation ? (
              useLocationFilter ? (
                <Chip
                  color="primary"
                  label={`Location: ${data?.location?.name ?? `#${homeLocation}`}`}
                  onDelete={() => setUseLocationFilter(false)}
                />
              ) : (
                <Stack direction="row" spacing={1} alignItems="center">
                  <Chip variant="outlined" label="All locations" />
                  <Button
                    size="small"
                    onClick={() => setUseLocationFilter(true)}
                  >
                    Show my location only
                  </Button>
                </Stack>
              )
            ) : (
              <Chip variant="outlined" label="All locations" />
            )}

            <FormControl size="small" sx={{ minWidth: 180 }}>
              <InputLabel id="ip-category-label">Category</InputLabel>
              <Select
                labelId="ip-category-label"
                label="Category"
                value={categoryId}
                onChange={(e) => setCategoryId(e.target.value)}
              >
                <MenuItem value="">All categories</MenuItem>
                {(data?.filters?.categories ?? []).map((c) => (
                  <MenuItem key={c.id} value={c.id}>
                    {c.name}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            <FormControl size="small" sx={{ minWidth: 180 }}>
              <InputLabel id="ip-tag-label">Tag</InputLabel>
              <Select
                labelId="ip-tag-label"
                label="Tag"
                value={tagId}
                onChange={(e) => setTagId(e.target.value)}
              >
                <MenuItem value="">All tags</MenuItem>
                {(data?.filters?.tags ?? []).map((t) => (
                  <MenuItem key={t.tag_id} value={t.tag_id}>
                    {t.name}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            <Autocomplete
              size="small"
              sx={{ minWidth: 240, flexGrow: 1 }}
              options={[...items].sort(byName)}
              getOptionLabel={(option) => option.name}
              isOptionEqualToValue={(option, value) =>
                option.item_id === value.item_id
              }
              value={selectedItem}
              onChange={(_e, value) =>
                setSelectedItemId(value?.item_id ?? null)
              }
              renderInput={(params) => (
                <TextField {...params} label="Find an item" />
              )}
            />
          </Stack>
        </Stack>
      </Paper>

      {/* ---------- Status ---------- */}
      {loading && (
        <Stack
          direction="row"
          spacing={1}
          alignItems="center"
          sx={{ mb: 2, displayPrint: "none" }}
        >
          <CircularProgress size={20} />
          <Typography>Loading item performance…</Typography>
        </Stack>
      )}

      {rangePreset === "custom" && !appliedCustom && !loading && (
        <Alert severity="info" sx={{ mb: 2 }}>
          Pick a start and end date, then select Show results.
        </Alert>
      )}

      {error && (
        <Alert severity="error" sx={{ mb: 2, borderRadius: 2 }}>
          {error}
        </Alert>
      )}

      {data && !error && (
        <Box sx={{ opacity: loading ? 0.5 : 1 }}>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            {describeRange(data.range)}. {summary.total_units_sold} units sold
            across {summary.items_with_sales} of {summary.items_considered}{" "}
            items, {summary.total_add_on_units} of them as add-ons.
          </Typography>

          {/* ---------- Item finder result ---------- */}
          {selectedItem && (
            <Alert
              severity={selectedItem.units_sold === 0 ? "warning" : "info"}
              sx={{ mb: 3 }}
              onClose={() => setSelectedItemId(null)}
            >
              <AlertTitle>{selectedItem.name}</AlertTitle>
              {selectedItem.units_sold === 0
                ? `No sales in this period. ${selectedItem.category_name}.`
                : `Rank #${selectedItem.rank} of ${summary.items_with_sales} items with sales. ` +
                  `${selectedItem.units_sold} units, ${selectedItem.share_of_units}% of units sold. ` +
                  (selectedItem.add_on_units > 0
                    ? `${selectedItem.add_on_units} units (${selectedItem.add_on_share}%) were add-ons, ` +
                      `on ${selectedItem.add_on_days} of ${data.range.days} days. `
                    : "") +
                  `${selectedItem.category_name}.`}
              {!selectedItem.currently_available &&
                " This item is no longer available but sold during this period."}
            </Alert>
          )}

          {/* ---------- Best / worst ---------- */}
          <Grid container spacing={2} sx={{ mb: 3 }}>
            <Grid size={{ xs: 12, md: 6 }}>
              <ReportItemsGrid
                compact
                title={`Top ${TOP_N} items`}
                report="item-performance"
                range={data.range}
                rows={data.best}
                columns={RANK_TABLE_COLUMNS}
                selectedItemId={selectedItemId}
                onSelect={setSelectedItemId}
                emptyText="No items sold in this period."
              />
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <ReportItemsGrid
                compact
                title={`Bottom ${TOP_N} items (with sales)`}
                report="item-performance"
                range={data.range}
                rows={data.worst}
                columns={RANK_TABLE_COLUMNS}
                selectedItemId={selectedItemId}
                onSelect={setSelectedItemId}
                emptyText={`Fewer than ${TOP_N * 2} items sold, so every seller already appears in the top list.`}
              />
            </Grid>
          </Grid>

          {/* ---------- Zero sales ---------- */}
          {data.zero_sales.length > 0 && (
            <Alert severity="warning" sx={{ mb: 3 }}>
              <AlertTitle>
                {data.zero_sales.length}{" "}
                {data.zero_sales.length === 1 ? "item" : "items"} with no sales
              </AlertTitle>
              <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
                {data.zero_sales.map((item) => (
                  <Chip
                    key={item.item_id}
                    label={
                      item.status === "Seasonal"
                        ? `${item.name} (seasonal)`
                        : item.name
                    }
                    size="small"
                    onClick={() => setSelectedItemId(item.item_id)}
                  />
                ))}
              </Stack>
            </Alert>
          )}

          {/* ---------- Add-ons ---------- */}
          <Box sx={{ mb: 3 }}>
            <ReportItemsGrid
              compact
              title="Add-on items"
              caption="Sorted by how many days each item was added on, then by add-on units."
              report="item-performance"
              range={data.range}
              rows={data.add_ons}
              columns={addOnColumns(data.range.days)}
              selectedItemId={selectedItemId}
              onSelect={setSelectedItemId}
              emptyText="No add-on sales in this period."
            />
          </Box>

          {/* ---------- All items ---------- */}
          <ReportItemsGrid
            report="item-performance"
            range={data.range}
            rows={items}
            columns={ALL_ITEMS_COLUMNS}
            selectedItemId={selectedItemId}
            onSelect={setSelectedItemId}
            highlightRow={(item) => item.units_sold === 0}
            emptyText="No items match these filters. Try another category, tag or date range."
          />
        </Box>
      )}
    </Box>
  );
};

export default ItemPerformance;
