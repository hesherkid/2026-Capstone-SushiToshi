import api from "@/config/api";
import React, { useState, useEffect } from "react";
import {
  Box,
  Stack,
  Typography,
  IconButton,
  Alert,
  TableContainer,
  Table,
  TableHead,
  TableBody,
  TableRow,
  TableCell,
  Paper,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  TextField,
  Switch,
  FormControlLabel,
  Grid,
  Button,
  CircularProgress,
} from "@mui/material";
import AppDialog from "@/components/common/AppDialog";
import { X, Edit, Trash2 } from "lucide-react";

const ALL_LOCATIONS = ""; // "" in the form = default row (location_id null)

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
};

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

  const loadAssignments = async () => {
    const response = await api.get(`/menuItem/${selectedItem.item_id}`);
    const assignmentsData = response.data.menuAssignments || [];
    setAssignments(Array.isArray(assignmentsData) ? assignmentsData : []);
  };

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
            location_id: l.location_Id ?? l.location_id,
            name: l.name ?? l.Name,
          })),
        ),
      )
      .catch((err) => console.error("Location fetch error:", err));
  }, [open]);

  useEffect(() => {
    if (editingAssignment) {
      setFormData({
        menu_id: editingAssignment.menu_Id,
        location_id: editingAssignment.location_Id ?? ALL_LOCATIONS,
        price: editingAssignment.price.toString(),
        is_add_on: editingAssignment.is_Add_On === true,
        status: (editingAssignment.status || "Available").toLowerCase(),
        adult_limit: editingAssignment.adult_Limit || 0,
        child_limit: editingAssignment.child_limit || 0,
        senior_limit: editingAssignment.senior_limit || 0,
        tot_limit: editingAssignment.tot_Limit || 0,
      });
    }
  }, [editingAssignment]);

  const locationName = (id) =>
    id == null
      ? "All locations"
      : locations.find((l) => l.location_id === id)?.name ?? `Location ${id}`;

  // ?locationId=X for an override row, nothing for the default row
  const locationQuery = (locId) => (locId != null ? `?locationId=${locId}` : "");

  const handleDeleteAssignment = async (assignment) => {
    const isDefault = assignment.location_Id == null;
    if (
      isDefault &&
      !window.confirm(
        "Removing the default also removes all location prices for this menu. Continue?",
      )
    ) {
      return;
    }
    try {
      await api.delete(
        `/menuassignment/${assignment.menu_Id}/${selectedItem.item_id}${locationQuery(assignment.location_Id)}`,
      );
      setAssignments((prev) =>
        isDefault
          ? prev.filter((a) => a.menu_Id !== assignment.menu_Id) // default + its overrides
          : prev.filter((a) => a.assignment_Id !== assignment.assignment_Id),
      );
      resetForm();
    } catch (err) {
      setError("Failed to delete assignment");
    }
  };

  const resetForm = () => {
    setFormData({ ...emptyForm, price: "" });
    setEditingAssignment(null);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSubmitting(true);
    try {
      const url = editingAssignment
        ? `/menuassignment/${editingAssignment.menu_Id}/${selectedItem.item_id}${locationQuery(editingAssignment.location_Id)}`
        : "/menuassignment";

      await api({
        method: editingAssignment ? "PUT" : "POST",
        url,
        data: {
          menu_id: parseInt(formData.menu_id),
          item_id: selectedItem.item_id,
          location_id:
            formData.location_id === ALL_LOCATIONS
              ? null
              : parseInt(formData.location_id),
          price: parseFloat(formData.price) || 0,
          adult_limit: parseInt(formData.adult_limit) || 0,
          child_limit: parseInt(formData.child_limit) || 0,
          senior_limit: parseInt(formData.senior_limit) || 0,
          tot_limit: parseInt(formData.tot_limit) || 0,
          status: formData.status,
          is_add_on: formData.is_add_on,
        },
      });

      // Refetch assignments to ensure we have complete data with menu objects
      await loadAssignments();

      resetForm();
      if (onSuccess) onSuccess();
    } catch (err) {
      console.error("Submit error:", err);
      const data = err.response?.data;
      setError(
        typeof data === "string"
          ? data
          : data?.message ??
              data?.title ??
              (editingAssignment
                ? "Failed to update assignment"
                : "Failed to create assignment"),
      );
    } finally {
      setSubmitting(false);
    }
  };

  // Does this menu + location already have a row?
  const hasRow = (menuId, locId) =>
    assignments.some(
      (a) => a.menu_Id === menuId && (a.location_Id ?? null) === locId,
    );

  const selectedLocId =
    formData.location_id === ALL_LOCATIONS ? null : Number(formData.location_id);

  // Default row: menus without one yet.
  // Location row: menus that HAVE a default but no row for that location yet.
  const availableMenus = menus.filter((menu) => {
    if (editingAssignment && editingAssignment.menu_Id === menu.menu_id) {
      return true;
    }
    if (selectedLocId == null) {
      return !hasRow(menu.menu_id, null);
    }
    return hasRow(menu.menu_id, null) && !hasRow(menu.menu_id, selectedLocId);
  });

  // Picking a location + menu copies the default row's values into the form
  useEffect(() => {
    if (editingAssignment || selectedLocId == null || !formData.menu_id) return;
    const def = assignments.find(
      (a) => a.menu_Id === Number(formData.menu_id) && a.location_Id == null,
    );
    if (!def) return;
    setFormData((prev) => ({
      ...prev,
      price: def.price.toString(),
      is_add_on: def.is_Add_On === true,
      status: (def.status || "Available").toLowerCase(),
      adult_limit: def.adult_Limit || 0,
      child_limit: def.child_limit || 0,
      senior_limit: def.senior_limit || 0,
      tot_limit: def.tot_Limit || 0,
    }));
  }, [formData.menu_id, formData.location_id]);

  return (
    <AppDialog open={open} onClose={onClose}>
      <form onSubmit={handleSubmit}>
        <Box sx={{ p: 3 }}>
          <Stack
            direction="row"
            justifyContent="space-between"
            alignItems="center"
            mb={2}
          >
            <Stack spacing={0.5}>
              <Typography variant="h6">Menu Assignments</Typography>
              <Typography variant="subtitle2" color="text.secondary">
                {selectedItem?.name}
              </Typography>
            </Stack>
            <IconButton onClick={onClose} size="small">
              <X size={20} />
            </IconButton>
          </Stack>

          {error && (
            <Alert
              severity="error"
              sx={{ mb: 3 }}
              onClose={() => setError(null)}
            >
              {error}
            </Alert>
          )}

          <Typography variant="subtitle1" gutterBottom sx={{ mt: 3 }}>
            Current Assignments
          </Typography>

          {assignments.length > 0 ? (
            <TableContainer component={Paper} sx={{ mb: 3 }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell sx={{ color: "white" }}>Menu</TableCell>
                    <TableCell sx={{ color: "white" }}>Location</TableCell>
                    <TableCell sx={{ color: "white" }}>Price</TableCell>
                    <TableCell sx={{ color: "white" }}>Status</TableCell>
                    <TableCell sx={{ color: "white" }}>Add-on</TableCell>
                    <TableCell sx={{ color: "white" }} align="right">
                      Actions
                    </TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                    {[...assignments]
                    .sort(
                      (a, b) =>
                        a.menu_Id - b.menu_Id ||
                        (a.location_Id ?? 0) - (b.location_Id ?? 0),
                    )
                    .map((assignment) => (
                    <TableRow
                      key={assignment.assignment_Id}
                      selected={editingAssignment?.assignment_Id === assignment.assignment_Id}
                    >
                      <TableCell>
                        {assignment.menu?.name || "Unknown Menu"}
                      </TableCell>
                      <TableCell>
                        {assignment.location_Id == null ? (
                          <strong>All locations</strong>
                        ) : (
                          locationName(assignment.location_Id)
                        )}
                      </TableCell>
                      <TableCell>${Number(assignment.price).toFixed(2)}</TableCell>
                      <TableCell>{assignment.status}</TableCell>
                      <TableCell>
                        {assignment.is_Add_On ? "Yes" : "No"}
                      </TableCell>
                      <TableCell align="right">
                        <IconButton
                          size="small"
                          onClick={() => setEditingAssignment(assignment)}
                          sx={{ mr: 1 }}
                        >
                          <Edit size={16} />
                        </IconButton>
                        <IconButton
                          size="small"
                          color="error"
                          title={
                            assignment.location_Id == null
                              ? "Remove from menu"
                              : "Reset to default price"
                          }
                          onClick={() => handleDeleteAssignment(assignment)}
                        >
                          <Trash2 size={16} />
                        </IconButton>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
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
              <InputLabel>Location</InputLabel>
              <Select
                value={formData.location_id}
                onChange={(e) =>
                  setFormData((prev) => ({
                    ...prev,
                    location_id: e.target.value,
                    menu_id: "", // menu choices depend on the location
                  }))
                }
                label="Location"
                disabled={Boolean(editingAssignment)}
              >
                <MenuItem value={ALL_LOCATIONS}>All locations (default)</MenuItem>
                {locations.map((loc) => (
                  <MenuItem key={loc.location_id} value={loc.location_id}>
                    {loc.name}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <FormControl fullWidth required>
              <InputLabel>Menu</InputLabel>
              <Select
                value={formData.menu_id}
                onChange={(e) =>
                  setFormData((prev) => ({ ...prev, menu_id: e.target.value }))
                }
                label="Menu"
                disabled={Boolean(editingAssignment)}
              >
                {availableMenus.map((menu) => (
                  <MenuItem key={menu.menu_id} value={menu.menu_id}>
                    {menu.name}
                  </MenuItem>
                ))}                
              </Select>
            </FormControl>

            <Stack direction="row" spacing={2}>
              <TextField
                label="Price"
                type="number"
                value={formData.price}
                onChange={(e) =>
                  setFormData((prev) => ({ ...prev, price: e.target.value }))
                }
                required
                disabled={!formData.is_add_on}
                inputProps={{ step: "0.01", min: "0" }}
                sx={{ flex: 1 }}
              />

              <FormControl sx={{ flex: 1 }}>
                <InputLabel>Status</InputLabel>
                <Select
                  value={formData.status}
                  onChange={(e) =>
                    setFormData((prev) => ({ ...prev, status: e.target.value }))
                  }
                  label="Status"
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
                      price: e.target.checked ? prev.price : "0",
                    }))
                  }
                />
              }
              label="Is Add-on Item"
            />

            <Grid container spacing={2}>
              <Grid item xs={6}>
                <TextField
                  fullWidth
                  label="Adult Limit"
                  type="number"
                  value={formData.adult_limit}
                  onChange={(e) =>
                    setFormData((prev) => ({
                      ...prev,
                      adult_limit: e.target.value,
                    }))
                  }
                  inputProps={{ min: 0, max: 99 }}
                />
              </Grid>
              <Grid item xs={6}>
                <TextField
                  fullWidth
                  label="Child Limit"
                  type="number"
                  value={formData.child_limit}
                  onChange={(e) =>
                    setFormData((prev) => ({
                      ...prev,
                      child_limit: e.target.value,
                    }))
                  }
                  inputProps={{ min: 0, max: 99 }}
                />
              </Grid>
              <Grid item xs={6}>
                <TextField
                  fullWidth
                  label="Senior Limit"
                  type="number"
                  value={formData.senior_limit}
                  onChange={(e) =>
                    setFormData((prev) => ({
                      ...prev,
                      senior_limit: e.target.value,
                    }))
                  }
                  inputProps={{ min: 0, max: 99 }}
                />
              </Grid>
              <Grid item xs={6}>
                <TextField
                  fullWidth
                  label="Tot Limit"
                  type="number"
                  value={formData.tot_limit}
                  onChange={(e) =>
                    setFormData((prev) => ({
                      ...prev,
                      tot_limit: e.target.value,
                    }))
                  }
                  inputProps={{ min: 0, max: 99 }}
                />
              </Grid>
            </Grid>

            <Stack direction="row" spacing={2}>
              {editingAssignment && (
                <Button
                  onClick={resetForm}
                  disabled={submitting}
                  sx={{ flex: 1 }}
                >
                  Cancel Edit
                </Button>
              )}
              <Button
                type="submit"
                variant="contained"
                disabled={submitting}
                sx={{ flex: 1 }}
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
            </Stack>
          </Stack>
        </Box>
      </form>
    </AppDialog>
  );
};

export default MenuAssignmentDialog;
