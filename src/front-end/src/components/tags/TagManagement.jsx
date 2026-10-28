"use client";
import React, { useState, useEffect } from "react";
import { Refresh as RefreshIcon } from "@mui/icons-material";
import {
  Box,
  Button,
  TextField,
  Typography,
  InputAdornment,
} from "@mui/material";
import FormDialog from "@/components/common/FormDialog";
import { Edit, Trash2, Eye } from "lucide-react";
import api from "@/config/api";
import TagChip from "./TagChip";
import DeleteTagDialog from "./DeleteTagDialog";
import TagMenuItemsDialog from "./TagMenuItemsDialog";
import { serverMessage } from "@/utils/serverMessage";
import ManagementPage from "@/components/common/ManagementPage";
import { actionsColumn } from "@/components/common/RowActions";

const calculateColorDifference = (color1, color2) => {
  const hex2rgb = (hex) => {
    const r = parseInt(hex.slice(1, 3), 16);
    const g = parseInt(hex.slice(3, 5), 16);
    const b = parseInt(hex.slice(5, 7), 16);
    return [r, g, b];
  };

  const [r1, g1, b1] = hex2rgb(color1);
  const [r2, g2, b2] = hex2rgb(color2);

  return Math.abs(r1 - r2) + Math.abs(g1 - g2) + Math.abs(b1 - b2);
};

const generatePastelColor = () => {
  const r = Math.floor(Math.random() * 105) + 120;
  const g = Math.floor(Math.random() * 105) + 120;
  const b = Math.floor(Math.random() * 105) + 120;

  const adjustment = 30;
  const channel = Math.floor(Math.random() * 3);

  let finalR = r,
    finalG = g,
    finalB = b;

  switch (channel) {
    case 0:
      finalR = Math.max(120, Math.min(225, r - adjustment));
      break;
    case 1:
      finalG = Math.max(120, Math.min(225, g - adjustment));
      break;
    case 2:
      finalB = Math.max(120, Math.min(225, b - adjustment));
      break;
  }

  return `#${finalR.toString(16).padStart(2, "0")}${finalG
    .toString(16)
    .padStart(2, "0")}${finalB.toString(16).padStart(2, "0")}`;
};

// TODO: Extract to utils/colorGenerator.js, write tests, or use a library (chroma-js) to reduce complexity
const generateColorSet = (count, existingColors) => {
  const colors = new Set();
  const existingSet = new Set(existingColors);
  const minColorDifference = 75;

  let attempts = 0;
  const maxAttempts = 1000;

  while (colors.size < count && attempts < maxAttempts) {
    const newColor = generatePastelColor();
    let isDistinct = true;

    for (const existingColor of existingSet) {
      if (
        calculateColorDifference(newColor, existingColor) < minColorDifference
      ) {
        isDistinct = false;
        break;
      }
    }

    for (const color of colors) {
      if (calculateColorDifference(newColor, color) < minColorDifference) {
        isDistinct = false;
        break;
      }
    }

    if (isDistinct && !existingSet.has(newColor)) {
      colors.add(newColor);
    }

    attempts++;
  }

  if (colors.size < count) {
    const secondAttemptColors = new Set(colors);
    const reducedMinDifference = 50;

    while (secondAttemptColors.size < count && attempts < maxAttempts * 2) {
      const newColor = generatePastelColor();
      let isDistinct = true;

      for (const existingColor of [...existingSet, ...secondAttemptColors]) {
        if (
          calculateColorDifference(newColor, existingColor) <
          reducedMinDifference
        ) {
          isDistinct = false;
          break;
        }
      }

      if (isDistinct && !existingSet.has(newColor)) {
        secondAttemptColors.add(newColor);
      }

      attempts++;
    }

    return Array.from(secondAttemptColors);
  }

  return Array.from(colors);
};

