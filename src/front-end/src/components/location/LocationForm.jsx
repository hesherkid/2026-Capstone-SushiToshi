import { useState } from "react";
import {
  Box,
  TextField,
  Button,
  DialogTitle,
  DialogContent,
  DialogActions,
  Stack,
} from "@mui/material";

const LocationForm = ({ initialData, onSubmit, onClose, mode }) => {
  const [formData, setFormData] = useState({
    name: initialData?.name || "",
    address_one: initialData?.address_one || "",
    address_two: initialData?.address_two || "",
    city: initialData?.city || "",
    province: initialData?.province || "",
    postal_code: initialData?.postal_code || "",
    phone_number: initialData?.phone_number || "",
  });

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData((prev) => ({
      ...prev,
      [name]: value,
    }));
  };

  const handleSubmit = (e) => {
    e.preventDefault();
    onSubmit(formData);
  };

  return (
    <form onSubmit={handleSubmit}>
      <DialogTitle>
        {mode === "create" ? "Add New Location" : "Edit Location"}
      </DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 2 }}>
          <TextField
            name="name"
            label="Name"
            value={formData.name}
            onChange={handleChange}
            required
            fullWidth
          />
          <TextField
            name="address_one"
            label="Address Line 1"
            value={formData.address_one}
            onChange={handleChange}
            required
            fullWidth
          />
          <TextField
            name="address_two"
            label="Address Line 2"
            value={formData.address_two}
            onChange={handleChange}
            fullWidth
          />
          <TextField
            name="city"
            label="City"
            value={formData.city}
            onChange={handleChange}
            required
            fullWidth
          />
          <TextField
            name="province"
            label="Province"
            value={formData.province}
            onChange={handleChange}
            required
            fullWidth
          />
          <TextField
            name="postal_code"
            label="Postal Code"
            value={formData.postal_code}
            onChange={handleChange}
            required
            fullWidth
          />
          <TextField
            name="phone_number"
            label="Phone Number"
            value={formData.phone_number}
            onChange={handleChange}
            required
            fullWidth
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button type="submit" variant="contained">
          {mode === "create" ? "Create" : "Save"}
        </Button>
      </DialogActions>
    </form>
  );
};

export default LocationForm;
