import { useState, useEffect } from "react";
import { TextField, MenuItem } from "@mui/material";
import FormDialog from "@/components/common/FormDialog";
import { useTable } from "../../context/TableContext";
import api from "@/config/api";
import storage from "@/utils/storage";
import serverMessage from "@/utils/serverMessage";

// The working location chosen in the app bar, as a number to match the location ids
const getWorkingLocation = () => {
  const stored = storage.get("branch-location");
  return stored ? Number(stored) : "";
};

const emptyForm = () => ({
  table_number: "",
  seat_count: "",
  location_id: getWorkingLocation(),
});

const NewTableDialog = ({ open }) => {
  const { closeDialog, refreshData } = useTable();
  const [formData, setFormData] = useState(emptyForm);
  const [availableLocations, setAvailableLocations] = useState([]);
  const [error, setError] = useState(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (!open) return;
    api
      .get("/Location")
      .then((response) => setAvailableLocations(response.data))
      .catch((err) => setError(serverMessage(err, "Couldn't load locations.")));
  }, [open]);

  // Tables can only be added at the working location; the list is only open without one
  const workingLocation = getWorkingLocation();
  const locationOptions = workingLocation
    ? availableLocations.filter((location) => location.location_id === workingLocation)
    : availableLocations;

  const set = (field) => (e) => setFormData((prev) => ({ ...prev, [field]: e.target.value }));

  const handleClose = () => {
    setFormData(emptyForm());
    setError(null);
    closeDialog();
  };

  const handleSubmit = async () => {
    setIsSubmitting(true);
    setError(null);
    try {
      await api.post("/TableEntity", {
        table_number: parseInt(formData.table_number),
        seat_count: parseInt(formData.seat_count),
        qr_code_url: "",
        is_active: true,
        location_id: formData.location_id,
      });
      await refreshData();
      handleClose();
    } catch (err) {
      setError(serverMessage(err, "Couldn't create the table."));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <FormDialog
      open={open}
      onClose={handleClose}
      title="Create New Table"
      error={error}
      onErrorClose={() => setError(null)}
      onSubmit={handleSubmit}
      submitLabel="Create Table"
      busyLabel="Creating..."
      busy={isSubmitting}
    >
      <TextField
        select
        label="Location"
        required
        value={locationOptions.length ? formData.location_id : ""}
        onChange={set("location_id")}
        disabled={Boolean(workingLocation)}
      >
        {locationOptions.map((location) => (
          <MenuItem key={location.location_id} value={location.location_id}>
            {location.name}
          </MenuItem>
        ))}
      </TextField>
      <TextField
        label="Table Number"
        type="number"
        required
        autoFocus
        value={formData.table_number}
        onChange={set("table_number")}
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

export default NewTableDialog;
