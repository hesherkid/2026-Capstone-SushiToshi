import { useState, useEffect } from "react";
import { MenuItem, TextField } from "@mui/material";
import FormDialog from "@/components/common/FormDialog";
import api from "@/config/api";
import storage from "@/utils/storage";
import serverMessage from "@/utils/serverMessage";
import { useSession } from "../../context/SessionContext";

const AddTableDialog = ({ open, sessionId, onClose }) => {
  const { addTable } = useSession();
  const [selectedTable, setSelectedTable] = useState("");
  const [availableTables, setAvailableTables] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    setSelectedTable("");
    setError(null);
    if (!open) return;
    setIsLoading(true);
    api
      .get(`/Location/${storage.get("branch-location")}/tables`)
      .then((response) => setAvailableTables(response.data.filter((table) => table.is_active)))
      .catch((err) => setError(serverMessage(err, "Couldn't load the tables.")))
      .finally(() => setIsLoading(false));
  }, [open]);

  // A failure is shown as a notification by the session actions; the dialog stays open to retry
  const handleSubmit = async () => {
    setSubmitting(true);
    try {
      if (await addTable(sessionId, selectedTable)) onClose();
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title="Add Table to Session"
      error={error}
      onErrorClose={() => setError(null)}
      onSubmit={handleSubmit}
      submitLabel="Add Table"
      busyLabel="Adding..."
      busy={submitting}
      submitDisabled={!sessionId || !selectedTable || isLoading}
    >
      <TextField
        select
        label="Table"
        required
        value={selectedTable}
        onChange={(e) => setSelectedTable(e.target.value)}
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
      </TextField>
    </FormDialog>
  );
};

export default AddTableDialog;
