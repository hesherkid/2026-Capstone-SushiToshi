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
  Grid,
  Paper,
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
import {
  ExportMenu,
  downloadChartPng,
  downloadCsv,
  exportFileName,
  printReport,
} from "@/components/analytics/ReportExport";

ChartJS.register(CategoryScale, LinearScale, BarElement, Tooltip);

const REPORT = "table-turnover";

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

// Busiest time can be read by parties or guests, toggle switches between the chart views.
const BUSIEST_MEASURES = {
  seatings: {
    label: "Parties",
    unit: "parties seated",
    peakKey: "peak_by_seatings",
  },
  guests: { label: "Guests", unit: "guests seated", peakKey: "peak_by_guests" },
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

const mins = (value) =>
  value === null || value === undefined ? "-" : `${value} min`;

// 0-23 -> "6 PM". Hours are already in restaurant time.
const formatHour = (hour) =>
  `${hour % 12 === 0 ? 12 : hour % 12} ${hour < 12 ? "AM" : "PM"}`;

// API bucket ("1", "2", "6+", "unknown") chart labels.
const partySizeLabel = (bucket) => {
  if (bucket === "unknown") return "Unknown size";
  if (bucket === "1") return "1 guest";
  return `${bucket} guests`;
};

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
    "Something went wrong loading table turnover."
  );
};

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

// ---------- Export ----------

const durationRow = (label, duration, counted) => [
  label,
  duration.average_minutes,
  duration.median_minutes,
  duration.count,
  counted,
];

const buildSummaryRows = (data) => {
  const summary = data.summary;
  const gap = data.turnover_gap;
  return [
    ["Report", "Table turnover"],
    ["Period", describeRange(data.range)],
    ["Location", data.location?.name ?? "All locations"],
    [],
    ["Measure", "Value"],
    ["Parties seated", summary.sessions_seated],
    ["Parties still seated", summary.sessions_open],
    ["Guests seated", summary.guests_seated],
    [
      "Parties with unknown size (no bill)",
      summary.sessions_without_party_size,
    ],
    [
      `Visits over ${summary.time_limit_minutes} minutes`,
      summary.sessions_over_time_limit,
    ],
    [
      "Busiest hour by parties",
      data.peak_by_seatings ? formatHour(data.peak_by_seatings.hour) : null,
    ],
    ["Parties seated in that hour", data.peak_by_seatings?.seatings],
    [
      "Busiest hour by guests",
      data.peak_by_guests ? formatHour(data.peak_by_guests.hour) : null,
    ],
    ["Guests seated in that hour", data.peak_by_guests?.guests],
    [],
    ["Turnover gap", "Average (min)", "Median (min)", "Count", "Counted"],
    durationRow("Single tables", gap.tables, "gaps"),
    durationRow("Table groups", gap.groups, "gaps"),
    [],
    ["Not counted in the turnover gap", "Count"],
    [
      `Gaps over ${summary.max_gap_minutes} minutes (table sat empty)`,
      gap.gaps_over_limit,
    ],
    ["Overlapping sessions", gap.overlapping_sessions],
  ];
};

// so the CSV has the same shape everytime
const buildHourRows = (data) => [
  ["Hour", "Parties seated", "Guests seated"],
  ...data.seatings_by_hour.map((h) => [
    formatHour(h.hour),
    h.seatings,
    h.guests,
  ]),
];

const buildPartySizeRows = (data) => [
  [
    "Party size",
    "Parties seated",
    "Closed visits",
    "Average visit (min)",
    "Median visit (min)",
    `Over ${data.summary.time_limit_minutes} minutes`,
  ],
  ...data.duration_by_party_size.map((b) => [
    partySizeLabel(b.party_size),
    b.sessions,
    b.duration.count,
    b.duration.average_minutes,
    b.duration.median_minutes,
    b.over_time_limit,
  ]),
];

// ---------- Charts ----------

