import api from "@/config/api";
import React, { useState, useEffect } from "react";
import {
  Dialog,
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
import { X, Edit, Trash2 } from "lucide-react";

const MenuAssignmentDialog = ({
  open,
  onClose,
  selectedItem,
  menus,
  onSuccess,
  initialAssignments = [],
}) => {
  const [formData, setFormData] = useState({
    menu_id: "",
    price: "0",
    is_add_on: false,
    status: "available",
    adult_limit: 0,
    child_limit: 0,
    senior_limit: 0,
    tot_limit: 0,
  });
  const [submitting, setSubmitting] = useState(false);
  const [assignments, setAssignments] = useState(initialAssignments);
  const [editingAssignment, setEditingAssignment] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    const fetchAssignments = async () => {
      if (!selectedItem) return;
      try {
        const response = await api.get(`/menuItem/${selectedItem.item_id}`);
        const assignmentsData = response.data.menuAssignments || [];
        setAssignments(Array.isArray(assignmentsData) ? assignmentsData : []);
      } catch (err) {
        console.error("Fetch error:", err);
        setError("Failed to fetch assignments");
        setAssignments([]);
      }
    };
    fetchAssignments();
  }, [selectedItem]);

  useEffect(() => {
    if (editingAssignment) {
      setFormData({
        menu_id: editingAssignment.menu_Id, // FIXED: capital I
        price: editingAssignment.price.toString(),
        is_add_on: editingAssignment.is_Add_On === true, // FIXED: capital A and O
        status: (editingAssignment.status || "Available").toLowerCase(),
        adult_limit: editingAssignment.adult_Limit || 0, // FIXED: capital L
        child_limit: editingAssignment.child_limit || 0,
        senior_limit: editingAssignment.senior_limit || 0,
        tot_limit: editingAssignment.tot_Limit || 0, // FIXED: capital L
      });
    }
  }, [editingAssignment]);

  const handleDeleteAssignment = async (menuId, itemId) => {
    try {
      await api.delete(`/menuassignment/${menuId}/${itemId}`);
      setAssignments((prev) => prev.filter((a) => a.menu_Id !== menuId)); // FIXED: capital I
      resetForm();
    } catch (err) {
      setError("Failed to delete assignment");
    }
  };

  const resetForm = () => {
    setFormData({
      menu_id: "",
      price: "",
      is_add_on: false,
      status: "available",
      adult_limit: 0,
      child_limit: 0,
      senior_limit: 0,
      tot_limit: 0,
    });
    setEditingAssignment(null);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSubmitting(true);
    try {
      const url = editingAssignment
        ? `/menuassignment/${editingAssignment.menu_Id}/${selectedItem.item_id}` // FIXED: menu_Id
        : "/menuassignment";

      await api({
        method: editingAssignment ? "PUT" : "POST",
        url,
        data: {
          menu_id: parseInt(formData.menu_id),
          item_id: selectedItem.item_id,
          price: parseFloat(formData.price),
          adult_limit: parseInt(formData.adult_limit) || 0,
          child_limit: parseInt(formData.child_limit) || 0,
          senior_limit: parseInt(formData.senior_limit) || 0,
          tot_limit: parseInt(formData.tot_limit) || 0,
          status: formData.status,
          is_add_on: formData.is_add_on,
        },
      });

      // Refetch assignments to ensure we have complete data with menu objects
      const response = await api.get(`/menuItem/${selectedItem.item_id}`);
      const assignmentsData = response.data.menuAssignments || [];
      setAssignments(Array.isArray(assignmentsData) ? assignmentsData : []);

      resetForm();
      if (onSuccess) onSuccess();
    } catch (err) {
      console.error("Submit error:", err);
      setError(
        err.response.data ||
          (editingAssignment
            ? "Failed to update assignment"
            : "Failed to create assignment"),
      );
    } finally {
      setSubmitting(false);
    }
  };

  // FIXED: Better filtering logic
  const availableMenus = menus.filter((menu) => {
    // If we're editing, allow the current menu
    if (editingAssignment && editingAssignment.menu_Id === menu.menu_id) {
      return true;
    }
    // Otherwise, check if this menu is already assigned
    return !assignments.some(
      (assignment) => assignment.menu_Id === menu.menu_id,
    );
  });

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
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
                    <TableCell sx={{ color: "white" }}>Price</TableCell>
                    <TableCell sx={{ color: "white" }}>Status</TableCell>
                    <TableCell sx={{ color: "white" }}>Add-on</TableCell>
                    <TableCell sx={{ color: "white" }} align="right">
                      Actions
                    </TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {assignments.map((assignment) => (
                    <TableRow key={assignment.menu_Id}>
                      <TableCell>
                        {/* FIXED: Use the nested menu object directly */}
                        {assignment.menu?.name || "Unknown Menu"}
                      </TableCell>
                      <TableCell>${assignment.price.toFixed(2)}</TableCell>
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
                          onClick={() =>
                            handleDeleteAssignment(
                              assignment.menu_Id, // FIXED: capital I
                              selectedItem.item_id,
                            )
                          }
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
                {editingAssignment && (
                  <MenuItem value={editingAssignment.menu_Id}>
                    {editingAssignment.menu?.name}
                  </MenuItem>
                )}
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
                ) : (
                  "Add Assignment"
                )}
              </Button>
            </Stack>
          </Stack>
        </Box>
      </form>
    </Dialog>
  );
};

export default MenuAssignmentDialog;
