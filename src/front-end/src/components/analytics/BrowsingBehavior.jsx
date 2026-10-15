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
  nullableNumberColumn,
  rankColumn,
} from "@/components/analytics/ReportItemsGrid";
import {
  ExportMenu,
  downloadCsv,
  exportFileName,
  printReport,
} from "@/components/analytics/ReportExport";

const TOP_N = 5;
const MIN_VIEWS_OPTIONS = [5, 10, 20, 50];

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

// The API sends null when there's nothing to calculate (e.g. no views), shown as a dash.
const pct = (value) =>
  value === null || value === undefined ? "-" : `${value}%`;
const secs = (value) =>
  value === null || value === undefined ? "-" : `${value}s`;

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
    "Something went wrong loading browsing behavior."
  );
};

const byName = (a, b) => a.name.localeCompare(b.name);

// Badges for item current state.
const StatusChips = ({ item }) => (
  <>
    {item.status === "Seasonal" && (
      <Chip label="Seasonal" size="small" variant="outlined" sx={{ ml: 1 }} />
    )}
    {!item.currently_available && (
      <Chip
        label="Not available now"
        size="small"
        variant="outlined"
        color="default"
        sx={{ ml: 1 }}
      />
    )}
  </>
);

// Columns shared by several tables. Screen shows "#3", "12s" and "40%"; the CSV gets plain numbers.
const NAME_COL = {
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
const RANK_COL = rankColumn("view_rank");
const VIEWS_COL = {
  field: "views",
  headerName: "Views",
  type: "number",
  width: 90,
};
const AVG_VIEW_COL = nullableNumberColumn(
  "average_view_seconds",
  "Avg view (s)",
  secs,
);
const CONVERSION_COL = nullableNumberColumn(
  "conversion_rate",
  "Conversion (%)",
  pct,
);
const ORDERED_BY_COL = {
  field: "ordering_sessions",
  headerName: "Ordered by (tables)",
  type: "number",
  width: 150,
};
const UNAVAILABLE_VIEWS_COL = {
  field: "unavailable_views",
  headerName: "Views while unavailable",
  type: "number",
  width: 190,
};
const NOW_COL = {
  field: "available_now",
  headerName: "Now",
  width: 120,
  valueGetter: (_value, row) =>
    row.currently_available ? "Available" : "Unavailable",
};

// Short report tables: item name first, then the given columns, with an export button.
const ItemTable = ({ items, columns, ...props }) => (
  <ReportItemsGrid
    compact
    report="browsing-behavior"
    rows={items}
    columns={[NAME_COL, ...columns]}
    {...props}
  />
);

// Same wording as StatusChips, as text so it also lands in the CSV.
const itemStatus = (item) => {
  if (!item.currently_available) return "Not available now";
  if (item.status === "Seasonal") return "Seasonal";
  return "Available";
};

// "All items" grid columns.
const ALL_ITEMS_COLUMNS = [
  RANK_COL,
  { field: "name", headerName: "Item", flex: 1, minWidth: 180 },
  {
    field: "availability",
    headerName: "Status",
    width: 160,
    valueGetter: (_value, row) => itemStatus(row),
    renderCell: (params) =>
      params.value === "Available" ? (
        params.value
      ) : (
        <Chip label={params.value} size="small" variant="outlined" />
      ),
  },
  { field: "category_name", headerName: "Category", flex: 1, minWidth: 140 },
  VIEWS_COL,
  AVG_VIEW_COL,
  CONVERSION_COL,
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
    ["Report", "Browsing behavior"],
    ["Period", describeRange(data.range)],
    ["Location", data.location?.name ?? "All locations"],
    ["Category", filters.category],
    ["Tag", filters.tag],
    ["High views starts at", summary.min_views],
    [],
    ["Measure", "Value"],
    ["Views counted", summary.total_views],
    ["Tables that viewed items", summary.viewing_sessions],
    ["Items viewed", summary.items_viewed],
    ["Items considered", summary.items_considered],
    [
      `Short views left out (under ${summary.min_view_seconds} seconds)`,
      summary.short_views_excluded,
    ],
    ["Conversion (%)", summary.conversion_rate],
    [
      "Average view when ordered (seconds)",
      summary.average_view_seconds_ordered,
    ],
    [
      "Average view when not ordered (seconds)",
      summary.average_view_seconds_not_ordered,
    ],
  ];
};

