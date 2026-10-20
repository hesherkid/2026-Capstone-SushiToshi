import React, { useEffect, useState } from "react";
import {
  Typography,
  Alert,
  CircularProgress,
  Stack,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
} from "@mui/material";
import AppDialog from "@/components/common/AppDialog";
import { Edit, Trash2, AlertTriangle } from "lucide-react";
import api from "@/config/api";
import ManagementPage from "@/components/common/ManagementPage";
import { actionsColumn } from "@/components/common/RowActions";

// 0 means "no limit" for every limit except toddler, where it means "same as child"
const limitCell = ({ value }) => (value ? value : "—");
const toddlerCell = ({ value, row }) =>
  value ? value : row.child_limit ? `${row.child_limit} (child)` : "—";

const CategoryList = ({ onEditCategory, onCreateCategory }) => {
  const [categories, setCategories] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // For delete dialog
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [categoryToDelete, setCategoryToDelete] = useState(null);
  const [deleting, setDeleting] = useState(false);

  const fetchCategories = async () => {
    setLoading(true);
    setError(null);
    try {
      const { data } = await api.get("/category");
      setCategories(Array.isArray(data) ? data : []);
    } catch (err) {
      setError("Failed to fetch categories");
      setCategories([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchCategories();
  }, []);

  const handleDeleteClick = (category) => {
    setCategoryToDelete(category);
    setDeleteDialogOpen(true);
  };

  const handleDeleteConfirm = async () => {
    setDeleting(true);
    setError(null);
    try {
      await api.delete(
        `/Category/delete_category/${categoryToDelete.category_id}`,
      );
      await fetchCategories();
      setDeleteDialogOpen(false);
      setCategoryToDelete(null);
    } catch (err) {
      setError("Failed to delete category");
    } finally {
      setDeleting(false);
    }
  };

  const handleDeleteCancel = () => {
    setDeleteDialogOpen(false);
    setCategoryToDelete(null);
  };

  const columns = [
    { field: "category_name", headerName: "Name", flex: 1, minWidth: 150 },
    { field: "description", headerName: "Description", flex: 2, minWidth: 250 },
    {
      field: "adult_limit",
      headerName: "Adult",
      width: 90,
      type: "number",
      renderCell: limitCell,
    },
    {
      field: "child_limit",
      headerName: "Child",
      width: 90,
      type: "number",
      renderCell: limitCell,
    },
    {
      field: "senior_limit",
      headerName: "Senior",
      width: 90,
      type: "number",
      renderCell: limitCell,
    },
    {
      field: "toddler_limit",
      headerName: "Toddler",
      width: 110,
      type: "number",
      renderCell: toddlerCell,
    },
    {
      field: "per_order_limit",
      headerName: "Per Order",
      width: 100,
      type: "number",
      renderCell: limitCell,
    },
    {
      field: "over_limit_behavior",
      headerName: "Over Limit",
      width: 120,
      type: "singleSelect",
      valueOptions: ["Block", "Charge"],
    },
    actionsColumn((row) => [
      {
        label: "Edit",
        icon: Edit,
        // Map row fields to dialog expected props
        onClick: () => onEditCategory?.({ ...row, name: row.category_name }),
      },
      {
        label: "Delete",
        icon: Trash2,
        color: "error",
        onClick: () => handleDeleteClick(row),
      },
    ]),
  ];

  return (
    <ManagementPage
      title="Categories"
      addLabel="Add New Category"
      onAdd={() => onCreateCategory?.()}
      error={deleteDialogOpen ? null : error}
      onErrorClose={() => setError(null)}
      gridProps={{
        rows: categories,
        columns,
        loading,
        getRowId: (row) => row.category_id,
      }}
    >
      {/* Delete Confirmation Dialog */}
      <AppDialog
        open={deleteDialogOpen}
        onClose={handleDeleteCancel}
        maxWidth="xs"
      >
        <DialogTitle>
          <Stack direction="row" alignItems="center" spacing={1}>
            <AlertTriangle color="error" />
            <Typography variant="h6">Confirm Delete</Typography>
          </Stack>
        </DialogTitle>
        <DialogContent>
          <Typography mb={2}>
            Are you sure you want to delete "{categoryToDelete?.category_name}"?
            This action cannot be undone.
          </Typography>
          {error && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error}
            </Alert>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={handleDeleteCancel}>Cancel</Button>
          <Button
            variant="contained"
            color="error"
            onClick={handleDeleteConfirm}
            disabled={deleting}
          >
            {deleting ? <CircularProgress size={24} /> : "Delete"}
          </Button>
        </DialogActions>
      </AppDialog>
    </ManagementPage>
  );
};

export default CategoryList;
