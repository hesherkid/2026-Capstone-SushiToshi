import api from "@/config/api";
import React, { useState, useEffect } from "react";
import {
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Button,
  Box,
  Stack,
  Alert,
  CircularProgress,
  Typography,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  FormHelperText,
} from "@mui/material";
import AppDialog from "@/components/common/AppDialog";
import { AlertTriangle } from "lucide-react";

const emptyForm = {
  name: "",
  description: "",
  adult_limit: 0,
  child_limit: 0,
  senior_limit: 0,
  toddler_limit: 0,
  per_order_limit: 0,
  over_limit_behavior: "Block",
};

const errorText = (data, fallback) => {
  if (!data) return fallback;
  if (typeof data === "string") return data;
  if (data.errors && typeof data.errors === "object") {
    return Object.values(data.errors).flat().join(" ");
  }
  return data.message ?? data.title ?? fallback;
};

// Mirrors Limit Rules so admins see problems before saving
const cap = (n) => (n === 0 ? Infinity : n);
const limitProblem = ({
  adult_limit,
  child_limit,
  senior_limit,
  toddler_limit,
  per_order_limit,
}) => {
  const values = [
    adult_limit,
    child_limit,
    senior_limit,
    toddler_limit,
    per_order_limit,
  ];
  if (values.some((v) => v < 0)) return "Limits cannot be negative.";
  if (cap(child_limit) > cap(adult_limit))
    return "Child limit cannot exceed Adult limit.";
  if (cap(senior_limit) > cap(adult_limit))
    return "Senior limit cannot exceed Adult limit.";
  const toddler = toddler_limit === 0 ? child_limit : toddler_limit;
  if (cap(toddler) > cap(child_limit))
    return "Toddler limit cannot exceed Child limit.";
  return null;
};