const TagManagement = () => {
  const [tags, setTags] = useState([]);
  const [loading, setLoading] = useState(true);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [currentTag, setCurrentTag] = useState({ tag_name: "", tag_color: "" });
  const [error, setError] = useState(null);
  const [successMessage, setSuccessMessage] = useState(null);
  const [showColorPicker, setShowColorPicker] = useState(false);
  const [saving, setSaving] = useState(false);
  const [availableColors, setAvailableColors] = useState([]);
  const [dialogMode, setDialogMode] = useState("add");
  const [itemsDialogOpen, setItemsDialogOpen] = useState(false);
  const [itemsSuccess, setItemsSuccess] = useState(null);
  const [selectedTagForItems, setSelectedTagForItems] = useState(null);
  const [menuItems, setMenuItems] = useState([]);
  const [loadingItems, setLoadingItems] = useState(false);
  const [itemsError, setItemsError] = useState(null);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [tagToDelete, setTagToDelete] = useState(null);

  const columns = [
    {
      field: "tag_name",
      headerName: "Preview",
      flex: 0.25,
      sortable: true,
      renderCell: (params) => {
        if (!params?.row) return null;
        return (
          <Box sx={{ display: "flex", justifyContent: "left", width: "100%" }}>
            <TagChip tag={params.row} size="xl" />
          </Box>
        );
      },
    },
    {
      field: "tag_color",
      headerName: "Color Code",
      flex: 0.25,
      renderCell: (params) => {
        if (!params?.value) {
          return <Typography>N/A</Typography>;
        }
        return (
          <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
            <Box
              sx={{
                width: 24,
                height: 24,
                bgcolor: params.value,
                borderRadius: 1,
                border: "1px solid rgba(0, 0, 0, 0.1)",
              }}
            />
            <Typography>{params.value}</Typography>
          </Box>
        );
      },
    },
    actionsColumn(
      (tag) => [
        {
          label: "Edit",
          icon: Edit,
          onClick: () => handleOpenDialog("update", tag),
        },
        {
          label: "View Items",
          icon: Eye,
          onClick: () => handleOpenItemsDialog(tag),
        },
        {
          label: "Delete",
          icon: Trash2,
          color: "error",
          onClick: () => handleDelete(tag),
        },
      ],
      { flex: 0.25 },
    ),
  ];

  const handleDelete = async (tag) => {
    setError(null);
    setTagToDelete(tag);
    setDeleteDialogOpen(true);
  };

  // Returns false on failure, so the confirm dialog stays open and shows why
  const handleDeleteConfirm = async () => {
    try {
      const response = await api.delete(`/tag/${tagToDelete.tag_id}`);
      if (response.status !== 200) {
        throw new Error(response.data.detail || "Failed to delete tag");
      }
      setSuccessMessage("Tag deleted successfully");
      setTagToDelete(null);
      fetchTags();
    } catch (err) {
      setError(err?.response?.data?.detail || "Failed to delete tag");
      return false;
    }
  };

  const fetchMenuItemsForTag = async (tagId) => {
    try {
      setLoadingItems(true);
      setItemsError(null);

      const response = await api.get(`/tag/${tagId}/menu_items`);
      if (response.status !== 200) {
        throw new Error("Failed to fetch menu items");
      }

      setMenuItems(response.data);
    } catch (err) {
      setItemsError(
        err?.response?.data?.detail || "Failed to fetch menu items",
      );
      setMenuItems([]);
    } finally {
      setLoadingItems(false);
    }
  };

  const handleOpenItemsDialog = (tag) => {
    setSelectedTagForItems(tag);
    setError(null);
    setItemsSuccess(null);
    setItemsDialogOpen(true);
    fetchMenuItemsForTag(tag.tag_id);
  };

  const handleCloseItemsDialog = () => {
    setItemsDialogOpen(false);
    setError(null);
    setItemsSuccess(null);
  };

  const handleRemoveTagFromItem = async (item) => {
    setError(null);
    setItemsSuccess(null);
    try {
      await api.delete(
        `/menuItem/${item.item_id}/tags/${selectedTagForItems.tag_id}`,
      );
      setMenuItems((prev) => prev.filter((i) => i.item_id !== item.item_id));
      setItemsSuccess(`Tag removed from ${item.name}`);
    } catch (err) {
      setError(serverMessage(err, "Failed to remove tag from item"));
    }
  };

  const fetchTags = async () => {
    setLoading(true);
    try {
      // TODO: Tag/colours ???

      const response = await api.get("/Tag/colors");

      if (response.status !== 200) {
        throw new Error("Failed to fetch tags");
      }
      setTags(response.data);

      const existingColors = response.data.map((tag) => tag.tag_color);
      const newColors = generateColorSet(15, existingColors);
      setAvailableColors(newColors);

      if (newColors.length > 0 && !currentTag.tag_color) {
        setCurrentTag((prev) => ({ ...prev, tag_color: newColors[0] }));
      }
    } catch (err) {
      setError("Failed to fetch tags");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchTags();
  }, []);

  const handleOpenDialog = (mode, tag = null) => {
    const existingColors = tags.map((t) => t.tag_color);
    const newColors = generateColorSet(15, existingColors);
    setAvailableColors(newColors);

    if (mode === "add") {
      setCurrentTag({ tag_name: "", tag_color: newColors[0] });
    } else {
      setCurrentTag({
        tag_id: tag.tag_id,
        tag_name: tag.tag_name,
        tag_color: tag.tag_color,
      });
    }

    setError(null);   // the dialog shows the shared error
    setDialogMode(mode);
    setDialogOpen(true);
  };

  const handleCloseDialog = () => {
    setDialogOpen(false);
    setError(null);
    setShowColorPicker(false);
    setCurrentTag({ tag_name: "", tag_color: "" });
  };

  const handleColorClick = (color) => {
    setCurrentTag((prev) => ({ ...prev, tag_color: color }));
    setShowColorPicker(false);
  };

  const handleSubmit = async () => {
    if (!currentTag.tag_name.trim()) {
      setError("Tag name is required");
      return;
    }
    setSaving(true);
    try {
      if (dialogMode === "add") {
        const response = await api.post("/Tag", currentTag);
        if (response.status !== 201) {
          throw new Error("Failed to create tag");
        }
        setSuccessMessage("Tag created successfully");
      } else {
        const response = await api.put(`/Tag/${currentTag.tag_id}`, currentTag);
        if (response.status !== 200) {
          throw new Error("Failed to update tag");
        }
        setSuccessMessage("Tag updated successfully");
      }

      handleCloseDialog();
      fetchTags();
    } catch (err) {
      setError(err?.response?.data?.detail || `Failed to ${dialogMode} tag`);
    } finally {
      setSaving(false);
    }
  };

  return (
    <ManagementPage
      title="Tag Management"
      addLabel="Add New Tag"
      onAdd={() => handleOpenDialog("add")}
      error={dialogOpen || deleteDialogOpen || itemsDialogOpen ? null : error}
      onErrorClose={() => setError(null)}
      success={successMessage}
      onSuccessClose={() => setSuccessMessage(null)}
      gridProps={{
        rows: tags,
        columns,
        loading,
        getRowId: (row) => row.tag_id,
      }}
    >
      <FormDialog
        open={dialogOpen}
        onClose={handleCloseDialog}
        title={dialogMode === "add" ? "Add New Tag" : "Update Tag"}
        // A missing name shows under the field instead
        error={error && currentTag.tag_name?.trim() ? error : null}
        onErrorClose={() => setError(null)}
        onSubmit={handleSubmit}
        submitLabel={dialogMode === "add" ? "Save" : "Update"}
        busyLabel="Saving..."
        busy={saving}
      >
        <TextField
          autoFocus
          label="Tag Name"
          value={currentTag.tag_name}
          onChange={(e) =>
            setCurrentTag((prev) => ({ ...prev, tag_name: e.target.value }))
          }
          fullWidth
          error={Boolean(error && !currentTag.tag_name.trim())}
          helperText={
            error && !currentTag.tag_name.trim()
              ? "Tag name is required"
              : ""
          }
        />
        <TextField
          label="Color"
          value={currentTag.tag_color}
          onClick={() => setShowColorPicker(true)}
          InputProps={{
            startAdornment: (
              <InputAdornment position="start">
                <Box
                  sx={{
                    width: 24,
                    height: 24,
                    backgroundColor: currentTag.tag_color,
                    borderRadius: 1,
                    border: "1px solid rgba(0, 0, 0, 0.23)",
                    cursor: "pointer",
                  }}
                />
              </InputAdornment>
            ),
            readOnly: true,
            sx: { cursor: "pointer" },
          }}
          fullWidth
        />

        {showColorPicker && (
          <Box sx={{ width: "100%" }}>
            <Box
              sx={{
                display: "flex",
                justifyContent: "flex-end",
                mb: 2,
              }}
            >
              <Button
                size="small"
                startIcon={<RefreshIcon />}
                onClick={() => {
                  const existingColors = tags.map((tag) => tag.tag_color);
                  const newColors = generateColorSet(15, existingColors);
                  setAvailableColors(newColors);
                  if (newColors.length > 0) {
                    setCurrentTag((prev) => ({
                      ...prev,
                      tag_color: newColors[0],
                    }));
                  }
                }}
              >
                Refresh Colors
              </Button>
            </Box>
            <Box
              sx={{
                display: "grid",
                gridTemplateColumns: {
                  xs: "repeat(3, 1fr)",
                  sm: "repeat(5, 1fr)",
                },
                gap: 1,
              }}
            >
              {availableColors.map((color) => (
                <Box
                  key={color}
                  onClick={() => handleColorClick(color)}
                  sx={{
                    position: "relative",
                    width: "100%",
                    paddingBottom: "100%",
                  }}
                >
                  <Box
                    sx={{
                      position: "absolute",
                      top: 0,
                      left: 0,
                      right: 0,
                      bottom: 0,
                      backgroundColor: color,
                      borderRadius: 1,
                      cursor: "pointer",
                      border:
                        currentTag.tag_color === color
                          ? "2px solid black"
                          : "1px solid rgba(0, 0, 0, 0.23)",
                      "&:hover": {
                        opacity: 0.9,
                      },
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                      fontSize: { xs: "0.75rem", sm: "0.9rem" },
                      overflow: "hidden",
                      padding: "2px",
                    }}
                  >
                    {color}
                  </Box>
                </Box>
              ))}
            </Box>
          </Box>
        )}
      </FormDialog>

      <DeleteTagDialog
        open={deleteDialogOpen}
        tag={tagToDelete}
        error={deleteDialogOpen ? error : null}
        onClose={() => setDeleteDialogOpen(false)}
        onConfirm={handleDeleteConfirm}
      />

      <TagMenuItemsDialog
        open={itemsDialogOpen}
        tag={selectedTagForItems}
        items={menuItems}
        loading={loadingItems}
        loadError={itemsError}
        error={itemsDialogOpen ? error : null}
        onErrorClose={() => setError(null)}
        success={itemsSuccess}
        onSuccessClose={() => setItemsSuccess(null)}
        onClose={handleCloseItemsDialog}
        onRemoveItem={handleRemoveTagFromItem}
      />
    </ManagementPage>
  );
};

export default TagManagement;