// Parties or guests seated per hour, trimmed to the hours that had seatings, peak highlighted.
const SeatingsByHourChart = ({ hours, measure, peak, chartRef }) => {
  const theme = useTheme();
  const withSeatings = hours.filter((h) => h.seatings > 0);

  if (withSeatings.length === 0) {
    return (
      <Typography color="text.secondary">
        No parties were seated in this period.
      </Typography>
    );
  }

  const first = withSeatings[0].hour;
  const last = withSeatings[withSeatings.length - 1].hour;
  const shown = hours.filter((h) => h.hour >= first && h.hour <= last);
  const { unit } = BUSIEST_MEASURES[measure];

  const chartData = {
    labels: shown.map((h) => formatHour(h.hour)),
    datasets: [
      {
        label: unit,
        data: shown.map((h) => h[measure]),
        backgroundColor: shown.map((h) =>
          h.hour === peak?.hour
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
    scales: { y: { beginAtZero: true, ticks: { precision: 0 } } },
  };

  return (
    <Box sx={{ height: 300 }}>
      <Bar
        id="seatings-by-hour-chart"
        ref={chartRef}
        data={chartData}
        options={options}
        role="img"
        aria-label={
          peak
            ? `${BUSIEST_MEASURES[measure].label} seated by hour. Busiest hour ${formatHour(peak.hour)} with ${peak[measure]} ${unit}.`
            : `${BUSIEST_MEASURES[measure].label} seated by hour.`
        }
      />
    </Box>
  );
};

// Average closed-visit length for each party size. Median, count and over-limit are in the tooltip.
const VisitLengthChart = ({ buckets, timeLimit, chartRef }) => {
  const theme = useTheme();
  const measured = buckets.filter((b) => b.duration.count > 0);

  if (measured.length === 0) {
    return (
      <Typography color="text.secondary">
        No visits were closed in this period.
      </Typography>
    );
  }

  const chartData = {
    labels: measured.map((b) => partySizeLabel(b.party_size)),
    datasets: [
      {
        label: "Average visit (min)",
        data: measured.map((b) => b.duration.average_minutes),
        backgroundColor: measured.map((b) =>
          b.party_size === "unknown"
            ? theme.palette.grey[400]
            : theme.palette.primary.main,
        ),
      },
    ],
  };

  const options = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { display: false },
      tooltip: {
        callbacks: {
          label: (context) => `Average ${mins(context.parsed.y)}`,
          afterLabel: (context) => {
            const b = measured[context.dataIndex];
            return [
              `Median ${mins(b.duration.median_minutes)}`,
              `${b.duration.count} closed visits`,
              `${b.over_time_limit} over ${timeLimit} minutes`,
            ];
          },
        },
      },
    },
    scales: {
      y: { beginAtZero: true, title: { display: true, text: "Minutes" } },
    },
  };

  return (
    <Box sx={{ height: 300 }}>
      <Bar
        id="visit-length-chart"
        ref={chartRef}
        data={chartData}
        options={options}
        role="img"
        aria-label={`Average visit length by party size. ${measured
          .map(
            (b) =>
              `${partySizeLabel(b.party_size)}: ${mins(b.duration.average_minutes)}`,
          )
          .join(", ")}.`}
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

const TableTurnover = () => {
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

  // Busiest time chart: parties or guests.
  const [busiestMeasure, setBusiestMeasure] = useState("seatings");

  // Last finished request. Loading is derived from it, so state is only set in request callbacks.
  const [result, setResult] = useState({ key: null, data: null, error: null });

  const todayInput = toInputDate(new Date());

  // Everything the request depends on, as one string. null = nothing to fetch yet.
  const requestKey = useMemo(() => {
    if (!isClient) return null;
    if (rangePreset === "custom" && !appliedCustom) return null;

    const params = { range: rangePreset };
    if (rangePreset === "custom") {
      params.from = appliedCustom.from;
      params.to = appliedCustom.to;
    }
    if (useLocationFilter && homeLocation) params.locationId = homeLocation;
    return JSON.stringify(params);
  }, [isClient, rangePreset, appliedCustom, useLocationFilter, homeLocation]);

  useEffect(() => {
    if (!requestKey) return;

    const controller = new AbortController();
    api
      .get("analytics/table-turnover", {
        params: JSON.parse(requestKey),
        signal: controller.signal,
      })
      .then((response) =>
        setResult({ key: requestKey, data: response.data, error: null }),
      )
      .catch((err) => {
        if (controller.signal.aborted) return; // a newer request replaced this one
        console.error("Error fetching table turnover:", err);
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

  const handleMeasureChange = (_event, value) => {
    if (value) setBusiestMeasure(value);
  };

  const customRangeInvalid =
    !customFrom || !customTo || customFrom > customTo || customTo > todayInput;

  const summary = data?.summary;
  const closedVisits = summary
    ? summary.sessions_seated - summary.sessions_open
    : 0;
  const peak = data?.[BUSIEST_MEASURES[busiestMeasure].peakKey] ?? null;

  // The Chart.js instances, for saving the charts as images.
  const hourChartRef = useRef(null);
  const visitChartRef = useRef(null);
  const hasSeatings = (summary?.sessions_seated ?? 0) > 0;
  const hasClosedVisits = (data?.duration_by_party_size ?? []).some(
    (b) => b.duration.count > 0,
  );

  const exportOptions = data && [
    {
      label: "Summary (CSV)",
      description: "Every card on this page",
      onClick: () =>
        downloadCsv(
          exportFileName(REPORT, "summary", data.range),
          buildSummaryRows(data),
        ),
    },
    {
      label: "Seatings by hour (CSV)",
      description: "Parties and guests for all 24 hours",
      onClick: () =>
        downloadCsv(
          exportFileName(REPORT, "seatings-by-hour", data.range),
          buildHourRows(data),
        ),
    },
    {
      label: "Seatings by hour chart (PNG)",
      description: hasSeatings
        ? `The chart as shown (${BUSIEST_MEASURES[busiestMeasure].label.toLowerCase()})`
        : "No parties seated in this period",
      disabled: !hasSeatings,
      onClick: () =>
        downloadChartPng(
          hourChartRef.current,
          exportFileName(REPORT, `${busiestMeasure}-by-hour`, data.range),
        ),
    },
    {
      label: "Visit length by party size (CSV)",
      description: "Average, median and count for each size",
      onClick: () =>
        downloadCsv(
          exportFileName(REPORT, "visit-length-by-party-size", data.range),
          buildPartySizeRows(data),
        ),
    },
    {
      label: "Visit length chart (PNG)",
      description: hasClosedVisits
        ? "The chart as an image"
        : "No closed visits in this period",
      disabled: !hasClosedVisits,
      onClick: () =>
        downloadChartPng(
          visitChartRef.current,
          exportFileName(REPORT, "visit-length-by-party-size", data.range),
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
          Table turnover
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

          <Box>
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
          </Box>
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
          <Typography>Loading table turnover…</Typography>
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
            {describeRange(data.range)}. {summary.sessions_seated} parties
            seated ({summary.sessions_open} still seated),{" "}
            {summary.guests_seated} guests.
          </Typography>

          {/* ---------- Seatings ---------- */}
          <Typography variant="h6" gutterBottom>
            Seatings
          </Typography>
          <Grid container spacing={2} sx={{ mb: 3 }}>
            <Grid size={{ xs: 12, sm: 6, md: 4 }}>
              <StatCard
                label="Parties seated"
                value={summary.sessions_seated}
                caption={`${summary.sessions_open} still seated`}
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 6, md: 4 }}>
              <StatCard
                label="Guests seated"
                value={summary.guests_seated}
                caption={
                  summary.sessions_without_party_size > 0
                    ? `${summary.sessions_without_party_size} ${
                        summary.sessions_without_party_size === 1
                          ? "party has"
                          : "parties have"
                      } no bill, so ${
                        summary.sessions_without_party_size === 1
                          ? "its size is"
                          : "their sizes are"
                      } unknown`
                    : "From the guest counts on each party's bills"
                }
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 6, md: 4 }}>
              <StatCard
                label={`Over ${summary.time_limit_minutes} minutes`}
                value={
                  closedVisits === 0 ? "-" : summary.sessions_over_time_limit
                }
                caption={
                  closedVisits === 0
                    ? "No closed visits in this period"
                    : `${Math.round((summary.sessions_over_time_limit / closedVisits) * 100)}% of closed visits`
                }
              />
            </Grid>
          </Grid>

          {/* ---------- Busiest time ---------- */}
          <Stack
            direction="row"
            justifyContent="space-between"
            alignItems="center"
            spacing={2}
            sx={{ mb: 1 }}
          >
            <Typography variant="h6">Busiest time</Typography>
            <ToggleButtonGroup
              value={busiestMeasure}
              exclusive
              onChange={handleMeasureChange}
              size="small"
              aria-label="Busiest time measure"
              sx={{ displayPrint: "none" }}
            >
              {Object.entries(BUSIEST_MEASURES).map(([value, m]) => (
                <ToggleButton key={value} value={value}>
                  {m.label}
                </ToggleButton>
              ))}
            </ToggleButtonGroup>
          </Stack>
          <Paper sx={{ p: 2, mb: 3 }}>
            {(data.peak_by_seatings || data.peak_by_guests) && (
              <Typography sx={{ mb: 2 }}>
                {data.peak_by_seatings && (
                  <>
                    Most parties seated:{" "}
                    <strong>{formatHour(data.peak_by_seatings.hour)}</strong> (
                    {data.peak_by_seatings.seatings}).{" "}
                  </>
                )}
                {data.peak_by_guests && (
                  <>
                    Most guests seated:{" "}
                    <strong>{formatHour(data.peak_by_guests.hour)}</strong> (
                    {data.peak_by_guests.guests}).
                  </>
                )}
              </Typography>
            )}
            <SeatingsByHourChart
              hours={data.seatings_by_hour}
              measure={busiestMeasure}
              peak={peak}
              chartRef={hourChartRef}
            />
          </Paper>

          {/* ---------- Visit length by party size ---------- */}
          <Typography variant="h6" gutterBottom>
            Visit length by party size
          </Typography>
          <Paper sx={{ p: 2, mb: 3 }}>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
              Average length of closed visits. Party size comes from the guest
              counts on each party&apos;s bills; parties with no bill are shown
              as unknown size.
            </Typography>
            <VisitLengthChart
              buckets={data.duration_by_party_size}
              timeLimit={summary.time_limit_minutes}
              chartRef={visitChartRef}
            />
          </Paper>

          {/* ---------- Turnover gap ---------- */}
          <Typography variant="h6" gutterBottom>
            Time between parties
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
            From one party leaving to the next being seated at the same table,
            on the same day. Gaps over {summary.max_gap_minutes} minutes count
            as the table sitting empty and aren&apos;t included (
            {data.turnover_gap.gaps_over_limit} in this period).
          </Typography>
          {data.turnover_gap.overlapping_sessions > 0 && (
            <Alert severity="info" sx={{ mb: 2 }}>
              {data.turnover_gap.overlapping_sessions}{" "}
              {data.turnover_gap.overlapping_sessions === 1
                ? "party was"
                : "parties were"}{" "}
              seated before the previous party&apos;s visit was closed, so{" "}
              {data.turnover_gap.overlapping_sessions === 1
                ? "it isn't"
                : "they aren't"}{" "}
              included.
            </Alert>
          )}
          <Grid container spacing={2}>
            <Grid size={{ xs: 12, sm: 6 }}>
              <DurationCard
                label="Single tables"
                duration={data.turnover_gap.tables}
                countLabel="gaps"
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 6 }}>
              <DurationCard
                label="Table groups"
                duration={data.turnover_gap.groups}
                countLabel="gaps"
              />
            </Grid>
          </Grid>
        </Box>
      )}
    </Box>
  );
};

export default TableTurnover;
