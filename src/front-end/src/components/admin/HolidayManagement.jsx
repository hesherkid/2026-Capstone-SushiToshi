import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  CircularProgress,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  IconButton,
  MenuItem,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Tooltip,
  Typography,
} from "@mui/material";
import { Download, Edit, Trash2 } from "lucide-react";
import AppDialog from "@/components/common/AppDialog";
import ConfirmDialog from "@/components/common/ConfirmDialog";
import ManagementHeader from "@/components/common/ManagementHeader";
import {
  addHoliday,
  approveHolidays,
  deleteHoliday,
  getHolidayOptions,
  importHolidays,
  listHolidays,
  rejectHolidays,
  updateHoliday,
} from "@/services/holidayService";
import serverMessage from "@/utils/serverMessage";

const STATUSES = ["All", "Pending", "Approved", "Rejected"];
const STATUS_COLORS = { Pending: "warning", Approved: "success", Rejected: "default" };

// "2026-12-25" -> "Fri, Dec 25, 2026". Built from its parts: new Date("2026-12-25") is
// UTC midnight, which is still 24 Dec in Edmonton
export const formatDay = (day) => {
  if (!day) return "";
  const [year, month, date] = day.split("-").map(Number);
  return new Date(year, month - 1, date).toLocaleDateString("en-US", {
    weekday: "short",
    month: "short",
    day: "numeric",
    year: "numeric",
  });
};

