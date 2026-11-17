import api from "@/config/api";
import React, { useState, useEffect } from "react";
import {
  Box,
  Stack,
  Typography,
  Alert,
  DialogActions,
  DialogContent,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  TextField,
  Switch,
  FormControlLabel,
  FormHelperText,
  Button,
  CircularProgress,
} from "@mui/material";
import AppDialog from "@/components/common/AppDialog";
import DialogHeader from "@/components/common/DialogHeader";
import ConfirmDialog from "@/components/common/ConfirmDialog";
import ManagementGrid from "@/components/common/ManagementGrid";
import { actionsColumn } from "@/components/common/RowActions";
import { Edit, Trash2 } from "lucide-react";

// "all" in the form = default row (location_id null). Not "", which the
// Select treats as nothing chosen and shows blank.
const ALL_LOCATIONS = "all";

const FORM_ID = "menu-assignment-form";

const emptyForm = {
  menu_id: "",
  location_id: ALL_LOCATIONS,
  price: "0",
  is_add_on: false,
  status: "available",
  adult_limit: 0,
  child_limit: 0,
  senior_limit: 0,
  tot_limit: 0,
  per_order_limit: 0,
  over_limit_behavior: "Block",
};

const toForm = (a) => ({
  price: a.price.toString(),
  is_add_on: a.is_add_on === true,
  status: (a.status || "Available").toLowerCase(),
  adult_limit: a.adult_limit || 0,
  child_limit: a.child_limit || 0,
  senior_limit: a.senior_limit || 0,
  tot_limit: a.tot_limit || 0,
  per_order_limit: a.per_order_limit || 0,
  over_limit_behavior: a.over_limit_behavior || "Block",
});

// Mirrors Limit Rules so problems show before saving
const cap = (n) => (n === 0 ? Infinity : n);
const formProblem = (f) => {
  const [adult, child, senior, tot, perOrder] = [
    f.adult_limit,
    f.child_limit,
    f.senior_limit,
    f.tot_limit,
    f.per_order_limit,
  ].map((v) => parseInt(v, 10) || 0);

  if ([adult, child, senior, tot, perOrder].some((v) => v < 0))
    return "Limits cannot be negative.";
  if (cap(child) > cap(adult)) return "Child limit cannot exceed Adult limit.";
  if (cap(senior) > cap(adult))
    return "Senior limit cannot exceed Adult limit.";
  if (cap(tot === 0 ? child : tot) > cap(child))
    return "Toddler limit cannot exceed Child limit.";
  if (f.is_add_on && !(parseFloat(f.price) > 0))
    return "Add-on items must have a price greater than 0.";
  if (f.over_limit_behavior === "Charge" && !(parseFloat(f.price) > 0))
    return "Items that charge over the limit need a price greater than 0.";
  return null;
};

const errorText = (data, fallback) => {
  if (!data) return fallback;
  if (typeof data === "string") return data;
  if (data.errors && typeof data.errors === "object")
    return Object.values(data.errors).flat().join(" ");
  return data.message ?? data.title ?? fallback;
};

// ["A"] → "A", ["A", "B"] → "A and B", ["A", "B", "C"] → "A, B and C"
const joinNames = (names) =>
  names.length <= 1
    ? names.join("")
    : `${names.slice(0, -1).join(", ")} and ${names[names.length - 1]}`;

// Short summary for the table, e.g. "2/person, 1/order"
const limitSummary = (a) => {
  const parts = [];
  if (a.adult_limit) parts.push(`${a.adult_limit}/adult`);
  if (a.per_order_limit) parts.push(`${a.per_order_limit}/order`);
  if (parts.length && a.over_limit_behavior === "Charge")
    parts.push(`then $${Number(a.price).toFixed(2)}`);
  return parts.length ? parts.join(", ") : "None";
};

// The menu's default row first, then its location prices
const byMenuThenLocation = (a, b) =>
  a.menu_id - b.menu_id || (a.location_id ?? 0) - (b.location_id ?? 0);

