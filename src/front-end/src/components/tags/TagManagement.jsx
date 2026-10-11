"use client";
import React, { useState, useEffect } from "react";
import { DataGrid } from "@mui/x-data-grid";
import { Add as AddIcon, Refresh as RefreshIcon } from "@mui/icons-material";
import {
  Box,
  Button,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Typography,
  Alert,
  Snackbar,
  InputAdornment,
  IconButton,
  CircularProgress,
  Tooltip,
} from "@mui/material";
import { Plus, Edit, Image as ImageIcon, Trash2, Eye, X } from "lucide-react";
import { styled } from "@mui/material/styles";
import api from "@/config/api";
import TagChip from "./TagChip";
//import { isAuthenticated } from "@/utils/token";
// import { userAgentFromString } from "next/server";F

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

const AddButton = styled(IconButton)(({ theme }) => ({
  backgroundColor: theme.palette.primary.main,
  color: "white",
  padding: theme.spacing(1),
  minWidth: "auto",
  "&:hover": {
    backgroundColor: theme.palette.primary.dark,
  },
  "& svg": {
    width: 30,
    height: 30,
  },
}));

const TagManagement = () => {
  const [tags, setTags] = useState([]);
  const [dialogOpen, setDialogOpen] = useState(false);
const [currentTag, setCurrentTag] = useState({tag_name: "", tag_color: "" });
  const [error, setError] = useState(null);
  const [successMessage, setSuccessMessage] = useState(null);
  const [showColorPicker, setShowColorPicker] = useState(false);
  const [availableColors, setAvailableColors] = useState([]);
  const [dialogMode, setDialogMode] = useState("add");
  const [itemsDialogOpen, setItemsDialogOpen] = useState(false);
  const [selectedTagForItems, setSelectedTagForItems] = useState(null);
  const [menuItems, setMenuItems] = useState([]);
  const [loadingItems, setLoadingItems] = useState(false);
  const [itemsError, setItemsError] = useState(null);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [tagToDelete, setTagToDelete] = useState(null);

const columns = [
  {
    field: "tag_name",  // Changed from "name" to "tag_name"
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
    field: "tag_color",  // Changed from "color_code" to "tag_color"
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
  {
    field: "actions",
    headerName: "Actions",
    flex: 0.25,
    sortable: false,
    renderCell: (params) => {
      if (!params?.row) return null;
      return renderActionButtons(params.row);
    },
  },
];

  const handleDelete = async (tag) => {
    setTagToDelete(tag);
    setDeleteDialogOpen(true);
  };

  const handleDeleteConfirm = async () => {
    try {
      const response = await api.delete(`/tag/${tagToDelete.tag_id}`);
      if (response.status !== 200) {
        throw new Error(response.data.detail || "Failed to delete tag");
      }
      setSuccessMessage("Tag deleted successfully");
      setDeleteDialogOpen(false);
      setTagToDelete(null);
      fetchTags();
    } catch (err) {
      setError(err?.response?.data?.detail || "Failed to delete tag");
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
        err?.response?.data?.detail || "Failed to fetch menu items"
      );
      setMenuItems([]);
    } finally {
      setLoadingItems(false);
    }
  };

  const handleOpenItemsDialog = (tag) => {
    setSelectedTagForItems(tag);
    setItemsDialogOpen(true);
    fetchMenuItemsForTag(tag.tag_id);
  };

  const renderActionButtons = (tag) => (
    <Box sx={{ display: "flex", gap: 1 }}>
      <Tooltip title="Edit">
        <IconButton
          color="light"
          onClick={() => handleOpenDialog("update", tag)}
        >
          <Edit size={20} />
        </IconButton>
      </Tooltip>
      <Tooltip title="View Items">
        <IconButton color="light" onClick={() => handleOpenItemsDialog(tag)}>
          <Eye size={20} />
        </IconButton>
      </Tooltip>
      <Tooltip title="Delete">
        <IconButton color="dark" onClick={() => handleDelete(tag)}>
          <Trash2 size={20} />
        </IconButton>
      </Tooltip>
    </Box>
  );

  const DeleteConfirmationDialog = () => (
    <Dialog
      open={deleteDialogOpen}
      onClose={() => setDeleteDialogOpen(false)}
      maxWidth="xs"
      fullWidth
    >
      <DialogTitle>Confirm Delete</DialogTitle>
      <DialogContent>
        <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
          <Typography>Are you sure you want to delete this tag?</Typography>
          {tagToDelete && (
            <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
              <Typography>Tag:</Typography>
              <TagChip tag={tagToDelete} size="l" />
            </Box>
          )}
          <Typography color="error">
            This action will remove the tag from all menu items and cannot be
            undone.
          </Typography>
        </Box>
      </DialogContent>
      <DialogActions>
        <Button onClick={() => setDeleteDialogOpen(false)}>Cancel</Button>
        <Button onClick={handleDeleteConfirm} variant="contained" color="error">
          Delete
        </Button>
      </DialogActions>
    </Dialog>
  );

  const MenuItemsDialog = () => {
    const handleRemoveTag = async (itemId) => {
      try {
        const response = await api.delete(`/menu-items/${itemId}/tags/${selectedTagForItems.tag_id}`);

        if (response.status !== 200) {
          throw new Error("Failed to remove tag from item");
        }
        setMenuItems(menuItems.filter((item) => item.item_id !== itemId));

        setSuccessMessage(
          `Tag removed from ${
            menuItems.find((i) => i.item_id === itemId)?.name
          }`
        );
      } catch (err) {
        setError(
          err?.response?.data?.detail || "Failed to remove tag from item"
        );
      }
    };

    return (
      <Dialog
        open={itemsDialogOpen}
        onClose={() => setItemsDialogOpen(false)}
        maxWidth="md"
        fullWidth
      >
        <DialogTitle sx={{ pb: 1 }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 2 }}>
            <Typography>Menu Items with Tag:</Typography>
            {selectedTagForItems && (
              <TagChip tag={selectedTagForItems} size="l" />
            )}
          </Box>
        </DialogTitle>
        <DialogContent dividers>
          {loadingItems ? (
            <Box sx={{ display: "flex", justifyContent: "center", p: 3 }}>
              <CircularProgress />
            </Box>
          ) : itemsError ? (
            <Alert severity="error" sx={{ mb: 2 }}>
              {itemsError}
            </Alert>
          ) : menuItems.length === 0 ? (
            <Typography color="text.secondary" align="center" sx={{ py: 3 }}>
              No menu items found with this tag
            </Typography>
          ) : (
            <TableContainer>
              <Table>
                <TableHead>
                  <TableRow>
                    <TableCell sx={{ color: "white" }}>Image</TableCell>
                    <TableCell sx={{ color: "white" }}>Name</TableCell>
                    <TableCell sx={{ color: "white" }}>Description</TableCell>
                    <TableCell sx={{ color: "white" }} align="center">
                      Actions
                    </TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {menuItems.map((item) => (
                    <TableRow key={item.item_id} hover>
                      <TableCell sx={{ width: 100 }}>
                        {item.item_image_url ? (
                          <Box
                            component="img"
                            src={item.item_image_url}
                            alt={item.name}
                            sx={{
                              width: 60,
                              height: 60,
                              objectFit: "cover",
                              borderRadius: 1,
                            }}
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
                        )}
                      </TableCell>
                      <TableCell>
                        <Typography variant="body1">{item.name}</Typography>
                      </TableCell>
                      <TableCell>
                        <Typography variant="body2" color="text.secondary">
                          {item.description}
                        </Typography>
                      </TableCell>
                      <TableCell
                        align="right"
                        sx={{
                          width: "100px",
                          padding: "0 16px",
                        }}
                      >
                        <Box
                          sx={{
                            display: "flex",
                            justifyContent: "center",
                            alignItems: "center",
                            height: "100%",
                          }}
                        >
                          <Tooltip title="Remove tag">
                            <IconButton
                              onClick={() => handleRemoveTag(item.item_id)}
                              color="error"
                              size="small"
                              sx={{
                                "&:hover": {
                                  backgroundColor: "error.light",
                                  color: "error.dark",
                                },
                              }}
                            >
                              <X size={18} />
                            </IconButton>
                          </Tooltip>
                        </Box>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setItemsDialogOpen(false)}>Close</Button>
        </DialogActions>
      </Dialog>
    );
  };

  const fetchTags = async () => {
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
    } 
    else {
      setCurrentTag({
        tag_id: tag.tag_id,
        tag_name: tag.tag_name,
        tag_color: tag.tag_color,
      });
    }

    setDialogMode(mode);
    setDialogOpen(true);
  };

  const handleCloseDialog = () => {
    setDialogOpen(false);
    setError(null);
    setShowColorPicker(false);
    setCurrentTag({ name: "", color_code: "" });
  };

  const handleColorClick = (color) => {
    setCurrentTag((prev) => ({ ...prev, tag_color: color }));
    setShowColorPicker(false);
  };

  const handleSubmit = async () => {
    try {
      if (!currentTag.tag_name.trim()) {
        setError("Tag name is required");
        return;
      }

      if (dialogMode === "add") {
        const response = await api.post("/Tag" , currentTag);
        if (response.status !== 201) {
          throw new Error("Failed to create tag");
        }
        setSuccessMessage("Tag created successfully");
      } else {
        const response = await api.put(
          `/Tag/${currentTag.tag_id}`,
          currentTag
        );
        if (response.status !== 200) {
          throw new Error("Failed to update tag");
        }
        setSuccessMessage("Tag updated successfully");
      }

      handleCloseDialog();
      fetchTags();
    } catch (err) {
      setError(err?.response?.data?.detail || `Failed to ${dialogMode} tag`);
    }
  };

  return (
    <>
      <Box
        sx={{
          display: "flex",
          justifyContent: "left",
          alignItems: "center",
          p: 3,
          width: "100%",
        }}
      >
        <Typography variant="h5" component="h1" sx={{ fontWeight: "bold" }}>
          Tag Management
        </Typography>
        <Button sx={{ ml: 2 }} onClick={() => handleOpenDialog("add")}>
          <Plus />
          Add New Tag
        </Button>
      </Box>

      <Box
        sx={{
          height: "100%",
          width: "100%",
          "& .MuiDataGrid-root": {
            border: "none",
            backgroundColor: "background.paper",
            "& .MuiDataGrid-cell:focus": {
              outline: "none",
            },
          },
          "& .MuiDataGrid-columnHeaders": {
            backgroundColor: "primary.main",
            color: "primary.contrastText",
            "& .MuiDataGrid-columnHeaderTitle": {
              color: "black",
            },
          },
        }}
      >
        <DataGrid
          rows={tags}
          columns={columns}
          getRowId={(row) => row.tag_id}
          disableColumnMenu
          disableRowSelectionOnClick
          pageSizeOptions={[5, 10, 25]}
          initialState={{
            pagination: {
              paginationModel: { pageSize: 10 },
            },
          }}
        />
      </Box>

      <Dialog
        open={dialogOpen}
        onClose={handleCloseDialog}
        maxWidth="sm"
        fullWidth
        PaperProps={{
          sx: {
            maxHeight: "90vh",
            width: { xs: "95%", sm: "80%", md: "600px" },
          },
        }}
      >
        <DialogTitle sx={{ pb: 1 }}>
          {dialogMode === "add" ? "Add New Tag" : "Update Tag"}
        </DialogTitle>
        <DialogContent dividers>
          <Box
            sx={{
              display: "flex",
              flexDirection: "column",
              gap: 3,
              pt: 1,
            }}
          >
            <TextField
              label="Tag Name"
              value={currentTag.tag_name}
              onChange={(e) =>
                setCurrentTag((prev) => ({ ...prev, tag_name: e.target.value }))
              }
              fullWidth
              error={Boolean(error && !currentTag.tag_name.trim())}
              helperText={
                error && !currentTag.tag_name.trim() ? "Tag name is required" : ""
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
                            currentTag.tag_color  === color
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
          </Box>
        </DialogContent>
        <DialogActions sx={{ p: 2 }}>
          <Button onClick={handleCloseDialog}>Cancel</Button>
          <Button onClick={handleSubmit} variant="contained">
            {dialogMode === "add" ? "Save" : "Update"}
          </Button>
        </DialogActions>
      </Dialog>

      <DeleteConfirmationDialog />
      <MenuItemsDialog />

      <Snackbar
        open={Boolean(error || successMessage)}
        autoHideDuration={6000}
        onClose={() => {
          setError(null);
          setSuccessMessage(null);
        }}
        anchorOrigin={{ vertical: "bottom", horizontal: "center" }}
      >
        <Alert
          severity={error ? "error" : "success"}
          onClose={() => {
            setError(null);
            setSuccessMessage(null);
          }}
          sx={{ width: "100%" }}
        >
          {error || successMessage}
        </Alert>
      </Snackbar>
    </>
  );
};

export default TagManagement;
