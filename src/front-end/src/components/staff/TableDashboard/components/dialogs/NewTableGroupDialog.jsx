import { useState, useEffect } from "react";
import { TextField, MenuItem } from "@mui/material";
import FormDialog from "@/components/common/FormDialog";
import { useTable } from "../../context/TableContext";
import api from "@/config/api";
import storage from "@/utils/storage";
import serverMessage from "@/utils/serverMessage";

const emptyForm = () => ({
  group_Name: "",
  location_Id: storage.get("branch-location") || "",
});

const NewTableGroupDialog = ({ open }) => {
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
      await api.post("/TableGroup", {
        Group_Name: formData.group_Name.trim(),
        Is_Active: true,
        Location_Id: formData.location_Id,
      });
      await refreshData();
      handleClose();
    } catch (err) {
      setError(serverMessage(err, "Couldn't create the table group."));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <FormDialog
      open={open}
      onClose={handleClose}
      title="Create New Table Group"
      error={error}
      onErrorClose={() => setError(null)}
      onSubmit={handleSubmit}
      submitLabel="Create Group"
      busyLabel="Creating..."
      busy={isSubmitting}
      submitDisabled={!formData.group_Name.trim()}
    >
      <TextField select label="Location" required value={formData.location_Id} onChange={set("location_Id")}>
        {availableLocations.map((location) => (
          <MenuItem key={location.location_Id} value={location.location_Id}>
            {location.name}
          </MenuItem>
        ))}
      </TextField>
      <TextField
        label="Group Name"
        required
        autoFocus
        value={formData.group_Name}
        onChange={set("group_Name")}
        placeholder="e.g., Large Party Area"
      />
    </FormDialog>
  );
};

export default NewTableGroupDialog;