// Hydration-safe browser reads: the server render and the first client render both see the
// "server" value, then React switches to the real value. Avoids a server/client markup mismatch.
const noopSubscribe = () => () => {};
const subscribeToStorage = (callback) => {
  window.addEventListener("storage", callback);
  return () => window.removeEventListener("storage", callback);
};

const BrowsingBehavior = () => {
  const isClient = useSyncExternalStore(
    noopSubscribe,
    () => true,
    () => false,
  );
  const homeLocation = useSyncExternalStore(
    subscribeToStorage,
    () => storage.get("branch-location"),
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
  const [minViews, setMinViews] = useState(10);

  const [selectedItemId, setSelectedItemId] = useState(null);

  // Last finished request. Loading is derived from it, so state is only set in request callbacks.
  const [result, setResult] = useState({ key: null, data: null, error: null });

  const todayInput = toInputDate(new Date());

  // Everything the request depends on, as one string. null = nothing to fetch yet.
  const requestKey = useMemo(() => {
    if (!isClient) return null;
    if (rangePreset === "custom" && !appliedCustom) return null;

    const params = { range: rangePreset, top: TOP_N, minViews };
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
    minViews,
  ]);

  useEffect(() => {
    if (!requestKey) return;

    const controller = new AbortController();
    api
      .get("analytics/browsing-behavior", {
        params: JSON.parse(requestKey),
        signal: controller.signal,
      })
      .then((response) =>
        setResult({ key: requestKey, data: response.data, error: null }),
      )
      .catch((err) => {
        if (controller.signal.aborted) return; // a newer request replaced this one
        console.error("Error fetching browsing behavior:", err);
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
    if (!value) return;
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
          exportFileName("browsing-behavior", "summary", data.range),
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
          Browsing behavior
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
              <InputLabel id="bb-category-label">Category</InputLabel>
              <Select
                labelId="bb-category-label"
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
              <InputLabel id="bb-tag-label">Tag</InputLabel>
              <Select
                labelId="bb-tag-label"
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

            <FormControl size="small" sx={{ minWidth: 200 }}>
              <InputLabel id="bb-minviews-label">
                High views starts at
              </InputLabel>
              <Select
                labelId="bb-minviews-label"
                label="High views starts at"
                value={minViews}
                onChange={(e) => setMinViews(e.target.value)}
              >
                {MIN_VIEWS_OPTIONS.map((n) => (
                  <MenuItem key={n} value={n}>
                    {n} views
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
          <Typography>Loading browsing behavior…</Typography>
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
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
            {describeRange(data.range)}. {summary.total_views} views from{" "}
            {summary.viewing_sessions} tables across {summary.items_viewed} of{" "}
            {summary.items_considered} items. Views under{" "}
            {summary.min_view_seconds} seconds aren&apos;t counted (
            {summary.short_views_excluded} left out).
          </Typography>
          <Typography variant="body1" sx={{ mb: 3 }}>
            {pct(summary.conversion_rate)} of item views led to an order at the
            same table. Average view:{" "}
            {secs(summary.average_view_seconds_ordered)} when ordered,{" "}
            {secs(summary.average_view_seconds_not_ordered)} when not.
          </Typography>

          {/* ---------- Item finder result ---------- */}
          {selectedItem && (
            <Alert
              severity={selectedItem.views === 0 ? "warning" : "info"}
              sx={{ mb: 3 }}
              onClose={() => setSelectedItemId(null)}
            >
              <AlertTitle>{selectedItem.name}</AlertTitle>
              {selectedItem.views === 0
                ? `Not viewed in this period. ${selectedItem.category_name}.`
                : `Rank #${selectedItem.view_rank} of ${summary.items_viewed} viewed items. ` +
                  `${selectedItem.views} views by ${selectedItem.viewing_sessions} tables, ` +
                  `${selectedItem.ordering_sessions} of which ordered it (${pct(selectedItem.conversion_rate)}). ` +
                  `Average view ${secs(selectedItem.average_view_seconds)}. ` +
                  (selectedItem.unavailable_views > 0
                    ? `${selectedItem.unavailable_views} views while unavailable. `
                    : "") +
                  `${selectedItem.category_name}.`}
            </Alert>
          )}

          {/* ---------- Most / least viewed ---------- */}
          <Grid container spacing={2} sx={{ mb: 3 }}>
            <Grid size={{ xs: 12, md: 6 }}>
              <ItemTable
                range={data.range}
                selectedItemId={selectedItemId}
                title="Most viewed"
                items={data.most_viewed}
                columns={[RANK_COL, VIEWS_COL]}
                emptyText="No items were viewed in this period."
                onSelect={setSelectedItemId}
              />
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <ItemTable
                range={data.range}
                selectedItemId={selectedItemId}
                title="Least viewed (excluding zero views)"
                items={data.least_viewed}
                columns={[RANK_COL, VIEWS_COL]}
                emptyText={`Fewer than ${TOP_N * 2} items were viewed, so every viewed item already appears in Most viewed.`}
                onSelect={setSelectedItemId}
              />
            </Grid>
          </Grid>

          {/* ---------- Viewed and ordered / not ordered ---------- */}
          <Grid container spacing={2} sx={{ mb: 3 }}>
            <Grid size={{ xs: 12, md: 6 }}>
              <ItemTable
                range={data.range}
                selectedItemId={selectedItemId}
                title={`Top ${TOP_N} viewed and ordered`}
                caption="Tables that viewed the item and ordered it."
                items={data.top_viewed_and_ordered}
                columns={[VIEWS_COL, ORDERED_BY_COL, CONVERSION_COL]}
                emptyText="No table ordered an item it viewed in this period."
                onSelect={setSelectedItemId}
              />
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <ItemTable
                range={data.range}
                selectedItemId={selectedItemId}
                title={`Top ${TOP_N} viewed but not ordered`}
                caption={`${data.viewed_not_ordered.length} items in total were viewed but not ordered.`}
                items={data.top_viewed_not_ordered}
                columns={[VIEWS_COL, AVG_VIEW_COL]}
                emptyText="Every viewed item was ordered by at least one table."
                onSelect={setSelectedItemId}
              />
            </Grid>
          </Grid>

          {/* ---------- Low conversion, high views ---------- */}
          <Box sx={{ mb: 3 }}>
            <ItemTable
              range={data.range}
              selectedItemId={selectedItemId}
              title="Low conversion with high views"
              caption={`Items with at least ${summary.min_views} views, lowest conversion first.`}
              items={data.low_conversion_high_views}
              columns={[VIEWS_COL, CONVERSION_COL, AVG_VIEW_COL]}
              emptyText={`No item reached ${summary.min_views} views in this period.`}
              onSelect={setSelectedItemId}
            />
          </Box>

          {/* ---------- Never viewed ---------- */}
          {data.never_viewed.length > 0 && (
            <Alert severity="warning" sx={{ mb: 3 }}>
              <AlertTitle>
                {data.never_viewed.length}{" "}
                {data.never_viewed.length === 1 ? "item" : "items"} never viewed
              </AlertTitle>
              <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
                {data.never_viewed.map((item) => (
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

          {/* ---------- Unavailable views ---------- */}
          <Box sx={{ mb: 3 }}>
            <ItemTable
              range={data.range}
              selectedItemId={selectedItemId}
              title="Views of unavailable items"
              caption="Viewed while the item couldn't be ordered."
              items={data.unavailable_views}
              columns={[UNAVAILABLE_VIEWS_COL, NOW_COL]}
              emptyText="No unavailable items were viewed in this period."
              onSelect={setSelectedItemId}
            />
          </Box>

          {/* ---------- All items ---------- */}
          <ReportItemsGrid
            report="browsing-behavior"
            range={data.range}
            rows={items}
            columns={ALL_ITEMS_COLUMNS}
            selectedItemId={selectedItemId}
            onSelect={setSelectedItemId}
            highlightRow={(item) =>
              item.views === 0 && item.currently_available
            }
            emptyText="No items match these filters. Try another category, tag or date range."
          />
        </Box>
      )}
    </Box>
  );
};

export default BrowsingBehavior;
