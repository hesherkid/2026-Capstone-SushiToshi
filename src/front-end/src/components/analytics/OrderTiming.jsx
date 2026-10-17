"use client";

import {
  useEffect,
  useMemo,
  useRef,
  useState,
  useSyncExternalStore,
} from "react";
import {
  Alert,
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
import { useTheme } from "@mui/material/styles";
import { Bar } from "react-chartjs-2";
import {
  BarElement,
  CategoryScale,
  Chart as ChartJS,
  LinearScale,
  Tooltip,
} from "chart.js";
import api from "@/config/api";
import storage from "@/utils/storage";
import { DataCard } from "@/components/common/DataCard";
import ReportItemsGrid, {
  nullableNumberColumn,
  rankColumn,
} from "@/components/analytics/ReportItemsGrid";
import {
  ExportMenu,
  downloadChartPng,
  downloadCsv,
  exportFileName,
  printReport,
} from "@/components/analytics/ReportExport";

ChartJS.register(CategoryScale, LinearScale, BarElement, Tooltip);

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

// "yyyy-mm-dd" -> Date at local midnight. new Date("yyyy-mm-dd") would parse as UTC and show the wrong day.
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

// The API sends null when there's nothing to calculate (e.g. no orders), shown as a dash.
const mins = (value) =>
  value === null || value === undefined ? "-" : `${value} min`;

// 0-23 -> "6 PM". Hours are already in restaurant time.
const formatHour = (hour) =>
  `${hour % 12 === 0 ? 12 : hour % 12} ${hour < 12 ? "AM" : "PM"}`;

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
    data?.message || data?.title || "Something went wrong loading order timing."
  );
};

// One figure with a label and a short explanation underneath.
const StatCard = ({ label, value, caption }) => (
  <DataCard sx={{ height: "100%" }}>
    <Typography variant="body2" color="text.secondary">
      {label}
    </Typography>
    <Typography variant="h5" component="p">
      {value}
    </Typography>
    {caption && (
      <Typography variant="body2" color="text.secondary">
        {caption}
      </Typography>
    )}
  </DataCard>
);

// Average as the headline, median and count underneath.
const DurationCard = ({ label, duration, countLabel }) => (
  <StatCard
    label={label}
    value={mins(duration.average_minutes)}
    caption={
      duration.count === 0
        ? "No data in this period"
        : `Median ${mins(duration.median_minutes)} · ${duration.count} ${countLabel}`
    }
  />
);

// Slowest / fastest tables. Screen shows "#3" and "12.5 min"; the CSV gets plain numbers.
const ITEM_TIMING_COLUMNS = [
  rankColumn("rank"),
  { field: "name", headerName: "Item", flex: 1, minWidth: 160 },
  { field: "category_name", headerName: "Category", width: 140 },
  { field: "deliveries", headerName: "Deliveries", type: "number", width: 110 },
  nullableNumberColumn("median_minutes", "Median (min)", mins),
  nullableNumberColumn("average_minutes", "Average (min)", mins),
];

// ---------- Export ----------

const durationRow = (label, duration, counted) => [
  label,
  duration.average_minutes,
  duration.median_minutes,
  duration.count,
  counted,
];

// Every card on the page, plus the report's period and location, as one CSV.
const buildSummaryRows = (data) => {
  const summary = data.summary;
  return [
    ["Report", "Order timing"],
    ["Period", describeRange(data.range)],
    ["Location", data.location?.name ?? "All locations"],
    [],
    ["Measure", "Average (min)", "Median (min)", "Count", "Counted"],
    durationRow("Visit length", data.session_duration, "closed visits"),
    durationRow("Seated to first order", data.time_to_first_order, "tables"),
    durationRow("Gap between orders", data.gap_between_orders, "gaps"),
    durationRow(
      "Pending to processing (waiting for staff)",
      data.pending_to_processing,
      "orders",
    ),
    durationRow(
      "Processing to completed (kitchen and delivery)",
      data.processing_to_completed,
      "orders",
    ),
    [],
    ["Measure", "Value"],
    ["Tables seated", summary.sessions_started],
    ["Tables still open", summary.sessions_open],
    [
      `Visits over ${summary.time_limit_minutes} minutes`,
      summary.sessions_over_time_limit,
    ],
    ["Orders placed (not cancelled)", summary.orders_placed],
    ["Orders per table (tables that ordered)", data.average_orders_per_session],
    ["Busiest hour", data.peak_hour ? formatHour(data.peak_hour.hour) : null],
    ["Orders in busiest hour", data.peak_hour?.orders],
    ["Orders missing approval time", summary.orders_missing_approval_time],
  ];
};