const CategoryManagementDialog = ({
  open,
  onClose,
  selectedCategory,
  onSuccess,
}) => {
  const [formData, setFormData] = useState(emptyForm);
  const [error, setError] = useState(null);
  const [submitting, setSubmitting] = useState(false);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);

  useEffect(() => {
    if (selectedCategory) {
      // Accept both API and dialog field names
      setFormData({
        name: selectedCategory.name ?? selectedCategory.category_name ?? "",
        description: selectedCategory.description || "",
        adult_limit: selectedCategory.adult_limit ?? 0,
        child_limit: selectedCategory.child_limit ?? 0,
        senior_limit: selectedCategory.senior_limit ?? 0,
        toddler_limit: selectedCategory.toddler_limit ?? 0,
        per_order_limit: selectedCategory.per_order_limit ?? 0,
        over_limit_behavior: selectedCategory.over_limit_behavior ?? "Block",
      });
    } else {
      setFormData(emptyForm);
    }
    setError(null);
  }, [selectedCategory]);

  const setNumber = (field) => (e) =>
    setFormData((prev) => ({
      ...prev,
      [field]: parseInt(e.target.value, 10) || 0,
    }));

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError(null);

    const problem = limitProblem(formData);
    if (problem) {
      setError(problem);
      return;
    }

    setSubmitting(true);
    try {
      if (selectedCategory) {
        await api.put(
          `/Category/update_category/${selectedCategory.category_id}`,
          formData,
        );
      } else {
        await api.post("/Category", formData);
      }
      onSuccess();
      onClose();
    } catch (err) {
      setError(
        errorText(err.response?.data, err.message || "Failed to save category"),
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleDelete = async () => {
    setSubmitting(true);
    setError(null);

    try {
      // The API returns 404 when the category has no items
      const hasItems = await api
        .get(`/Category/get_menu_by_category/${selectedCategory.category_id}`, {
          params: { limit: 1 },
        })
        .then((res) => Array.isArray(res.data) && res.data.length > 0)
        .catch((err) => {
          if (err.response?.status === 404) return false;
          throw err;
        });

      if (hasItems) {
        throw new Error(
          "Cannot delete category - it contains menu items. Remove all menu items first.",
        );
      }

      await api.delete(
        `/Category/delete_category/${selectedCategory.category_id}`,
      );

      onSuccess();
      onClose();
    } catch (err) {
      console.error("Error deleting category:", err);
      setError(errorText(err.response?.data, err.message));
    } finally {
      setSubmitting(false);
      setDeleteDialogOpen(false);
    }
  };

  return (
    <>
      <AppDialog open={open} onClose={onClose}>
        <DialogTitle>
          {selectedCategory ? "Edit Category" : "New Category"}
        </DialogTitle>
        <DialogContent>
          <Box component="form" onSubmit={handleSubmit} sx={{ pt: 2 }}>
            {error && (
              <Alert severity="error" sx={{ mb: 2 }}>
                {error}
              </Alert>
            )}

            <Stack spacing={3}>
              <TextField
                label="Name"
                value={formData.name}
                onChange={(e) =>
                  setFormData((prev) => ({ ...prev, name: e.target.value }))
                }
                required
                fullWidth
              />

              <TextField
                label="Description"
                value={formData.description}
                onChange={(e) =>
                  setFormData((prev) => ({
                    ...prev,
                    description: e.target.value,
                  }))
                }
                multiline
                rows={3}
                fullWidth
              />

              {/* Per person, per dining session. 0 = unlimited */}
              <Typography variant="subtitle2" color="text.secondary">
                Limits per person (whole dining session). 0 = unlimited.
              </Typography>

              <Stack direction="row" spacing={2}>
                <TextField
                  label="Adult Limit"
                  type="number"
                  value={formData.adult_limit}
                  onChange={setNumber("adult_limit")}
                  slotProps={{ htmlInput: { min: 0, max: 99 } }}
                  required
                  fullWidth
                />
                <TextField
                  label="Senior Limit"
                  type="number"
                  value={formData.senior_limit}
                  onChange={setNumber("senior_limit")}
                  slotProps={{ htmlInput: { min: 0, max: 99 } }}
                  helperText="Can't exceed adult"
                  required
                  fullWidth
                />
              </Stack>

              <Stack direction="row" spacing={2}>
                <TextField
                  label="Child Limit"
                  type="number"
                  value={formData.child_limit}
                  onChange={setNumber("child_limit")}
                  slotProps={{ htmlInput: { min: 0, max: 99 } }}
                  helperText="Can't exceed adult"
                  required
                  fullWidth
                />
                <TextField
                  label="Toddler Limit"
                  type="number"
                  value={formData.toddler_limit}
                  onChange={setNumber("toddler_limit")}
                  slotProps={{ htmlInput: { min: 0, max: 99 } }}
                  helperText="0 = same as child"
                  fullWidth
                />
              </Stack>

              <Stack direction="row" spacing={2} alignItems="flex-start">
                <TextField
                  label="Per Order Limit"
                  type="number"
                  value={formData.per_order_limit}
                  onChange={setNumber("per_order_limit")}
                  slotProps={{ htmlInput: { min: 0, max: 99 } }}
                  helperText="Whole table, all items in this category combined. 0 = no cap"
                  fullWidth
                />
                <FormControl fullWidth>
                  <InputLabel id="over-limit-label">Over Limit</InputLabel>
                  <Select
                    labelId="over-limit-label"
                    label="Over Limit"
                    value={formData.over_limit_behavior}
                    onChange={(e) =>
                      setFormData((prev) => ({
                        ...prev,
                        over_limit_behavior: e.target.value,
                      }))
                    }
                  >
                    <MenuItem value="Block">Block</MenuItem>
                    <MenuItem value="Charge">Charge full price</MenuItem>
                  </Select>
                  <FormHelperText>
                    Per-person limits only. Charge uses each item's full price.
                  </FormHelperText>
                </FormControl>
              </Stack>
            </Stack>
          </Box>
        </DialogContent>
        <DialogActions>
          {selectedCategory && (
            <Button
              color="error"
              onClick={() => setDeleteDialogOpen(true)}
              sx={{ mr: "auto" }}
            >
              Delete
            </Button>
          )}
          <Button onClick={onClose}>Cancel</Button>
          <Button
            onClick={handleSubmit}
            variant="contained"
            disabled={submitting}
          >
            {submitting ? (
              <CircularProgress size={24} />
            ) : selectedCategory ? (
              "Save Changes"
            ) : (
              "Create Category"
            )}
          </Button>
        </DialogActions>
      </AppDialog>

      <AppDialog
        open={deleteDialogOpen}
        onClose={() => setDeleteDialogOpen(false)}
        maxWidth="xs"
      >
        <Box sx={{ p: 3 }}>
          <Stack direction="row" spacing={1} alignItems="center" mb={2}>
            <AlertTriangle color="error" />
            <Typography variant="h6">Confirm Delete</Typography>
          </Stack>
          <Typography mb={3}>
            Are you sure you want to delete "{formData.name}"? This action
            cannot be undone.
          </Typography>
          <Stack direction="row" spacing={1} justifyContent="flex-end">
            <Button onClick={() => setDeleteDialogOpen(false)}>Cancel</Button>
            <Button
              variant="contained"
              color="error"
              onClick={handleDelete}
              disabled={submitting}
            >
              {submitting ? <CircularProgress size={24} /> : "Delete"}
            </Button>
          </Stack>
        </Box>
      </AppDialog>
    </>
  );
};

export default CategoryManagementDialog;
