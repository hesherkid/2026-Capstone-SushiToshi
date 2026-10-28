import { useState, useEffect } from "react";
import { TextField } from "@mui/material";
import FormDialog from "@/components/common/FormDialog";
import { useTable } from "../../context/TableContext";
import api from "@/config/api";
import serverMessage from "@/utils/serverMessage";

const EditTableDialog = ({ open, table }) => {
  const { closeDialog, refreshData } = useTable();
  const [formData, setFormData] = useState({ table_Number: "", seat_count: "" });
  const [error, setError] = useState(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (table) {
      setFormData({
        table_Number: table.table_number || "",
        seat_count: table.seat_count || "",
      });
    }
  }, [table]);

  const set = (field) => (e) => setFormData((prev) => ({ ...prev, [field]: e.target.value }));

  const handleClose = () => {
    setError(null);
    closeDialog();
  };

  const handleSubmit = async () => {
    setIsSubmitting(true);
    setError(null);
    try {
      await api.put(`/TableEntity/${table.table_Id}`, {
        Table_Number: parseInt(formData.table_Number),
        Seat_Count: parseInt(formData.seat_count),
        Qr_Code_Url: table.qr_Code || "",
      });
      await refreshData();
      handleClose();
    } catch (err) {
      setError(serverMessage(err, "Couldn't update the table."));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <FormDialog
      open={open}
      onClose={handleClose}
      title="Edit Table"
      subtitle={table && `Table ${table.table_number}`}
      error={error}
      onErrorClose={() => setError(null)}
      onSubmit={handleSubmit}
      submitLabel="Save Changes"
      busyLabel="Saving..."
      busy={isSubmitting}
    >
      <TextField
        label="Table Number"
        type="number"
        required
        autoFocus
        value={formData.table_Number}
        onChange={set("table_Number")}
        slotProps={{ htmlInput: { min: 1 } }}
      />
      <TextField
        label="Seat Count"
        type="number"
        required
        value={formData.seat_count}
        onChange={set("seat_count")}
        slotProps={{ htmlInput: { min: 1 } }}
      />
    </FormDialog>
  );
};

export default EditTableDialog;