const getAssignmentColumns = ({ locationName, onEdit, onDelete }) => [
  {
    field: "menu",
    headerName: "Menu",
    flex: 1,
    minWidth: 120,
    valueGetter: (value, row) => row.menu_name || "Unknown Menu",
  },
  {
    field: "location_id",
    headerName: "Location",
    flex: 1,
    minWidth: 130,
    valueGetter: (value) => locationName(value),
    renderCell: ({ row, value }) =>
      row.location_id == null ? <strong>{value}</strong> : value,
  },
  {
    field: "price",
    headerName: "Price",
    width: 90,
    valueFormatter: (value) => `$${Number(value).toFixed(2)}`,
  },
  {
    field: "limits",
    headerName: "Limits",
    flex: 1.2,
    minWidth: 140,
    valueGetter: (value, row) => limitSummary(row),
  },
  { field: "status", headerName: "Status", width: 110 },
  {
    field: "is_add_on",
    headerName: "Add-on",
    width: 90,
    valueGetter: (value) => (value ? "Yes" : "No"),
  },
  actionsColumn(
    (row) => {
      const menu = row.menu_name || "Unknown Menu";
      return [
        {
          label: "Edit assignment",
          icon: Edit,
          onClick: () => onEdit(row),
        },
        row.location_id == null
          ? {
              label: "Remove from menu",
              ariaLabel: `Remove from menu ${menu}`,
              icon: Trash2,
              color: "error",
              onClick: () => onDelete(row),
            }
          : {
              label: "Reset to default price",
              ariaLabel: `Reset to default price: ${menu}, ${locationName(row.location_id)}`,
              icon: Trash2,
              color: "error",
              onClick: () => onDelete(row),
            },
      ];
    },
    {
      width: 110,
      getRowName: (row) =>
        `${row.menu_name || "Unknown Menu"}, ${locationName(row.location_id)}`,
    },
  ),
];

