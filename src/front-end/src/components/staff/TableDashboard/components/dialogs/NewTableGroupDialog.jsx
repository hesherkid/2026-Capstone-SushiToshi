import { useState, useEffect } from "react";
import {
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Button,
  Box,
  Alert,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  IconButton,
} from "@mui/material";
import AppDialog from "@/components/common/AppDialog";
import { X } from "lucide-react";
import { useTable } from "../../context/TableContext";
import api from "@/config/api";
import storage from "@/utils/storage";

const NewTableGroupDialog = ({ open }) => {
  const { closeDialog, refreshData } = useTable();
  const [formData, setFormData] = useState({
    group_Name: "",
    is_Active: true,
    location_Id: storage.get("branch-location") || "",
  });
  const [availableLocations, setAvailableLocations] = useState([]);
  const [error, setError] = useState(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (open) {
      fetchLocations();
    }
  }, [open]);

  const fetchLocations = async () => {
    try {
      const response = await api.get("/Location");
      if (response.status !== 200) throw new Error("Failed to fetch locations");
      setAvailableLocations(response.data);
    } catch (err) {
      console.error("Error fetching locations:", err);
    }
  };

  const handleClose = () => {
    setFormData({
      group_Name: "",
      is_Active: true,
      location_Id: storage.get("branch-location") || "",
    });
    setError(null);
    closeDialog();
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setIsSubmitting(true);
    setError(null);
    try {
      await api.post("/TableGroup", {
        Group_Name: formData.group_Name,
        Is_Active: formData.is_Active,
        Location_Id: formData.location_Id,
      });
      await refreshData();
      handleClose();
    } catch (err) {
      setError(err.response.data || "Failed to create table group");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <AppDialog open={open} onClose={handleClose}>
      <form onSubmit={handleSubmit}>
        <DialogTitle>
          Create New Table Group
          <IconButton
            onClick={handleClose}
            sx={{ position: "absolute", right: 8, top: 8 }}
          >
            <X />
          </IconButton>
        </DialogTitle>
        <DialogContent>
          {error && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error}
            </Alert>
          )}
          <Box display="flex" flexDirection="column" gap={2} mt={1}>
            <FormControl fullWidth required>
              <InputLabel>Select Location</InputLabel>
              <Select
                value={formData.location_Id}
                onChange={(e) =>
                  setFormData({ ...formData, location_Id: e.target.value })
                }
                label="Select Location"
              >
                {availableLocations.map((location) => (
                  <MenuItem
                    key={location.location_Id}
                    value={location.location_Id}
                  >
                    {location.name}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <TextField
              label="Group Name"
              required
              value={formData.group_Name}
              onChange={(e) =>
                setFormData({ ...formData, group_Name: e.target.value })
              }
              placeholder="e.g., Large Party Area"
            />
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={handleClose}>Cancel</Button>
          <Button type="submit" variant="contained" disabled={isSubmitting}>
            Create Group
          </Button>
        </DialogActions>
      </form>
    </AppDialog>
  );
};

export default NewTableGroupDialog;