// All 24 hours, so the CSV has the same shape every time.
const buildHourRows = (data) => [
  ["Hour", "Orders"],
  ...data.orders_by_hour.map((h) => [formatHour(h.hour), h.orders]),
];

// Orders per hour, trimmed to the hours that had orders, with the peak hour highlighted.
const OrdersByHourChart = ({ hours, peakHour, chartRef }) => {
  const theme = useTheme();
  const withOrders = hours.filter((h) => h.orders > 0);

  if (withOrders.length === 0) {
    return (
      <Typography color="text.secondary">
        No orders were placed in this period.
      </Typography>
    );
  }

  const first = withOrders[0].hour;
  const last = withOrders[withOrders.length - 1].hour;
  const shown = hours.filter((h) => h.hour >= first && h.hour <= last);

  const chartData = {
    labels: shown.map((h) => formatHour(h.hour)),
    datasets: [
      {
        label: "Orders",
        data: shown.map((h) => h.orders),
        backgroundColor: shown.map((h) =>
          h.hour === peakHour?.hour
            ? theme.palette.primary.main
            : theme.palette.grey[400],
        ),
      },
    ],
  };

  const options = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { display: false } },
    scales: {
      y: { beginAtZero: true, ticks: { precision: 0 } },
    },
  };

  return (
    <Box sx={{ position: "relative", height: 300 }}>
      <Bar
        ref={chartRef}
        data={chartData}
        options={options}
        role="img"
        aria-label={`Orders by hour. Busiest hour ${formatHour(peakHour.hour)} with ${peakHour.orders} orders.`}
      />
    </Box>
  );
};

// Hydration-safe browser reads: the server render and the first client render both see the
// "server" value, then React switches to the real value. Avoids a server/client markup mismatch.
const noopSubscribe = () => () => {};
const subscribeToStorage = (callback) => {
  window.addEventListener("storage", callback);
  return () => window.removeEventListener("storage", callback);
};