// Add (holiday = null) or edit a holiday. onSave throws on failure; the error shows here
const HolidayDialog = ({ open, holiday, onSave, onClose }) => {
  const [date, setDate] = useState("");
  const [name, setName] = useState("");
  const [error, setError] = useState(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!open) return;
    setDate(holiday?.date ?? "");
    setName(holiday?.name ?? "");
    setError(null);
  }, [open, holiday]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSaving(true);
    setError(null);
    try {
      await onSave({ date, name: name.trim() });
      onClose();
    } catch (err) {
      setError(serverMessage(err, "Couldn't save the holiday."));
    } finally {
      setSaving(false);
    }
  };

  return (
    <AppDialog open={open} onClose={onClose} maxWidth="xs">
      <form onSubmit={handleSubmit}>
        <DialogTitle>{holiday ? "Edit Holiday" : "Add Holiday"}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {error && <Alert severity="error">{error}</Alert>}
            <TextField
              type="date"
              label="Date"
              value={date}
              onChange={(e) => setDate(e.target.value)}
              required
              slotProps={{ inputLabel: { shrink: true } }}
            />
            <TextField
              label="Name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
              slotProps={{ htmlInput: { maxLength: 100 } }}
            />
            {!holiday && (
              <Typography variant="body2" color="text.secondary">
                Holidays you add are approved straight away.
              </Typography>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="contained" disabled={saving || !date || !name.trim()}>
            Save
          </Button>
        </DialogActions>
      </form>
    </AppDialog>
  );
};

/**
 * Holidays for one province and year. "Load holidays" fetches them from canada-holidays.ca
 * as Pending; only approved holidays get holiday pricing.
 */
const HolidayManagement = () => {
  const [options, setOptions] = useState(null);
  const [province, setProvince] = useState("");
  const [year, setYear] = useState(null);
  const [includeOptional, setIncludeOptional] = useState(false);
  const [statusFilter, setStatusFilter] = useState("All");
  const [holidays, setHolidays] = useState([]);
  const [selected, setSelected] = useState([]);
  const [loadingList, setLoadingList] = useState(false);
  const [importing, setImporting] = useState(false);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(null);
  const [editing, setEditing] = useState(undefined);   // undefined = closed, null = new
  const [toDelete, setToDelete] = useState(null);

  useEffect(() => {
    getHolidayOptions()
      .then((data) => {
        setOptions(data);
        setProvince(data.default_province);
        const thisYear = new Date().getFullYear();
        setYear(Math.min(Math.max(thisYear, data.year_from), data.year_to));
        setIncludeOptional(data.include_optional);
      })
      .catch((err) => setError(serverMessage(err, "Couldn't load holiday settings.")));
  }, []);

  const refresh = useCallback(async () => {
    if (!province || !year) return;
    setLoadingList(true);
    try {
      setHolidays(await listHolidays({ province, year }));
      setSelected([]);
    } catch (err) {
      setError(serverMessage(err, "Couldn't load holidays."));
    } finally {
      setLoadingList(false);
    }
  }, [province, year]);

  useEffect(() => {
    refresh();
  }, [refresh]);

  const visible = useMemo(
    () => (statusFilter === "All" ? holidays : holidays.filter((h) => h.status === statusFilter)),
    [holidays, statusFilter],
  );

  const years = options
    ? Array.from({ length: options.year_to - options.year_from + 1 }, (_, i) => options.year_from + i)
    : [];

  const handleLoad = async () => {
    setImporting(true);
    setError(null);
    setSuccess(null);
    try {
      const result = await importHolidays({ province, year, includeOptional });
      setSuccess(
        result.added > 0
          ? `Loaded ${result.added} new holidays for ${province} ${year}` +
              (result.skipped ? ` (${result.skipped} already listed)` : "") +
              ". Approve the ones that get holiday pricing."
          : `No new holidays for ${province} ${year}: all ${result.skipped} are already listed.`,
      );
      setStatusFilter(result.added > 0 ? "Pending" : "All");
      await refresh();
    } catch (err) {
      setError(serverMessage(err, "Couldn't load holidays."));
    } finally {
      setImporting(false);
    }
  };

  const handleReview = async (review, verb) => {
    setError(null);
    setSuccess(null);
    try {
      const { updated } = await review(selected);
      setSuccess(`${verb} ${updated} holiday${updated === 1 ? "" : "s"}.`);
      await refresh();
    } catch (err) {
      setError(serverMessage(err, "Couldn't update the holidays."));
    }
  };

  const handleSave = async ({ date, name }) => {
    if (editing) {
      await updateHoliday(editing.holiday_id, { date, name });
    } else {
      await addHoliday({ province, date, name });
    }
    setSuccess(editing ? "Holiday updated." : "Holiday added and approved.");
    await refresh();
  };

  const handleDelete = async () => {
    try {
      await deleteHoliday(toDelete.holiday_id);
      setSuccess("Holiday deleted.");
      await refresh();
    } catch (err) {
      setError(serverMessage(err, "Couldn't delete the holiday."));
    }
  };

  const toggle = (id) =>
    setSelected((s) => (s.includes(id) ? s.filter((x) => x !== id) : [...s, id]));
  const allVisibleSelected = visible.length > 0 && visible.every((h) => selected.includes(h.holiday_id));
  const someVisibleSelected = visible.some((h) => selected.includes(h.holiday_id));
  const toggleAll = () => setSelected(allVisibleSelected ? [] : visible.map((h) => h.holiday_id));

  if (!options) {
    return (
      <Box sx={{ p: 3 }}>
        {error ? <Alert severity="error">{error}</Alert> : <CircularProgress />}
      </Box>
    );
  }

  return (
    <Box sx={{ width: "100%", maxWidth: 1200, mx: "auto", p: 3, textAlign: "left" }}>
      <ManagementHeader title="Holidays" addLabel="Add Holiday" onAdd={() => setEditing(null)} />
      <Typography color="text.secondary" sx={{ mb: 2 }}>
        Holiday pricing applies on approved holidays in each location's province. Loaded holidays
        wait here as Pending until you approve them.
      </Typography>

      {error && (
        <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}
      {success && (
        <Alert severity="success" onClose={() => setSuccess(null)} sx={{ mb: 2 }}>
          {success}
        </Alert>
      )}

      <Stack direction={{ xs: "column", sm: "row" }} spacing={2} alignItems={{ sm: "center" }} sx={{ mb: 2 }}>
        <TextField
          select
          label="Province"
          size="small"
          value={province}
          onChange={(e) => setProvince(e.target.value)}
          sx={{ minWidth: 120 }}
        >
          {options.provinces.map((code) => (
            <MenuItem key={code} value={code}>
              {code}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          select
          label="Year"
          size="small"
          value={year ?? ""}
          onChange={(e) => setYear(Number(e.target.value))}
          sx={{ minWidth: 120 }}
        >
          {years.map((y) => (
            <MenuItem key={y} value={y}>
              {y}
            </MenuItem>
          ))}
        </TextField>
        <FormControlLabel
          label="Include optional holidays"
          control={
            <Checkbox checked={includeOptional} onChange={(e) => setIncludeOptional(e.target.checked)} />
          }
        />
        <Button
          variant="contained"
          onClick={handleLoad}
          disabled={importing}
          startIcon={importing ? <CircularProgress size={16} color="inherit" /> : <Download size={18} />}
        >
          Load holidays
        </Button>
      </Stack>

      <Stack direction={{ xs: "column", sm: "row" }} spacing={2} justifyContent="space-between" sx={{ mb: 1 }}>
        <ToggleButtonGroup
          size="small"
          exclusive
          value={statusFilter}
          onChange={(_, value) => value && setStatusFilter(value)}
          aria-label="Status"
        >
          {STATUSES.map((status) => (
            <ToggleButton key={status} value={status}>
              {status}
            </ToggleButton>
          ))}
        </ToggleButtonGroup>
        <Stack direction="row" spacing={1}>
          <Button
            variant="contained"
            color="success"
            disabled={selected.length === 0}
            onClick={() => handleReview(approveHolidays, "Approved")}
          >
            Approve selected ({selected.length})
          </Button>
          <Button
            variant="outlined"
            color="error"
            disabled={selected.length === 0}
            onClick={() => handleReview(rejectHolidays, "Rejected")}
          >
            Reject selected ({selected.length})
          </Button>
        </Stack>
      </Stack>

      <TableContainer>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell padding="checkbox">
                <Checkbox
                  checked={allVisibleSelected}
                  indeterminate={!allVisibleSelected && someVisibleSelected}
                  onChange={toggleAll}
                  slotProps={{ input: { "aria-label": "Select all" } }}
                />
              </TableCell>
              <TableCell>Date</TableCell>
              <TableCell>Name</TableCell>
              <TableCell>Observed</TableCell>
              <TableCell>Source</TableCell>
              <TableCell>Status</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {loadingList ? (
              <TableRow>
                <TableCell colSpan={7} align="center">
                  <CircularProgress size={24} />
                </TableCell>
              </TableRow>
            ) : visible.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} align="center">
                  No {statusFilter === "All" ? "" : `${statusFilter.toLowerCase()} `}holidays for{" "}
                  {province} {year}.{statusFilter === "All" && " Press Load holidays to fetch them."}
                </TableCell>
              </TableRow>
            ) : (
              visible.map((holiday) => (
                <TableRow key={holiday.holiday_id} hover selected={selected.includes(holiday.holiday_id)}>
                  <TableCell padding="checkbox">
                    <Checkbox
                      checked={selected.includes(holiday.holiday_id)}
                      onChange={() => toggle(holiday.holiday_id)}
                      slotProps={{ input: { "aria-label": `Select ${holiday.name}` } }}
                    />
                  </TableCell>
                  <TableCell>{formatDay(holiday.date)}</TableCell>
                  <TableCell>{holiday.name}</TableCell>
                  {/* Only shown when it differs, e.g. a Saturday Christmas observed on Monday */}
                  <TableCell>
                    {holiday.observed_date && holiday.observed_date !== holiday.date
                      ? formatDay(holiday.observed_date)
                      : "—"}
                  </TableCell>
                  <TableCell>{holiday.source}</TableCell>
                  <TableCell>
                    <Chip size="small" label={holiday.status} color={STATUS_COLORS[holiday.status]} />
                  </TableCell>
                  <TableCell align="right">
                    <Tooltip title="Edit">
                      <IconButton size="small" aria-label={`Edit ${holiday.name}`} onClick={() => setEditing(holiday)}>
                        <Edit size={18} />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Delete">
                      <IconButton
                        size="small"
                        color="error"
                        aria-label={`Delete ${holiday.name}`}
                        onClick={() => setToDelete(holiday)}
                      >
                        <Trash2 size={18} />
                      </IconButton>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </TableContainer>

      <HolidayDialog
        open={editing !== undefined}
        holiday={editing}
        onSave={handleSave}
        onClose={() => setEditing(undefined)}
      />

      <ConfirmDialog
        open={!!toDelete}
        title="Delete holiday"
        message={`Delete ${toDelete?.name ?? "this holiday"}? Loading ${year} again would bring it back as Pending. To keep it out of pricing for good, reject it instead.`}
        confirmLabel="Delete"
        onConfirm={handleDelete}
        onClose={() => setToDelete(null)}
      />
    </Box>
  );
};

export default HolidayManagement;
