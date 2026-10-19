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

const NewTableDialog = ({ open }) => {
  const { closeDialog, refreshData } = useTable();
  const [formData, setFormData] = useState({
    table_Number: "",
    seat_count: "",
    qr_Code_Url: "",
    is_Active: true,
    location_Id: storage.get("branch-location") || "",
  });
  const [availableLocations, setAvailableLocations] = useState([]);
  const [error, setError] = useState(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [locationList, setLocationList] = useState([]);

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
      table_Number: "",
      seat_count: "",
      qr_Code_Url: "",
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
      await api.post("/TableEntity", {
        Table_Number: parseInt(formData.table_Number),
        Seat_Count: parseInt(formData.seat_count),
        Qr_Code_Url: formData.qr_Code_Url,
        Is_Active: formData.is_Active,
        Location_Id: formData.location_Id,
      });
      await refreshData();
      handleClose();
    } catch (err) {
      setError(err.response.data || "Failed to create table");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <AppDialog open={open} onClose={handleClose}>
      <form onSubmit={handleSubmit}>
        <DialogTitle>
          Create New Table
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
              label="Table Number"
              type="number"
              required
              value={formData.table_Number}
              onChange={(e) =>
                setFormData({ ...formData, table_Number: e.target.value })
              }
            />
            <TextField
              label="Seat Count"
              type="number"
              required
              value={formData.seat_count}
              onChange={(e) =>
                setFormData({ ...formData, seat_count: e.target.value })
              }
            />
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={handleClose}>Cancel</Button>
          <Button type="submit" variant="contained" disabled={isSubmitting}>
            Create Table
          </Button>
        </DialogActions>
      </form>
    </AppDialog>
  );
};

export default NewTableDialog;
