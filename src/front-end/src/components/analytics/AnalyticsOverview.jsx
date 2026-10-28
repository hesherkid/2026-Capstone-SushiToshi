"use client";

import { useEffect, useMemo, useState, useSyncExternalStore } from "react";
import Link from "next/link";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Grid,
  List,
  ListItem,
  ListItemText,
  Paper,
  Stack,
  Typography,
} from "@mui/material";
import api from "@/config/api";
import storage from "@/utils/storage";
import { DataCard } from "@/components/common/DataCard";

const TOP_N = 5;

const REQUESTS = {
  items: "analytics/item-performance",
  browsing: "analytics/browsing-behavior",
  seatings: "analytics/table-turnover",
  timing: "analytics/order-timing",
};

const parseLocalDate = (value) => {
  const [y, m, d] = value.split("-").map(Number);
  return new Date(y, m - 1, d);
};

const mins = (value) =>
  value === null || value === undefined ? "-" : `${value} min`;

const formatHour = (hour) =>
  `${hour % 12 === 0 ? 12 : hour % 12} ${hour < 12 ? "AM" : "PM"}`;

const getErrorMessage = (err) => {
  if (!err.response) return "Couldn't reach the server.";
  const { status, data } = err.response;
  if (status === 401)
    return "Your session has expired. Log in again to continue.";
  if (data?.errors) return Object.values(data.errors).flat().join(" ");
  if (typeof data === "string" && data) return data;
  return data?.message || data?.title || "Something went wrong.";
};

const noopSubscribe = () => () => {};
const subscribeToStorage = (callback) => {
  window.addEventListener("storage", callback);
  return () => window.removeEventListener("storage", callback);
};

const Panel = ({ title, href, error, loading, children }) => (
  <Paper sx={{ p: 2, height: "100%" }}>
    <Stack
      direction="row"
      justifyContent="space-between"
      alignItems="baseline"
      sx={{ mb: 1.5 }}
    >
      <Typography variant="h6" component="h2">
        {title}
      </Typography>
      <Button component={Link} href={href} size="small">
        Details
      </Button>
    </Stack>
    {error ? (
      <Alert severity="error">{error}</Alert>
    ) : loading && !children ? (
      <CircularProgress size={20} />
    ) : (
      children
    )}
  </Paper>
);

const Stat = ({ label, value, caption, warn = false }) => (
  <Box>
    <Typography variant="body2" color="text.secondary">
      {label}
    </Typography>
    <Typography
      variant="h5"
      component="p"
      color={warn ? "error" : "text.primary"}
    >
      {value ?? 0}
    </Typography>
    {caption && (
      <Typography variant="body2" color="text.secondary">
        {caption}
      </Typography>
    )}
  </Box>
);

const DurationCard = ({ label, duration }) => (
  <DataCard sx={{ height: "100%" }}>
    <Typography variant="body2" color="text.secondary">
      {label}
    </Typography>
    <Typography variant="h5" component="p">
      {mins(duration?.average_minutes)}
    </Typography>
    <Typography variant="body2" color="text.secondary">
      {!duration || duration.count === 0
        ? "No data yet today"
        : `Median ${mins(duration.median_minutes)} · ${duration.count} orders`}
    </Typography>
  </DataCard>
);

const RankList = ({ title, rows, valueKey, unit, emptyText }) => (
  <DataCard sx={{ height: "100%" }}>
    <Typography variant="body2" color="text.secondary" sx={{ mb: 0.5 }}>
      {title}
    </Typography>
    {rows.length === 0 ? (
      <Typography color="text.secondary">{emptyText}</Typography>
    ) : (
      <List dense disablePadding>
        {rows.map((row, i) => (
          <ListItem
            key={row.item_id}
            disableGutters
            divider={i < rows.length - 1}
          >
            <ListItemText
              primary={`${i + 1}. ${row.name}`}
              slotProps={{ primary: { noWrap: true } }}
            />
            <Typography
              variant="body2"
              color="text.secondary"
              sx={{ ml: 1, flexShrink: 0 }}
            >
              {row[valueKey]} {unit}
            </Typography>
          </ListItem>
        ))}
      </List>
    )}
  </DataCard>
);

// ---------- Page ----------

