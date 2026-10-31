import { useState } from "react";
import { TextField, FormControlLabel, Switch } from "@mui/material";
import FormDialog from "@/components/common/FormDialog";

// Add / Edit Menu. The page mounts it while open, so the fields start fresh each time
const MenuForm = ({
  open = true,
  initialData,
  onSubmit,
  onClose,
  mode,
  busy = false,
  error = null,
  onErrorClose,
}) => {
  const [formData, setFormData] = useState({
    name: initialData?.name || "",
    description: initialData?.description || "",
    start_time: initialData?.start_time || "",
    end_time: initialData?.end_time || "",
    is_active: initialData?.is_active ?? true,
  });

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData((prev) => ({
      ...prev,
      [name]: value,
    }));
  };

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={mode === "create" ? "Add New Menu" : "Edit Menu"}
      subtitle={mode === "edit" && initialData?.name}
      error={error}
      onErrorClose={onErrorClose}
      onSubmit={() => onSubmit(formData)}
      submitLabel={mode === "create" ? "Create" : "Save"}
      busyLabel={mode === "create" ? "Creating..." : "Saving..."}
      busy={busy}
    >
      <TextField
        name="name"
        label="Menu Name"
        value={formData.name}
        onChange={handleChange}
        required
        fullWidth
      />
      <TextField
        name="description"
        label="Description"
        value={formData.description}
        onChange={handleChange}
        fullWidth
        multiline
        rows={3}
      />
      <TextField
        name="start_time"
        label="Start Time"
        type="time"
        value={formData.start_time}
        onChange={handleChange}
        required
        slotProps={{ inputLabel: { shrink: true } }}
        fullWidth
      />
      <TextField
        name="end_time"
        label="End Time"
        type="time"
        value={formData.end_time}
        onChange={handleChange}
        required
        slotProps={{ inputLabel: { shrink: true } }}
        fullWidth
      />
      <FormControlLabel
        control={
          <Switch
            checked={formData.is_active === true}
            onChange={(e) =>
              setFormData((prev) => ({
                ...prev,
                is_active: e.target.checked,
              }))
            }
          />
        }
        label="Is Active Menu"
      />
    </FormDialog>
  );
};

export default MenuForm;
