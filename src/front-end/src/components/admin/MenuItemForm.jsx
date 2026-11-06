import React, { useState, useRef } from "react";
import {
  Box,
  Button,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Card,
  CardMedia,
  Input,
  TextField,
} from "@mui/material";
import { Image as ImageIcon } from "lucide-react";
import FormDialog from "@/components/common/FormDialog";

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
  onErrorClose,
}) => {
  const [formData, setFormData] = useState({
    name: selectedItem?.name || "",
    description: selectedItem?.description || "",
    category_id: selectedItem?.category_Id ?? selectedItem?.category_id ?? "",
  });

  const handleFileChange = (e) => {
    const file = e.target.files?.[0];
    if (file) onImageChange(file);
    e.target.value = "";
  };
  const fileInputRef = useRef(null);

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={selectedItem ? "Edit Menu Item" : "Create Menu Item"}
      subtitle={selectedItem?.name}
      error={error}
      onErrorClose={onErrorClose}
      onSubmit={() => onSubmit(formData)}
      submitLabel="Save"
      busyLabel="Saving..."
      busy={submitting}
    >
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
        <input
          ref={fileInputRef}
          type="file"
          accept="image/*"
          onChange={handleFileChange}
          hidden
        />
        <Button
          variant="contained"
          onClick={() => fileInputRef.current?.click()}
          sx={{
            position: "absolute",
            bottom: 16,
            right: 16,
            // Dark enough for white text over any photo
            bgcolor: "rgba(0, 0, 0, 0.75)",
            "&:hover": { bgcolor: "rgba(0, 0, 0, 0.85)" },
          }}
        >
          {imagePreview ? "Change image" : "Add image"}
        </Button>
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
            <MenuItem key={category.category_id} value={category.category_id}>
              {category.category_name}
            </MenuItem>
          ))}
        </Select>
      </FormControl>
    </FormDialog>
  );
};

export default MenuItemForm;
