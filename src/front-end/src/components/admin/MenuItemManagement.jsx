import React, { useState, useEffect } from "react";
import { Box, Button, IconButton, Typography, Stack } from "@mui/material";
import AppDialog from "@/components/common/AppDialog";
import {
  Plus,
  Edit,
  Trash2,
  Menu as MenuIcon,
  AlertTriangle,
  Image as ImageIcon,
} from "lucide-react";
import MenuAssignmentDialog from "@/components/admin/MenuAssignmentDialog";
import TagChip from "@/components/tags/TagChip";
import TagsDialog from "@/components/admin/TagsDialog";
import CategoryManagementDialog from "@/components/admin/CategoryManagementDialog";
import api from "@/config/api";
import ManagementPage from "@/components/common/ManagementPage";
import { actionsColumn } from "@/components/common/RowActions";
import MenuItemForm from "@/components/admin/MenuItemForm";

const MenuItemManagement = () => {
  const [items, setItems] = useState([]);
  const [menus, setMenus] = useState([]);
  const [categories, setCategories] = useState([]);
  const [tags, setTags] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [selectedItem, setSelectedItem] = useState(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [assignmentDialogOpen, setAssignmentDialogOpen] = useState(false);
  const [tagDialogOpen, setTagDialogOpen] = useState(false);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [itemToDelete, setItemToDelete] = useState(null);
  const [imageFile, setImageFile] = useState(null);
  const [imagePreview, setImagePreview] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [categoryDialogOpen, setCategoryDialogOpen] = useState(false);
  const [selectedCategory, setSelectedCategory] = useState(null);
  const [successMessage, setSuccessMessage] = useState("");
  const [formKey, setFormKey] = useState(0);

  const fetchData = async () => {
    try {
      const [
        { data: itemsData },
        { data: categoriesData },
        { data: menusData },
        { data: tagsData },
      ] = await Promise.all([
        api.get("/menuItem"),
        api.get("/Category"),
        api.get("/Menu"),
        api.get("/tag/colors"),
      ]);
      setItems(Array.isArray(itemsData) ? itemsData : []);
      setCategories(Array.isArray(categoriesData) ? categoriesData : []);
      setMenus(Array.isArray(menusData) ? menusData : []);
      setTags(Array.isArray(tagsData) ? tagsData : []);
    } catch (err) {
      console.error("Failed to fetch data:", err);
      setError("Failed to fetch data");
      setItems([]);
      setCategories([]);
      setMenus([]);
      setTags([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {}, [items, categories, tags, menus]);

  useEffect(() => {
    fetchData();
  }, []);

  const handleTagAction = async (itemId, tagId, action) => {
    try {
      if (action === "add") {
        await api.post(`/menuitem/${itemId}/tags/${tagId}`);
      } else {
        await api.delete(`/menuitem/${itemId}/tags/${tagId}`);
      }
      await fetchData();
    } catch (err) {
      console.error(`Error ${action}ing tag:`, err);
      setError(`Failed to ${action} tag`);
    }
  };

  const handleCreate = () => {
    setSelectedItem(null);
    setImageFile(null);
    setImagePreview("");
    setError(null);
    setFormKey((k) => k + 1);
    setDialogOpen(true);
  };

  const handleEdit = (item) => {
    setSelectedItem(item);
    setImageFile(null);
    setImagePreview(item.item_Image_Url || "");
    setError(null);
    setFormKey((k) => k + 1);
    setDialogOpen(true);
  };

  const serverMessage = (err, fallback) => {
    const data = err?.response?.data;
    if (typeof data === "string" && data.trim()) return data;
    if (typeof data?.message === "string" && data.message.trim())
      return data.message;
    return fallback;
  };

  const handleDelete = async (item) => {
    setError(null);
    try {
      const { data } = await api.get(`/menuitem/${item.item_id}`);

      if (data?.menuAssignments?.length > 0) {
        setError(
          "Cannot delete item - it is currently assigned to one or more menus. Remove menu assignments first.",
        );
        return;
      }

      setItemToDelete(item);
      setDeleteDialogOpen(true);
    } catch (err) {
      setError(serverMessage(err, "Failed to check item assignments"));
    }
  };

  const handleDeleteConfirm = async () => {
    try {
      await api.delete(`/menuitem/${itemToDelete.item_id}`);
      await fetchData();
    } catch (err) {
      setError(serverMessage(err, "Failed to delete menu item"));
    } finally {
      setDeleteDialogOpen(false);
      setItemToDelete(null);
    }
  };

  const columns = [
    {
      field: "item_Image_Url",
      headerName: "Image",
      width: 100,
      sortable: false,
      filterable: false,
      renderCell: (params) =>
        params.value ? (
          <Box
            component="img"
            src={params.value}
            alt={params.row.name}
            sx={{ width: 60, height: 60, objectFit: "cover", borderRadius: 1 }}
          />
        ) : (
          <Box
            sx={{
              width: 60,
              height: 60,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              bgcolor: "grey.100",
              borderRadius: 1,
            }}
          >
            <ImageIcon size={24} color="grey" />
          </Box>
        ),
    },
    {
      field: "name",
      headerName: "Name",
      flex: 1,
      minWidth: 200,
    },
    {
      field: "category_Id",
      headerName: "Category",
      flex: 1,
      minWidth: 150,
      type: "singleSelect",
      valueOptions: categories.map((cat) => ({
        value: cat.category_id,
        label: cat.category_name,
      })),
      renderCell: (params) =>
        categories.find((c) => c.category_id === params.value)?.category_name ||
        "(No category)",
    },
    {
      field: "description",
      headerName: "Description & Tags",
      flex: 2,
      minWidth: 300,
      renderCell: (params) => (
        <Box sx={{ py: 1 }}>
          <Typography variant="body2" sx={{ mb: 1 }}>
            {params.row.description}
          </Typography>
          <Box
            sx={{
              display: "flex",
              flexWrap: "wrap",
              gap: 0.5,
              alignItems: "center",
            }}
          >
            {params.row.tags?.map((tag) => (
              <TagChip
                key={`tag-${tag.tag_id}`}
                tag={tag}
                isActionChip={true}
                isUsedFor="menu-item"
                onToggle={() =>
                  handleTagAction(params.row.item_id, tag.tag_id, "remove")
                }
              />
            ))}
            <IconButton
              size="small"
              aria-label="Manage Tags"
              onClick={() => {
                setSelectedItem(params.row);
                setTagDialogOpen(true);
              }}
            >
              <Plus size={16} />
            </IconButton>
          </Box>
        </Box>
      ),
    },
    actionsColumn((row) => [
      { label: "Edit", icon: Edit, onClick: () => handleEdit(row) },
      {
        label: "Assign to Menus",
        icon: MenuIcon,
        onClick: () => {
          setSelectedItem(row);
          setAssignmentDialogOpen(true);
        },
      },
      {
        label: "Delete",
        icon: Trash2,
        color: "error",
        onClick: () => handleDelete(row),
      },
    ]),
  ];

  const handleImageChange = (file) => {
    setImageFile(file);
    const reader = new FileReader();
    reader.onloadend = () => setImagePreview(reader.result);
    reader.readAsDataURL(file);
  };

  const handleFormClose = () => {
    if (submitting) return;
    setDialogOpen(false);
    setError(null);
  };

  const handleSubmit = async (formData) => {
    setSubmitting(true);
    setError(null);

    try {
      // Step 1: Save menu item basic data
      const savedItem = selectedItem
        ? await api.put(`/menuItem/${selectedItem.item_id}`, formData)
        : await api.post("/menuItem", formData);

      // Step 2: Upload the image if one was chosen
      if (imageFile) {
        const imageFormData = new FormData();
        imageFormData.append("file", imageFile);

        const itemId = selectedItem?.item_id ?? savedItem.data.item_id;
        await api.post(`/menuItem/${itemId}/image`, imageFormData, {
          headers: { "Content-Type": "multipart/form-data" },
        });
      }

      // Step 3: Refresh data and close dialog
      await fetchData();
      setDialogOpen(false);
      setImageFile(null);
      setImagePreview("");
      setSuccessMessage(
        selectedItem
          ? "Menu item updated successfully"
          : "Menu item created successfully",
      );
    } catch (err) {
      console.error("Error saving item:", err, err.response);
      setError("Failed to save menu item");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <ManagementPage
      title="Menu Items"
      addLabel="Add New Menu Item"
      onAdd={handleCreate}
      error={error}
      onErrorClose={() => setError(null)}
      success={successMessage}
      onSuccessClose={() => setSuccessMessage("")}
      gridProps={{
        rows: items,
        columns,
        loading,
        getRowId: (row) => row.item_id,
        getRowHeight: () => "auto",
        density: "comfortable",
        sx: { "& .MuiDataGrid-row": { minHeight: "100px !important" } },
      }}
    >
      <MenuItemForm
        key={formKey}
        open={dialogOpen}
        onClose={handleFormClose}
        selectedItem={selectedItem}
        categories={categories}
        imagePreview={imagePreview}
        onImageChange={handleImageChange}
        onSubmit={handleSubmit}
        submitting={submitting}
        error={error}
      />

      <TagsDialog
        open={tagDialogOpen}
        onClose={() => setTagDialogOpen(false)}
        selectedItem={selectedItem}
        tags={tags}
        onTagAction={handleTagAction}
      />

      <MenuAssignmentDialog
        open={assignmentDialogOpen}
        onClose={() => setAssignmentDialogOpen(false)}
        selectedItem={selectedItem}
        menus={menus}
        onSuccess={fetchData}
      />

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
            Are you sure you want to delete "{itemToDelete?.name}"? This action
            cannot be undone.
          </Typography>
          <Stack direction="row" spacing={1} justifyContent="flex-end">
            <Button onClick={() => setDeleteDialogOpen(false)}>Cancel</Button>
            <Button
              variant="contained"
              color="error"
              onClick={handleDeleteConfirm}
            >
              Delete
            </Button>
          </Stack>
        </Box>
      </AppDialog>

      <CategoryManagementDialog
        open={categoryDialogOpen}
        onClose={() => setCategoryDialogOpen(false)}
        selectedCategory={selectedCategory}
        onSuccess={fetchData}
      />
    </ManagementPage>
  );
};

export default MenuItemManagement;
