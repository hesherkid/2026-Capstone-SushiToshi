import { useState, useEffect } from "react";
import { Alert, Box, Chip, MenuItem, TextField } from "@mui/material";
import FormDialog from "@/components/common/FormDialog";
import { useTable } from "../../context/TableContext";
import { TABLE_STATUS } from "../../hooks/useTableData";
import api from "@/config/api";
import serverMessage from "@/utils/serverMessage";

const AddTableToGroupDialog = ({ open, group }) => {
  const { closeDialog, refreshData, tables } = useTable();
  const [selectedTableId, setSelectedTableId] = useState("");
  const [availableTables, setAvailableTables] = useState([]);
  const [error, setError] = useState(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (open && group) {
      // Ungrouped tables that are active and not in a session
      setAvailableTables(
        tables.filter((t) => !t.tableGroup_Id && t.status === TABLE_STATUS.AVAILABLE)
      );
    }
  }, [open, group, tables]);

  const handleClose = () => {
    setSelectedTableId("");
    setError(null);
    closeDialog();
  };

  const handleSubmit = async () => {
    setIsSubmitting(true);
    setError(null);
    try {
      await api.post(`/TableGroup/${group.tableGroup_Id}/tables/${selectedTableId}`);
      await refreshData();
      handleClose();
    } catch (err) {
      setError(serverMessage(err, "Couldn't add the table to the group."));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <FormDialog
      open={open}
      onClose={handleClose}
      title="Add Table to Group"
      subtitle={group?.group_Name}
      error={error}
      onErrorClose={() => setError(null)}
      onSubmit={handleSubmit}
      submitLabel="Add Table"
      busyLabel="Adding..."
      busy={isSubmitting}
      submitDisabled={!selectedTableId}
    >
      {availableTables.length === 0 ? (
        <Alert severity="info">
          No available tables. All tables are either inactive, in a session or already in a group.
        </Alert>
      ) : (
        <TextField
          select
          label="Table"
          required
          value={selectedTableId}
          onChange={(e) => setSelectedTableId(e.target.value)}
        >
          {availableTables.map((table) => (
            <MenuItem key={table.table_Id} value={table.table_Id}>
              <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                Table {table.table_number}
                <Chip label={`${table.seat_count} seats`} size="small" variant="outlined" />
              </Box>
            </MenuItem>
          ))}
        </TextField>
      )}
    </FormDialog>
  );
};

export default AddTableToGroupDialog;
