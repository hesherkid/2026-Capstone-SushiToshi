import React, { useState } from "react";
import {
  Box,
  Button,
  Typography,
  CircularProgress,
  Alert,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Stack,
  Card,
  CardMedia,
  Input,
  TextField,
} from "@mui/material";
import { Image as ImageIcon } from "lucide-react";
import AppDialog from "@/components/common/AppDialog";

const MenuItemForm = ({
  open,
  onClose,
  selectedItem,
  categories,
  imagePreview,
  onImageChange,
  onSubmit,
  submitting,
  error,
}) => {
  const [formData, setFormData] = useState({
    name: selectedItem?.name || "",
    description: selectedItem?.description || "",
    category_id: selectedItem?.category_Id ?? selectedItem?.category_id ?? "",
  });

  const handleSubmit = (e) => {
    e.preventDefault();
    onSubmit(formData);
  };

  const handleFileChange = (e) => {
    const file = e.target.files?.[0];
    if (file) onImageChange(file);
    e.target.value = "";
  };

  return (
    <AppDialog open={open} onClose={onClose}>
      <Box component="form" onSubmit={handleSubmit} sx={{ p: 3 }}>
        <Typography variant="h6" gutterBottom>
          {selectedItem ? "Edit Menu Item" : "Create Menu Item"}
        </Typography>

        {error && (
          <Alert severity="error" sx={{ mb: 3 }}>
            {error}
          </Alert>
        )}

        <Stack spacing={3}>
          <Card
            sx={{
              aspectRatio: "16/9",
              overflow: "hidden",
              position: "relative",
            }}
          >
            {imagePreview ? (
              <CardMedia
                component="img"
                image={imagePreview}
                alt="Item preview"
                sx={{ height: "100%", objectFit: "cover" }}
              />
            ) : (
              <Box
                sx={{
                  height: "100%",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  bgcolor: "grey.100",
                }}
              >
                <ImageIcon size={48} color="grey" />
              </Box>
            )}
            <Input
              type="file"
              inputProps={{ accept: "image/*" }}
              onChange={handleFileChange}
              sx={{ display: "none" }}
              id="menu-item-image-input"
            />
            <label htmlFor="menu-item-image-input">
              <Button
                component="span"
                variant="contained"
                sx={{
                  position: "absolute",
                  bottom: 16,
                  right: 16,
                  bgcolor: "rgba(0, 0, 0, 0.5)",
                  "&:hover": { bgcolor: "rgba(0, 0, 0, 0.7)" },
                }}
              >
                Change Image
              </Button>
            </label>
          </Card>

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
              setFormData((prev) => ({ ...prev, description: e.target.value }))
            }
            multiline
            rows={3}
            fullWidth
          />

          <FormControl fullWidth required>
            <InputLabel id="menu-item-category-label">Category</InputLabel>
            <Select
              labelId="menu-item-category-label"
              value={formData.category_id}
              onChange={(e) =>
                setFormData((prev) => ({
                  ...prev,
                  category_id: e.target.value,
                }))
              }
              label="Category"
            >
              {categories.map((category) => (
                <MenuItem
                  key={category.category_id}
                  value={category.category_id}
                >
                  {category.category_name}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <Box sx={{ display: "flex", justifyContent: "flex-end", gap: 1 }}>
            <Button onClick={onClose} disabled={submitting}>
              Cancel
            </Button>
            <Button type="submit" variant="contained" disabled={submitting}>
              {submitting ? <CircularProgress size={24} /> : "Save"}
            </Button>
          </Box>
        </Stack>
      </Box>
    </AppDialog>
  );
};

export default MenuItemForm;
