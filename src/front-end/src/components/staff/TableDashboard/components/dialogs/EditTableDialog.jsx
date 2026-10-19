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
  FormControlLabel,
  Select,
  InputLabel,
  MenuItem,
} from "@mui/material";
import AppDialog from "@/components/common/AppDialog";
import { useTable } from "../../context/TableContext";
import api from "@/config/api";

const EditTableDialog = ({ open, table }) => {
  const { closeDialog, refreshData } = useTable();
  const [formData, setFormData] = useState({
    table_Number: "",
    seat_count: "",
    qr_Code_Url: "",
    Location: "",
  });
  const [error, setError] = useState(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (table) {
      setFormData({
        table_Number: table.table_number || "",
        seat_count: table.seat_count || "",
        qr_Code_Url: table.qr_Code || "",
      });
    }
  }, [table]);

  const handleClose = () => {
    setError(null);
    closeDialog();
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setIsSubmitting(true);
    setError(null);

    try {
      await api.put(`/TableEntity/${table.table_Id}`, {
        Table_Number: parseInt(formData.table_Number),
        Seat_Count: parseInt(formData.seat_count),
        Qr_Code_Url: formData.qr_Code_Url,
      });
      await refreshData();
      handleClose();
    } catch (err) {
      setError(err.response.data || "Failed to update table");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <AppDialog open={open} onClose={handleClose}>
      <form onSubmit={handleSubmit}>
        <DialogTitle>Edit Table</DialogTitle>
        <DialogContent>
          {error && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error}
            </Alert>
          )}
          <Box display="flex" flexDirection="column" gap={2} mt={1}>
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
            Save Changes
          </Button>
        </DialogActions>
      </form>
    </AppDialog>
  );
};

export default EditTableDialog;
