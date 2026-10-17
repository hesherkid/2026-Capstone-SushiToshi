import React, { useState, useEffect } from "react";
import {
  Box,
  TextField,
  Button,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Alert,
  CircularProgress,
  Paper,
  Typography,
  FormHelperText,
} from "@mui/material";
import { UserPlus, Save } from "lucide-react";
import api from "@/config/api";

const StaffManagementForm = ({
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
    location_Id: initialData?.location_Id || initialData?.location_id || "",
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

  const handleSubmit = async (e) => {
    e.preventDefault();

    await onSubmit(formData);
  };

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
    <Paper elevation={2} sx={{ p: 3, maxWidth: "sm", mx: "auto", mt: 4 }}>
      <Typography
        variant="h5"
        gutterBottom
        sx={{ display: "flex", alignItems: "center", gap: 1 }}
      >
        <UserPlus />
        {mode === "create" ? "Create New Staff Account" : "Edit Staff Account"}
      </Typography>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      <form onSubmit={handleSubmit}>
        <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
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

          <FormControl fullWidth>
            <InputLabel>Location</InputLabel>
            <Select
              name="location_Id"
              value={formData.location_Id}
              onChange={handleChange}
              label="Location"
            >
              <MenuItem value="">
                <em>None</em>
              </MenuItem>
              {locations.map((location) => (
                <MenuItem
                  key={location.location_Id}
                  value={location.location_Id}
                >
                  {location.name}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <Button
            type="submit"
            variant="contained"
            disabled={!isValid() || isLoading}
            startIcon={isLoading ? <CircularProgress size={20} /> : <Save />}
            fullWidth
            sx={{ mt: 2 }}
          >
            {mode === "create" ? "Create Account" : "Save Changes"}
          </Button>
        </Box>
      </form>
    </Paper>
  );
};

export default StaffManagementForm;