const MenuAssignmentDialog = ({
  open,
  onClose,
  selectedItem,
  menus,
  onSuccess,
  initialAssignments = [],
}) => {
  const [formData, setFormData] = useState(emptyForm);
  const [submitting, setSubmitting] = useState(false);
  const [assignments, setAssignments] = useState(initialAssignments);
  const [editingAssignment, setEditingAssignment] = useState(null);
  const [error, setError] = useState(null);
  const [locations, setLocations] = useState([]);
  // Adding a location price to a menu the item isn't on: sell it only there?
  const [onlyAtLocation, setOnlyAtLocation] = useState(false);
  // The default row waiting on "Remove from menu?" confirmation
  const [pendingRemove, setPendingRemove] = useState(null);
  const [removeError, setRemoveError] = useState(null);

  const setField = (field) => (e) =>
    setFormData((prev) => ({ ...prev, [field]: e.target.value }));

  const loadAssignments = async () => {
    const response = await api.get(`/menuItem/${selectedItem.item_id}`);
    const assignmentsData = response.data.menu_assignments || [];
    setAssignments(Array.isArray(assignmentsData) ? assignmentsData : []);
  };

  // The dialog stays mounted between items, so start each open with a clean form.
  // Otherwise a menu picked for the last item can be missing from this one's options.
  useEffect(() => {
    if (!open) return;
    setFormData(emptyForm);
    setEditingAssignment(null);
    setError(null);
    setOnlyAtLocation(false);
    setPendingRemove(null);
  }, [open, selectedItem]);

  useEffect(() => {
    if (!selectedItem) return;
    loadAssignments().catch((err) => {
      console.error("Fetch error:", err);
      setError("Failed to fetch assignments");
      setAssignments([]);
    });
  }, [selectedItem]);

  // Locations for the "Location" dropdown
  useEffect(() => {
    if (!open) return;
    api
      .get("/Location")
      .then((res) =>
        setLocations(
          (Array.isArray(res.data) ? res.data : []).map((l) => ({
            location_id: l.location_id,
            name: l.name,
          })),
        ),
      )
      .catch((err) => console.error("Location fetch error:", err));
  }, [open]);

  useEffect(() => {
    if (editingAssignment) {
      setFormData({
        ...emptyForm,
        menu_id: editingAssignment.menu_id,
        location_id: editingAssignment.location_id ?? ALL_LOCATIONS,
        ...toForm(editingAssignment),
      });
    }
  }, [editingAssignment]);

  const locationName = (id) =>
    id == null
      ? "All locations"
      : (locations.find((l) => l.location_id === id)?.name ?? `Location ${id}`);

  // ?location_id=X for an override row, nothing for the default row
  const locationQuery = (locId) =>
    locId != null ? `?location_id=${locId}` : "";

  const deleteAssignment = async (assignment) => {
    const isDefault = assignment.location_id == null;
    await api.delete(
      `/menuassignment/${assignment.menu_id}/${selectedItem.item_id}${locationQuery(assignment.location_id)}`,
    );
    setAssignments((prev) =>
      isDefault
        ? prev.filter((a) => a.menu_id !== assignment.menu_id) // default + its overrides
        : prev.filter((a) => a.assignment_id !== assignment.assignment_id),
    );
    resetForm();
  };

  // A default row takes its location prices with it, so it asks first
  const handleDeleteAssignment = async (assignment) => {
    if (assignment.location_id == null) {
      // Named now, since the rows are gone by the time the confirm finishes closing
      const locationNames = joinNames(
        assignments
          .filter(
            (a) => a.menu_id === assignment.menu_id && a.location_id != null,
          )
          .map((a) => locationName(a.location_id)),
      );
      setRemoveError(null);
      setPendingRemove({ assignment, locationNames });
      return;
    }
    try {
      await deleteAssignment(assignment);
    } catch (err) {
      setError(errorText(err.response?.data, "Failed to delete assignment"));
    }
  };

  // false keeps the confirm open to show the error
  const confirmRemove = async () => {
    try {
      await deleteAssignment(pendingRemove.assignment);
    } catch (err) {
      setRemoveError(
        errorText(err.response?.data, "Failed to remove from menu"),
      );
      return false;
    }
  };

  const removeMessage = (() => {
    if (!pendingRemove) return "";
    const menu = pendingRemove.assignment.menu_name || "this menu";
    const base = `${selectedItem?.name} will be removed from ${menu} at every location.`;
    return pendingRemove.locationNames
      ? `${base} This also removes the prices for ${pendingRemove.locationNames}.`
      : base;
  })();

  const resetForm = () => {
    setFormData({ ...emptyForm, price: "" });
    setOnlyAtLocation(false);
    setEditingAssignment(null);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError(null);

    const problem = formProblem(formData);
    if (problem) {
      setError(problem);
      return;
    }

    setSubmitting(true);
    try {
      const url = editingAssignment
        ? `/menuassignment/${editingAssignment.menu_id}/${selectedItem.item_id}${locationQuery(editingAssignment.location_id)}`
        : "/menuassignment";

      const fields = {
        price: parseFloat(formData.price) || 0,
        adult_limit: parseInt(formData.adult_limit) || 0,
        child_limit: parseInt(formData.child_limit) || 0,
        senior_limit: parseInt(formData.senior_limit) || 0,
        tot_limit: parseInt(formData.tot_limit) || 0,
        per_order_limit: parseInt(formData.per_order_limit) || 0,
        over_limit_behavior: formData.over_limit_behavior,
        status: formData.status,
        is_add_on: formData.is_add_on,
      };

      // An edit's menu, item and location are in the URL, and the API rejects them in the body.
      // A location price on a menu the item isn't on yet: the API adds the default row too
      await api({
        method: editingAssignment ? "PUT" : "POST",
        url,
        data: editingAssignment
          ? fields
          : {
              ...fields,
              menu_id: parseInt(formData.menu_id),
              item_id: selectedItem.item_id,
              location_id:
                formData.location_id === ALL_LOCATIONS
                  ? null
                  : parseInt(formData.location_id),
              ...(needsDefault && { only_at_location: onlyAtLocation }),
            },
      });

      // Refetch assignments to ensure we have complete data with menu objects
      await loadAssignments();

      resetForm();
      if (onSuccess) onSuccess();
    } catch (err) {
      console.error("Submit error:", err);
      setError(
        errorText(
          err.response?.data,
          editingAssignment
            ? "Failed to update assignment"
            : "Failed to create assignment",
        ),
      );
    } finally {
      setSubmitting(false);
    }
  };

  // Does this menu + location already have a row?
  const hasRow = (menuId, locId) =>
    assignments.some(
      (a) => a.menu_id === menuId && (a.location_id ?? null) === locId,
    );

  const selectedLocId =
    formData.location_id === ALL_LOCATIONS
      ? null
      : Number(formData.location_id);

  // Menus without a row for the chosen location (null = the default row) yet.
  // A location row on a menu with no default is fine: saving adds the default first.
  const availableMenus = menus.filter((menu) => {
    if (editingAssignment && editingAssignment.menu_id === menu.menu_id)
      return true;
    return !hasRow(menu.menu_id, selectedLocId);
  });

  // Picked a location on a menu the item isn't on yet
  const needsDefault =
    !editingAssignment &&
    selectedLocId != null &&
    formData.menu_id !== "" &&
    !hasRow(Number(formData.menu_id), null);

  const noMenusText =
    selectedLocId == null
      ? "This item is already on every menu."
      : "Every menu already has a price for this location.";

  // Picking a location + menu copies the default row's values into the form
  useEffect(() => {
    if (editingAssignment || selectedLocId == null || !formData.menu_id) return;
    const def = assignments.find(
      (a) => a.menu_id === Number(formData.menu_id) && a.location_id == null,
    );
    if (!def) return;
    setFormData((prev) => ({ ...prev, ...toForm(def) }));
  }, [formData.menu_id, formData.location_id]);

  const limitField = (field, label, helperText) => (
    <TextField
      key={field}
      fullWidth
      label={label}
      type="number"
      value={formData[field]}
      onChange={setField(field)}
      slotProps={{ htmlInput: { min: 0, max: 99 } }}
      helperText={helperText}
    />
  );

  return (
    <>
      {/* md: wide enough for every table column without scrolling sideways */}
      <AppDialog open={open} onClose={onClose} maxWidth="md">
        {/* Outside the scrolling content, so the title and close button stay in view */}
        <DialogHeader
          title="Menu Assignments"
          subtitle={selectedItem?.name}
          onClose={onClose}
        />
        <DialogContent dividers>
          <form id={FORM_ID} onSubmit={handleSubmit}>
            <Box>
              {error && (
                <Alert
                  severity="error"
                  sx={{ mb: 3 }}
                  onClose={() => setError(null)}
                >
                  {error}
                </Alert>
              )}

              <Typography variant="subtitle1" gutterBottom>
                Current Assignments
              </Typography>

              {assignments.length > 0 ? (
                <Box sx={{ mb: 3 }}>
                  <ManagementGrid
                    rows={[...assignments].sort(byMenuThenLocation)}
                    columns={getAssignmentColumns({
                      locationName,
                      onEdit: setEditingAssignment,
                      onDelete: handleDeleteAssignment,
                    })}
                    getRowId={(row) => row.assignment_id}
                    // The row being edited stays highlighted
                    rowSelectionModel={{
                      type: "include",
                      ids: new Set(
                        editingAssignment
                          ? [editingAssignment.assignment_id]
                          : [],
                      ),
                    }}
                    showToolbar={false}
                    hideFooter={assignments.length <= 10}
                    disableColumnMenu
                  />
                </Box>
              ) : (
                <Alert severity="info" sx={{ mb: 3 }}>
                  No current assignments
                </Alert>
              )}

              <Typography variant="subtitle1" gutterBottom>
                {editingAssignment ? "Edit Assignment" : "Add New Assignment"}
              </Typography>

              <Stack spacing={2}>
                <FormControl fullWidth>
                  <InputLabel id="assignment-location-label">
                    Location
                  </InputLabel>
                  <Select
                    labelId="assignment-location-label"
                    label="Location"
                    value={formData.location_id}
                    onChange={(e) =>
                      setFormData((prev) => ({
                        ...prev,
                        location_id: e.target.value,
                        menu_id: "", // menu choices depend on the location
                      }))
                    }
                    disabled={Boolean(editingAssignment)}
                  >
                    <MenuItem value={ALL_LOCATIONS}>
                      All locations (default)
                    </MenuItem>
                    {locations.map((loc) => (
                      <MenuItem key={loc.location_id} value={loc.location_id}>
                        {loc.name}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
                <FormControl fullWidth required>
                  <InputLabel id="assignment-menu-label">Menu</InputLabel>
                  <Select
                    labelId="assignment-menu-label"
                    label="Menu"
                    value={formData.menu_id}
                    onChange={setField("menu_id")}
                    disabled={Boolean(editingAssignment)}
                  >
                    {availableMenus.map((menu) => (
                      <MenuItem key={menu.menu_id} value={menu.menu_id}>
                        {menu.name}
                      </MenuItem>
                    ))}
                  </Select>
                  {!editingAssignment && availableMenus.length === 0 && (
                    <FormHelperText>{noMenusText}</FormHelperText>
                  )}
                </FormControl>
                {needsDefault && (
                  <Box>
                    <FormControlLabel
                      control={
                        <Switch
                          checked={onlyAtLocation}
                          onChange={(e) => setOnlyAtLocation(e.target.checked)}
                        />
                      }
                      label={`Only sell at ${locationName(selectedLocId)}`}
                    />
                    <FormHelperText>
                      {onlyAtLocation
                        ? "This item isn't on this menu yet. Saving also adds it for all locations as Unavailable, so only this location sells it."
                        : "This item isn't on this menu yet. Saving also adds it for all locations at the same price."}
                    </FormHelperText>
                  </Box>
                )}

                <Stack direction="row" spacing={2}>
                  <TextField
                    label="Price"
                    helperText={
                      formData.is_add_on
                        ? "Charged every time"
                        : "Included: only charged past a per-person limit set to Charge (0 = never)"
                    }
                    type="number"
                    value={formData.price}
                    onChange={setField("price")}
                    required
                    slotProps={{ htmlInput: { step: "0.01", min: "0" } }}
                    sx={{ flex: 1 }}
                  />

                  <FormControl sx={{ flex: 1 }}>
                    <InputLabel id="assignment-status-label">Status</InputLabel>
                    <Select
                      labelId="assignment-status-label"
                      label="Status"
                      value={formData.status}
                      onChange={setField("status")}
                    >
                      <MenuItem value="available">Available</MenuItem>
                      <MenuItem value="unavailable">Unavailable</MenuItem>
                      <MenuItem value="seasonal">Seasonal</MenuItem>
                    </Select>
                  </FormControl>
                </Stack>

                <FormControlLabel
                  control={
                    <Switch
                      checked={formData.is_add_on === true}
                      onChange={(e) =>
                        setFormData((prev) => ({
                          ...prev,
                          is_add_on: e.target.checked,
                        }))
                      }
                    />
                  }
                  label="Is Add-on Item"
                />

                {/* Per person, per dining session. 0 = unlimited */}
                <Typography variant="subtitle2" color="text.secondary">
                  Limits per person (whole dining session). 0 = unlimited.
                </Typography>
                <Box
                  sx={{
                    display: "grid",
                    gridTemplateColumns: {
                      xs: "repeat(2, 1fr)",
                      sm: "repeat(4, 1fr)",
                    },
                    gap: 2,
                  }}
                >
                  {limitField("adult_limit", "Adult", " ")}
                  {limitField("senior_limit", "Senior", "Max: adult")}
                  {limitField("child_limit", "Child", "Max: adult")}
                  {limitField("tot_limit", "Toddler", "0 = same as child")}
                </Box>

                <Box
                  sx={{
                    display: "grid",
                    gridTemplateColumns: { xs: "1fr", sm: "1fr 3fr" },
                    gap: 2,
                    alignItems: "start",
                  }}
                >
                  {limitField(
                    "per_order_limit",
                    "Per order limit",
                    "Whole table · 0 = no cap · always blocks",
                  )}

                  <FormControl fullWidth>
                    <InputLabel id="assignment-over-limit-label">
                      Past per-person limit
                    </InputLabel>
                    <Select
                      labelId="assignment-over-limit-label"
                      label="Past per-person limit"
                      value={formData.over_limit_behavior}
                      onChange={setField("over_limit_behavior")}
                    >
                      <MenuItem value="Block">Block</MenuItem>
                      <MenuItem value="Charge">
                        Charge the item&apos;s price
                      </MenuItem>
                    </Select>
                    <FormHelperText>
                      Charge bills the price for each extra over the limits
                      above
                    </FormHelperText>
                  </FormControl>
                </Box>
              </Stack>
            </Box>
          </form>
        </DialogContent>
        {/* In the footer so the save button stays in view; it submits the form by id */}
        <DialogActions>
          <Button onClick={onClose}>Close</Button>
          {editingAssignment && (
            <Button onClick={resetForm} disabled={submitting}>
              Cancel Edit
            </Button>
          )}
          <Button
            type="submit"
            form={FORM_ID}
            variant="contained"
            disabled={submitting}
          >
            {submitting ? (
              <CircularProgress size={24} />
            ) : editingAssignment ? (
              "Update Assignment"
            ) : selectedLocId != null ? (
              "Add Location Price"
            ) : (
              "Add Assignment"
            )}
          </Button>
        </DialogActions>
      </AppDialog>

      <ConfirmDialog
        open={Boolean(pendingRemove)}
        title="Remove from menu?"
        message={removeMessage}
        confirmLabel="Remove"
        onConfirm={confirmRemove}
        onClose={() => setPendingRemove(null)}
        error={removeError}
      />
    </>
  );
};

export default MenuAssignmentDialog;
