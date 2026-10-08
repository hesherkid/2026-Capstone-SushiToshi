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
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TableSortLabel,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from "@mui/material";
import api from "@/config/api";
import storage from "@/utils/storage";

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

const SORTERS = {
  rank: (a, b) =>
    (a.rank ?? Infinity) - (b.rank ?? Infinity) || a.name.localeCompare(b.name),
  name: (a, b) => a.name.localeCompare(b.name),
  category_name: (a, b) =>
    a.category_name.localeCompare(b.category_name) ||
    a.name.localeCompare(b.name),
  units_sold: (a, b) =>
    a.units_sold - b.units_sold || a.name.localeCompare(b.name),
  add_on_units: (a, b) =>
    a.add_on_units - b.add_on_units || a.name.localeCompare(b.name),
};

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

const AddOnTable = ({ items, days, onSelect }) => (
  <>
    <Typography variant="h6">Add-on items</Typography>
    <Typography variant="body2" color="text.secondary" gutterBottom>
      Sorted by how many days each item was added on, then by add-on units.
    </Typography>
    <TableContainer component={Paper}>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Item</TableCell>
            <TableCell align="right">Days added on</TableCell>
            <TableCell align="right">Add-on units</TableCell>
            <TableCell align="right">Share of item's units</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {items.length === 0 ? (
            <TableRow>
              <TableCell colSpan={4}>No add-on sales in this period.</TableCell>
            </TableRow>
          ) : (
            items.map((item) => (
              <TableRow
                key={item.item_id}
                hover
                sx={{ cursor: "pointer" }}
                onClick={() => onSelect(item.item_id)}
              >
                <TableCell>
                  {item.name}
                  <StatusChips item={item} />
                </TableCell>
                <TableCell align="right">
                  {item.add_on_days} of {days}
                </TableCell>
                <TableCell align="right">{item.add_on_units}</TableCell>
                <TableCell align="right">{item.add_on_share}%</TableCell>
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>
    </TableContainer>
  </>
);

const RankTable = ({ title, items, emptyText, onSelect }) => (
  <>
    <Typography variant="h6" gutterBottom>
      {title}
    </Typography>
    <TableContainer component={Paper}>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Rank</TableCell>
            <TableCell>Item</TableCell>
            <TableCell align="right">Units sold</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {items.length === 0 ? (
            <TableRow>
              <TableCell colSpan={3}>{emptyText}</TableCell>
            </TableRow>
          ) : (
            items.map((item) => (
              <TableRow
                key={item.item_id}
                hover
                sx={{ cursor: "pointer" }}
                onClick={() => onSelect(item.item_id)}
              >
                <TableCell>#{item.rank}</TableCell>
                <TableCell>
                  {item.name}
                  <StatusChips item={item} />
                </TableCell>
                <TableCell align="right">{item.units_sold}</TableCell>
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>
    </TableContainer>
  </>
);

// Hydration-safe browser reads: the server render and the first client render both see the
// "server" value, then React switches to the real value. Avoids a server/client markup mismatch.
const noopSubscribe = () => () => {};
const subscribeToStorage = (callback) => {
  window.addEventListener("storage", callback);
  return () => window.removeEventListener("storage", callback);
};

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
  const [sort, setSort] = useState({ field: "rank", direction: "asc" });

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

  const sortedItems = useMemo(() => {
    const sorter = SORTERS[sort.field] ?? SORTERS.rank;
    const copy = [...items]; // never sort state in place
    copy.sort(sorter);
    if (sort.direction === "desc") copy.reverse();
    return copy;
  }, [items, sort]);

  const handleSort = (field) =>
    setSort((prev) => ({
      field,
      direction:
        prev.field === field && prev.direction === "asc" ? "desc" : "asc",
    }));

  const handleRangeChange = (_event, value) => {
    if (!value) return; // ToggleButtonGroup sends null when the active button is clicked again
    setRangePreset(value);
    if (value !== "custom") setAppliedCustom(null);
  };

  const customRangeInvalid =
    !customFrom || !customTo || customFrom > customTo || customTo > todayInput;

  const summary = data?.summary;

  return (
    <Box sx={{ p: 1 }}>
      <Typography variant="h4" component="h1" gutterBottom>
        Item performance
      </Typography>

      {/* ---------- Filters ---------- */}
      <Paper sx={{ p: 2, mb: 3 }}>
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
              options={[...items].sort(SORTERS.name)}
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
        <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 2 }}>
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
              <RankTable
                title={`Top ${TOP_N} items`}
                items={data.best}
                emptyText="No items sold in this period."
                onSelect={setSelectedItemId}
              />
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <RankTable
                title={`Bottom ${TOP_N} items (with sales)`}
                items={data.worst}
                emptyText={`Fewer than ${TOP_N * 2} items sold, so every seller already appears in the top list.`}
                onSelect={setSelectedItemId}
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
            <AddOnTable
              items={data.add_ons}
              days={data.range.days}
              onSelect={setSelectedItemId}
            />
          </Box>

          {/* ---------- All items ---------- */}
          <Typography variant="h6" gutterBottom>
            All items
          </Typography>
          <TableContainer component={Paper}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  {[
                    { field: "rank", label: "Rank" },
                    { field: "name", label: "Item" },
                    { field: "category_name", label: "Category" },
                    {
                      field: "units_sold",
                      label: "Units sold",
                      align: "right",
                    },
                    {
                      field: "add_on_units",
                      label: "Add-on units",
                      align: "right",
                    },
                  ].map((col) => (
                    <TableCell key={col.field} align={col.align}>
                      <TableSortLabel
                        active={sort.field === col.field}
                        direction={
                          sort.field === col.field ? sort.direction : "asc"
                        }
                        onClick={() => handleSort(col.field)}
                      >
                        {col.label}
                      </TableSortLabel>
                    </TableCell>
                  ))}
                  <TableCell align="right">Share</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {sortedItems.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6}>
                      No items match these filters. Try another category, tag or
                      date range.
                    </TableCell>
                  </TableRow>
                ) : (
                  sortedItems.map((item) => (
                    <TableRow
                      key={item.item_id}
                      hover
                      selected={item.item_id === selectedItemId}
                      onClick={() => setSelectedItemId(item.item_id)}
                      sx={{
                        cursor: "pointer",
                        ...(item.units_sold === 0 && {
                          bgcolor: "warning.light",
                        }),
                      }}
                    >
                      <TableCell>
                        {item.rank ? `#${item.rank}` : "None"}
                      </TableCell>
                      <TableCell>
                        {item.name}
                        <StatusChips item={item} />
                      </TableCell>
                      <TableCell>{item.category_name}</TableCell>
                      <TableCell align="right">{item.units_sold}</TableCell>
                      <TableCell align="right">{item.add_on_units}</TableCell>
                      <TableCell align="right">
                        {item.share_of_units}%
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </TableContainer>
        </Box>
      )}
    </Box>
  );
};

export default ItemPerformance;
