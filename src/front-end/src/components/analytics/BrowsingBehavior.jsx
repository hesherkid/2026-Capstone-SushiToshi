"use client";

import { useEffect, useMemo, useState } from "react";
import {
  Alert,
  Autocomplete,
  Box,
  CircularProgress,
  FormControl,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import api from "@/config/api";
import {
  ExportMenu,
  downloadCsv,
  exportFileName,
  printReport,
} from "@/components/analytics/ReportExport";
import {
  DateRangeFilter,
  LocationFilter,
} from "@/components/analytics/ReportFilters";
import {
  getErrorMessage,
  useHomeLocation,
  useIsClient,
} from "@/components/analytics/reportUtils";
import BrowsingResults from "@/components/analytics/browsing-behavior/BrowsingResults";
import {
  buildSummaryRows,
  filterNames,
} from "@/components/analytics/browsing-behavior/exportRows";

const TOP_N = 5;
const MIN_VIEWS_OPTIONS = [5, 10, 20, 50];

const byName = (a, b) => a.name.localeCompare(b.name);

const BrowsingBehavior = () => {
  const isClient = useIsClient();
  const homeLocation = useHomeLocation();

  // Filters
  const [rangePreset, setRangePreset] = useState("last7");
  const [appliedCustom, setAppliedCustom] = useState(null);
  const [useLocationFilter, setUseLocationFilter] = useState(true);
  const [categoryId, setCategoryId] = useState("");
  const [tagId, setTagId] = useState("");
  const [minViews, setMinViews] = useState(10);

  const [selectedItemId, setSelectedItemId] = useState(null);

  // Last finished request. Loading is derived from it, so state is only set in request callbacks.
  const [result, setResult] = useState({ key: null, data: null, error: null });

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
          error: getErrorMessage(
            err,
            "Something went wrong loading browsing behavior.",
          ),
        }));
      });

    return () => controller.abort();
  }, [requestKey]);

  const loading = requestKey !== null && result.key !== requestKey;
  const data = result.data;
  const error = result.key === requestKey ? result.error : null;

  const items = data?.items ?? [];
  const selectedItem = items.find((i) => i.item_id === selectedItemId) ?? null;

  const handleRangeChange = (value) => {
    setRangePreset(value);
    if (value !== "custom") setAppliedCustom(null);
  };

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
    <Box>
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
          <DateRangeFilter
            value={rangePreset}
            onChange={handleRangeChange}
            onApplyCustom={setAppliedCustom}
          />

          <Stack
            direction={{ xs: "column", md: "row" }}
            spacing={2}
            alignItems={{ md: "center" }}
          >
            <LocationFilter
              homeLocation={homeLocation}
              locationName={data?.location?.name}
              enabled={useLocationFilter}
              onChange={setUseLocationFilter}
            />

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
        <BrowsingResults
          data={data}
          loading={loading}
          topN={TOP_N}
          selectedItem={selectedItem}
          selectedItemId={selectedItemId}
          onSelect={setSelectedItemId}
        />
      )}
    </Box>
  );
};

export default BrowsingBehavior;
