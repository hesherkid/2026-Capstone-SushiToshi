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
} from "@mui/material";
import AppDialog from "@/components/common/AppDialog";
import { AlertTriangle } from "lucide-react";

const CategoryManagementDialog = ({
  open,
  onClose,
  selectedCategory,
  onSuccess,
}) => {
  const [formData, setFormData] = useState({
    name: "",
    description: "",
    adult_limit: 0,
    child_limit: 0,
    senior_limit: 0,
    total_limit: 0,
  });
  const [error, setError] = useState(null);
  const [submitting, setSubmitting] = useState(false);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);

  // TODO: Update API endpoints for categories
  useEffect(() => {
    if (selectedCategory) {
      // Accept both API and dialog field names
      setFormData({
        name: selectedCategory.name ?? selectedCategory.category_name ?? "",
        description: selectedCategory.description || "",
        adult_limit: selectedCategory.adult_limit ?? 0,
        child_limit: selectedCategory.child_limit ?? 0,
        senior_limit: selectedCategory.senior_limit ?? 0,
        total_limit:
          selectedCategory.total_limit ?? selectedCategory.tot_limit ?? 0,
      });
    } else {
      setFormData({
        name: "",
        description: "",
        adult_limit: 0,
        child_limit: 0,
        senior_limit: 0,
        total_limit: 0,
      });
    }
  }, [selectedCategory]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSubmitting(true);
    setError(null);

    try {
      let response;
      if (selectedCategory) {
        response = await api.put(
          `/Category/update_category/${selectedCategory.category_id}`,
          formData
        );
      } else {
        response = await api.post("/Category", formData);
      }

      // Check for error in response
      if (response?.data?.error || response?.data?.detail) {
        setError(
          typeof response.data === "string"
            ? response.data
            : JSON.stringify(response.data, null, 2)
        );
        return;
      }

      onSuccess();
      onClose();
    } catch (err) {
      // Show full error response if available
      if (err.response?.data) {
        setError(
          typeof err.response.data === "string"
            ? err.response.data
            : JSON.stringify(err.response.data, null, 2)
        );
      } else {
        setError(
          err.message ||
          "Failed to save category"
        );
      }
    } finally {
      setSubmitting(false);
    }
  };

  const handleDelete = async () => {
    setSubmitting(true);
    setError(null);

    try {
      // Check for menu items in category
      const itemsResponse = await api.get(
        `/categories/${selectedCategory.category_id}/menu-items`
      );

      if (itemsResponse.data && itemsResponse.data.length > 0) {
        throw new Error(
          "Cannot delete category - it contains menu items. Remove all menu items first."
        );
      }

      // Delete the category
      await api.delete(`/categories/${selectedCategory.category_id}`);

      onSuccess();
      onClose();
    } catch (err) {
      console.error("Error deleting category:", err);
      setError(err.response?.data?.detail || err.message);
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

              <Stack direction="row" spacing={2}>
                <TextField
                  label="Adult Limit"
                  type="number"
                  value={formData.adult_limit}
                  onChange={(e) =>
                    setFormData((prev) => ({
                      ...prev,
                      adult_limit: parseInt(e.target.value) || 0,
                    }))
                  }
                  required
                  fullWidth
                />

                <TextField
                  label="Child Limit"
                  type="number"
                  value={formData.child_limit}
                  onChange={(e) =>
                    setFormData((prev) => ({
                      ...prev,
                      child_limit: parseInt(e.target.value) || 0,
                    }))
                  }
                  required
                  fullWidth
                />
              </Stack>

              <Stack direction="row" spacing={2}>
                <TextField
                  label="Senior Limit"
                  type="number"
                  value={formData.senior_limit}
                  onChange={(e) =>
                    setFormData((prev) => ({
                      ...prev,
                      senior_limit: parseInt(e.target.value) || 0,
                    }))
                  }
                  required
                  fullWidth
                />

                <TextField
                  label="Total Limit"
                  type="number"
                  value={formData.total_limit}
                  onChange={(e) =>
                    setFormData((prev) => ({
                      ...prev,
                      total_limit: parseInt(e.target.value) || 0,
                    }))
                  }
                  required
                  fullWidth
                />
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
            Are you sure you want to delete "{selectedCategory?.name}"? This
            action cannot be undone.
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