const OrderTiming = () => {
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
      .get("analytics/order-timing", {
        params: JSON.parse(requestKey),
        signal: controller.signal,
      })
      .then((response) =>
        setResult({ key: requestKey, data: response.data, error: null }),
      )
      .catch((err) => {
        if (controller.signal.aborted) return; // a newer request replaced this one
        console.error("Error fetching order timing:", err);
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

  const handleRangeChange = (_event, value) => {
    if (!value) return; // ToggleButtonGroup sends null when the active button is clicked again
    setRangePreset(value);
    if (value !== "custom") setAppliedCustom(null);
  };

  const customRangeInvalid =
    !customFrom || !customTo || customFrom > customTo || customTo > todayInput;

  const summary = data?.summary;
  const closedSessions = data?.session_duration.count ?? 0;

  // The Chart.js instance, for saving the chart as an image.
  const chartRef = useRef(null);
  const hasOrders = (data?.orders_by_hour ?? []).some((h) => h.orders > 0);

  const exportOptions = data && [
    {
      label: "Summary (CSV)",
      description: "Every card on this page",
      onClick: () =>
        downloadCsv(
          exportFileName("order-timing", "summary", data.range),
          buildSummaryRows(data),
        ),
    },
    {
      label: "Orders by hour (CSV)",
      description: "The chart's numbers",
      onClick: () =>
        downloadCsv(
          exportFileName("order-timing", "orders-by-hour", data.range),
          buildHourRows(data),
        ),
    },
    {
      label: "Orders by hour chart (PNG)",
      description: hasOrders
        ? "The chart as an image"
        : "No orders in this period",
      disabled: !hasOrders,
      onClick: () =>
        downloadChartPng(
          chartRef.current,
          exportFileName("order-timing", "orders-by-hour", data.range),
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
          Order timing
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
              <InputLabel id="ot-category-label">Category</InputLabel>
              <Select
                labelId="ot-category-label"
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
              <InputLabel id="ot-tag-label">Tag</InputLabel>
              <Select
                labelId="ot-tag-label"
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

            <Typography variant="body2" color="text.secondary">
              Category and tag filter the slowest and fastest items only.
            </Typography>
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
          <Typography>Loading order timing…</Typography>
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
            {describeRange(data.range)}. {summary.sessions_started} tables
            seated ({summary.sessions_open} still open), {summary.orders_placed}{" "}
            orders placed. Cancelled orders aren&apos;t counted.
          </Typography>

          {/* ---------- Visits ---------- */}
          <Typography variant="h6" gutterBottom>
            Visits
          </Typography>
          <Grid container spacing={2} sx={{ mb: 3 }}>
            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <DurationCard
                label="Average visit"
                duration={data.session_duration}
                countLabel="closed visits"
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <StatCard
                label={`Over ${summary.time_limit_minutes} minutes`}
                value={
                  closedSessions === 0 ? "-" : summary.sessions_over_time_limit
                }
                caption={
                  closedSessions === 0
                    ? "No closed visits in this period"
                    : `${Math.round((summary.sessions_over_time_limit / closedSessions) * 100)}% of closed visits`
                }
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <DurationCard
                label="Seated to first order"
                duration={data.time_to_first_order}
                countLabel="tables"
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <DurationCard
                label="Gap between orders"
                duration={data.gap_between_orders}
                countLabel="gaps"
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <StatCard
                label="Orders per table"
                value={data.average_orders_per_session ?? "-"}
                caption="Average, for tables that ordered"
              />
            </Grid>
          </Grid>

          {/* ---------- Busiest time ---------- */}
          <Typography variant="h6" gutterBottom>
            Orders by hour
          </Typography>
          <Paper sx={{ p: 2, mb: 3 }}>
            {data.peak_hour && (
              <Typography sx={{ mb: 2 }}>
                Busiest hour: <strong>{formatHour(data.peak_hour.hour)}</strong>{" "}
                with {data.peak_hour.orders} orders.
              </Typography>
            )}
            <OrdersByHourChart
              hours={data.orders_by_hour}
              peakHour={data.peak_hour}
              chartRef={chartRef}
            />
          </Paper>

          {/* ---------- Order status timings ---------- */}
          <Typography variant="h6" gutterBottom>
            Order status
          </Typography>
          {summary.orders_missing_approval_time > 0 && (
            <Alert severity="info" sx={{ mb: 2 }}>
              {summary.orders_missing_approval_time}{" "}
              {summary.orders_missing_approval_time === 1
                ? "order was"
                : "orders were"}{" "}
              approved before approval times were recorded, so{" "}
              {summary.orders_missing_approval_time === 1
                ? "it isn't"
                : "they aren't"}{" "}
              included below.
            </Alert>
          )}
          <Grid container spacing={2} sx={{ mb: 3 }}>
            <Grid size={{ xs: 12, sm: 6 }}>
              <DurationCard
                label="Pending to processing (waiting for staff)"
                duration={data.pending_to_processing}
                countLabel="orders"
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 6 }}>
              <DurationCard
                label="Processing to completed (kitchen and delivery)"
                duration={data.processing_to_completed}
                countLabel="orders"
              />
            </Grid>
          </Grid>

          {/* ---------- Slowest / fastest items ---------- */}
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
            Time from staff approval to the item being delivered. Items need at
            least {summary.min_deliveries} deliveries to be ranked;{" "}
            {summary.items_ranked} items qualify.
          </Typography>
          <Grid container spacing={2}>
            <Grid size={{ xs: 12, md: 6 }}>
              <ReportItemsGrid
                compact
                title={`Slowest ${TOP_N} items`}
                report="order-timing"
                range={data.range}
                rows={data.slowest_items}
                columns={ITEM_TIMING_COLUMNS}
                emptyText={`No items had ${summary.min_deliveries} or more deliveries in this period.`}
              />
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <ReportItemsGrid
                compact
                title={`Fastest ${TOP_N} items`}
                report="order-timing"
                range={data.range}
                rows={data.fastest_items}
                columns={ITEM_TIMING_COLUMNS}
                emptyText={`Fewer than ${TOP_N * 2} items qualify, so every ranked item already appears in Slowest.`}
              />
            </Grid>
          </Grid>
        </Box>
      )}
    </Box>
  );
};

export default OrderTiming;
