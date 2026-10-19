import { useState, useEffect } from "react";
import api from "@/config/api";
import storage from "@/utils/storage";
import {
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  IconButton,
  Typography,
} from "@mui/material";
import AppDialog from "@/components/common/AppDialog";
import { X } from "lucide-react";
import { useSession } from "../../context/SessionContext";

const AddTableDialog = ({ open, sessionId, onClose }) => {
  const { addTable } = useSession();
  const [selectedTable, setSelectedTable] = useState("");
  const [availableTables, setAvailableTables] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (open) {
      fetchTables();
    } else {
      setSelectedTable("");
      setError(null);
    }
  }, [open]);

  const fetchTables = async () => {
    try {
      setIsLoading(true);
      setError(null);

      // First, get all tables for location
      const tablesResponse = await api.get(
        `/Location/${storage.get("branch-location")}/tables`
      );
      const locationTables = tablesResponse.data;
      console.log(locationTables);

      // Filter out tables that are active and not in use in any session
      const availableTables = locationTables.filter((table) => table.is_active);

      setAvailableTables(availableTables);
    } catch (err) {
      console.error("Error fetching tables:", err);
      setError("Failed to load available tables");
    } finally {
      setIsLoading(false);
    }
  };

  const handleSubmit = async () => {
    if (!sessionId || !selectedTable) return;

    try {
      const success = await addTable(sessionId, selectedTable);
      if (success) {
        // Remove the selected table from the available tables list
        setAvailableTables((prevTables) =>
          prevTables.filter((table) => table.table_id !== selectedTable)
        );

        // Reset the selected table
        setSelectedTable("");
        onClose();
      }
    } catch (err) {
      setError("Failed to add table");
    }
  };

  return (
    <AppDialog open={open} onClose={onClose}>
      <DialogTitle>
        Add Table to Session
        <IconButton
          onClick={onClose}
          sx={{ position: "absolute", right: 8, top: 8 }}
        >
          <X />
        </IconButton>
      </DialogTitle>
      <DialogContent>
        {error && (
          <Typography color="error" sx={{ mb: 2 }}>
            {error}
          </Typography>
        )}
        <FormControl fullWidth sx={{ mt: 2 }}>
          <InputLabel>Select Table</InputLabel>
          <Select
            value={selectedTable}
            onChange={(e) => {
              console.log(e.target.value);
              setSelectedTable(e.target.value);
            }}
            label="Select Table"
            disabled={isLoading}
          >
            {availableTables.length === 0 ? (
              <MenuItem disabled value="">
                {isLoading ? "Loading tables..." : "No available tables"}
              </MenuItem>
            ) : (
              availableTables.map((table) => (
                <MenuItem key={table.table_id} value={table.table_id}>
                  Table {table.table_number} ({table.seat_count} seats)
                </MenuItem>
              ))
            )}
          </Select>
        </FormControl>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button
          onClick={handleSubmit}
          variant="contained"
          disabled={!selectedTable || isLoading}
        >
          Add Table
        </Button>
      </DialogActions>
    </AppDialog>
  );
};

export default AddTableDialog;
