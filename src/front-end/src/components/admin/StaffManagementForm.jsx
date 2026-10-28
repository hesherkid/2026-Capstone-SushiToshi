import React, { useState, useEffect } from "react";
import {
  Box,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Typography,
  FormHelperText,
  Chip,
} from "@mui/material";
import FormDialog from "@/components/common/FormDialog";
import api from "@/config/api";

// The Add / Edit Staff dialog. The page mounts it while open, so each opening starts fresh
const StaffManagementForm = ({
  open = true,
  onClose,
  onErrorClose,
  initialData = {},
  onSubmit,
  isLoading = false,
  error = null,
  mode = "create",
  // True when an admin edits their own account; the API won't let them change their own role
  lockRole = false,
}) => {
  const [locations, setLocations] = useState([]);
  const [formData, setFormData] = useState({
    email: initialData?.email || "",
    password: "",
    first_name: initialData?.first_name || "",
    last_name: initialData?.last_name || "",
    // The API returns "Admin"/"Staff"; the dropdown options are lowercase
    role: initialData?.role?.toLowerCase() || "staff",
    status: initialData?.status || "active",
    // The locations a staff member may work at (admins can work at every location)
    location_ids: initialData?.location_ids ?? [],
  });

  // Fetch locations when component mounts
  useEffect(() => {
    fetchLocations();
  }, []);

  const fetchLocations = async () => {
    try {
      const response = await api.get("/location");

      setLocations(response.data);
    } catch (error) {
      console.error("Error fetching locations:", error);
    }
  };

  const handleChange = (e) => {
    const { name, value } = e.target;

    setFormData((prev) => ({
      ...prev,
      [name]: value,
    }));
  };

  const handleSubmit = async () => {
    const { location_ids, ...account } = formData;
    // New staff start at the first location chosen
    await onSubmit(
      account.role === "staff"
        ? { ...account, location_ids, location_id: location_ids[0] ?? null }
        : account
    );
  };

  const locationName = (id) =>
    locations.find((location) => location.location_Id === id)?.name ?? id;

  const isValid = () => {
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return (
      formData.email.match(emailRegex) &&
      (mode === "edit" || formData.password.length >= 8) &&
      formData.first_name.trim() &&
      formData.last_name.trim() &&
      formData.role
    );
  };

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={mode === "create" ? "Create New Staff Account" : "Edit Staff Account"}
      subtitle={mode === "edit" && `${initialData?.first_name ?? ""} ${initialData?.last_name ?? ""}`.trim()}
      error={error}
      onErrorClose={onErrorClose}
      onSubmit={handleSubmit}
      submitLabel={mode === "create" ? "Create Account" : "Save Changes"}
      busyLabel={mode === "create" ? "Creating..." : "Saving..."}
      busy={isLoading}
      submitDisabled={!isValid()}
    >
      <TextField
        label="Email Address"
        name="email"
        type="email"
        value={formData.email}
        onChange={handleChange}
        required
        disabled={mode === "edit"}
        fullWidth
      />

      {mode === "create" && (
        <TextField
          label="Password"
          name="password"
          type="password"
          value={formData.password}
          onChange={handleChange}
          required
          fullWidth
          helperText="Minimum 8 characters"
        />
      )}

      <TextField
        label="First Name"
        name="first_name"
        value={formData.first_name}
        onChange={handleChange}
        required
        fullWidth
      />

      <TextField
        label="Last Name"
        name="last_name"
        value={formData.last_name}
        onChange={handleChange}
        required
        fullWidth
      />

      <FormControl fullWidth required disabled={lockRole}>
        <InputLabel>Role</InputLabel>
        <Select
          name="role"
          value={formData.role}
          onChange={handleChange}
          label="Role"
        >
          <MenuItem value="staff">Staff</MenuItem>
          <MenuItem value="admin">Admin</MenuItem>
        </Select>
        {lockRole && (
          <FormHelperText>You can't change your own role</FormHelperText>
        )}
      </FormControl>

      {formData.role === "staff" ? (
        <FormControl fullWidth>
          <InputLabel id="staff-locations-label">Locations</InputLabel>
          <Select
            labelId="staff-locations-label"
            name="location_ids"
            multiple
            value={formData.location_ids}
            onChange={handleChange}
            label="Locations"
            renderValue={(selected) => (
              <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.5 }}>
                {selected.map((id) => (
                  <Chip key={id} label={locationName(id)} size="small" />
                ))}
              </Box>
            )}
          >
            {locations.map((location) => (
              <MenuItem
                key={location.location_Id}
                value={location.location_Id}
              >
                {location.name}
              </MenuItem>
            ))}
          </Select>
          <FormHelperText>
            {formData.location_ids.length === 0
              ? "They can't work anywhere until you choose a location"
              : "They can switch between these locations"}
          </FormHelperText>
        </FormControl>
      ) : (
        <Typography variant="body2" color="text.secondary">
          Admins can work at every location.
        </Typography>
      )}
    </FormDialog>
  );
};

export default StaffManagementForm;