const AnalyticsOverview = () => {
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

  const [refreshTick, setRefreshTick] = useState(0);
  const [result, setResult] = useState({
    key: null,
    data: {},
    errors: {},
    at: null,
  });

  const requestKey = useMemo(() => {
    if (!isClient) return null;
    const params = { range: "today", top: TOP_N };
    if (homeLocation) params.locationId = homeLocation;
    return JSON.stringify({ params, refreshTick });
  }, [isClient, homeLocation, refreshTick]);

  useEffect(() => {
    if (!requestKey) return;
    const controller = new AbortController();
    const { params } = JSON.parse(requestKey);
    const keys = Object.keys(REQUESTS);

    Promise.allSettled(
      keys.map((k) =>
        api.get(REQUESTS[k], { params, signal: controller.signal }),
      ),
    ).then((settled) => {
      if (controller.signal.aborted) return;
      const data = {};
      const errors = {};
      settled.forEach((r, i) => {
        if (r.status === "fulfilled") data[keys[i]] = r.value.data;
        else {
          console.error(`Error fetching ${keys[i]}:`, r.reason);
          errors[keys[i]] = getErrorMessage(r.reason);
        }
      });
      setResult({ key: requestKey, data, errors, at: new Date() });
    });

    return () => controller.abort();
  }, [requestKey]);

  const loading = requestKey !== null && result.key !== requestKey;
  const { data, errors } = result;

  const anyResponse =
    data.items ?? data.browsing ?? data.seatings ?? data.timing;
  const locationName = anyResponse?.location?.name ?? "all locations";
  const range = anyResponse?.range;
  const seat = data.seatings?.summary;
  const closedVisits = seat ? seat.sessions_seated - seat.sessions_open : 0;
  const timing = data.timing;

  return (
    <Box>
      <Stack
        direction="row"
        justifyContent="space-between"
        alignItems="flex-end"
        spacing={2}
        sx={{ mb: 3 }}
      >
        <Box>
          <Typography variant="h4" component="h1">
            Today at {locationName}
          </Typography>
          {range && (
            <Typography variant="body2" color="text.secondary">
              {parseLocalDate(range.from).toLocaleDateString(undefined, {
                weekday: "long",
                month: "long",
                day: "numeric",
              })}
              {result.at &&
                `. Updated ${result.at.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" })}`}
            </Typography>
          )}
        </Box>
        <Button
          variant="outlined"
          onClick={() => setRefreshTick((t) => t + 1)}
          disabled={loading}
        >
          {loading ? "Refreshing…" : "Refresh"}
        </Button>
      </Stack>

      <Box sx={{ opacity: loading && result.key ? 0.5 : 1 }}>
        {/* ---------- How service is going ---------- */}
        <Grid container spacing={2} sx={{ mb: 2 }}>
          <Grid size={{ xs: 12, md: 6 }}>
            <Panel
              title="Seatings"
              href="/analytics/table-turnover"
              error={errors.seatings}
              loading={loading}
            >
              {seat && (
                <Grid container spacing={10}>
                  <Grid size={{ xs: 6, sm: 4 }}>
                    <Stat label="Tables seated" value={seat.sessions_seated} />
                  </Grid>
                  <Grid size={{ xs: 6, sm: 4 }}>
                    <Stat label="Guests" value={seat.guests_seated} />
                  </Grid>
                  <Grid size={{ xs: 6, sm: 4 }}>
                    <Stat label="Still seated" value={seat.sessions_open} />
                  </Grid>
                  <Grid size={{ xs: 6, sm: 4 }}>
                    <Stat
                      label={`Over ${seat.time_limit_minutes} minutes`}
                      value={
                        closedVisits === 0 ? "-" : seat.sessions_over_time_limit
                      }
                      warn={seat.sessions_over_time_limit > 0}
                      caption={
                        closedVisits === 0
                          ? "No closed visits yet today"
                          : `${Math.round((seat.sessions_over_time_limit / closedVisits) * 100)}% of closed visits`
                      }
                    />
                  </Grid>
                  <Grid size={{ xs: 12, sm: 8 }}>
                    <Stat
                      label="Busiest seating hour"
                      value={
                        data.seatings.peak_by_seatings
                          ? formatHour(data.seatings.peak_by_seatings.hour)
                          : "-"
                      }
                    />
                  </Grid>
                </Grid>
              )}
            </Panel>
          </Grid>

          <Grid size={{ xs: 12, md: 6 }}>
            <Panel
              title="Order status"
              href="/analytics/order-timing"
              error={errors.timing}
              loading={loading}
            >
              {timing && (
                <Stack spacing={2}>
                  <Typography>
                    <Typography component="span" variant="h5">
                      {timing.summary?.orders_placed ?? 0}
                    </Typography>{" "}
                    order(s) placed (not cancelled)
                  </Typography>
                  <DurationCard
                    label="Pending to processing (waiting for staff)"
                    duration={timing.pending_to_processing}
                  />
                  <DurationCard
                    label="Processing to completed (kitchen and delivery)"
                    duration={timing.processing_to_completed}
                  />
                </Stack>
              )}
            </Panel>
          </Grid>
        </Grid>

        {/* ---------- Items ---------- */}
        <Grid container spacing={2}>
          <Grid size={{ xs: 12, md: 6 }}>
            <Panel
              title="Item performance"
              href="/analytics/item-performance"
              error={errors.items}
              loading={loading}
            >
              {data.items && (
                <Grid container spacing={2}>
                  <Grid size={{ xs: 12, sm: 6 }}>
                    <RankList
                      title={`Top ${TOP_N} sellers`}
                      rows={data.items.best}
                      valueKey="units_sold"
                      unit="sold"
                      emptyText="No sales yet today."
                    />
                  </Grid>
                  <Grid size={{ xs: 12, sm: 6 }}>
                    <RankList
                      title={`Bottom ${TOP_N} sellers`}
                      rows={data.items.worst}
                      valueKey="units_sold"
                      unit="sold"
                      emptyText={`Fewer than ${TOP_N * 2} items sold, so every seller is in the top list.`}
                    />
                  </Grid>
                </Grid>
              )}
            </Panel>
          </Grid>

          <Grid size={{ xs: 12, md: 6 }}>
            <Panel
              title="Browsing behavior"
              href="/analytics/browsing-behavior"
              error={errors.browsing}
              loading={loading}
            >
              {data.browsing && (
                <Grid container spacing={2}>
                  <Grid size={{ xs: 12, sm: 6 }}>
                    <RankList
                      title="Most viewed"
                      rows={data.browsing.most_viewed}
                      valueKey="views"
                      unit="views"
                      emptyText="No menu views yet today."
                    />
                  </Grid>
                  <Grid size={{ xs: 12, sm: 6 }}>
                    <RankList
                      title="Least viewed"
                      rows={data.browsing.least_viewed}
                      valueKey="views"
                      unit="views"
                      emptyText="No menu views yet today."
                    />
                  </Grid>
                </Grid>
              )}
            </Panel>
          </Grid>
        </Grid>
      </Box>
    </Box>
  );
};

export default AnalyticsOverview;
