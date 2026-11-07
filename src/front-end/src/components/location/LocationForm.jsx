import { useState, useEffect } from "react";
import { TextField, MenuItem } from "@mui/material";
import FormDialog from "@/components/common/FormDialog";
import { getHolidayOptions } from "@/services/holidayService";

// Add / Edit Location. The page mounts it while open, so the fields start fresh each time
const LocationForm = ({
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
    address_one: initialData?.address_one || "",
    address_two: initialData?.address_two || "",
    city: initialData?.city || "",
    province: initialData?.province || "",
    province_code: initialData?.province_code || "AB",
    postal_code: initialData?.postal_code || "",
    phone_number: initialData?.phone_number || "",
  });

  // Province codes come from Provinces.cs via GET /holidays/options.
  // Starts with the current code so the dropdown is never empty while loading,
  // and editing still works if the request fails.
  const [provinceCodes, setProvinceCodes] = useState([formData.province_code]);

  useEffect(() => {
    getHolidayOptions()
      .then((options) => {
        if (options.provinces?.length) setProvinceCodes(options.provinces);
      })
      .catch((err) => console.error("Error fetching provinces:", err));
  }, []);


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
      title={mode === "create" ? "Add New Location" : "Edit Location"}
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
        label="Name"
        value={formData.name}
        onChange={handleChange}
        required
        fullWidth
      />
      <TextField
        name="address_one"
        label="Address Line 1"
        value={formData.address_one}
        onChange={handleChange}
        required
        fullWidth
      />
      <TextField
        name="address_two"
        label="Address Line 2"
        value={formData.address_two}
        onChange={handleChange}
        fullWidth
      />
      <TextField
        name="city"
        label="City"
        value={formData.city}
        onChange={handleChange}
        required
        fullWidth
      />
      <TextField
        name="province"
        label="Province"
        value={formData.province}
        onChange={handleChange}
        required
        fullWidth
      />
      <TextField
        select
        name="province_code"
        label="Province code"
        value={formData.province_code}
        onChange={handleChange}
        helperText="Holiday pricing uses this province's holidays"
        required
        fullWidth
      >
        {provinceCodes.map((code) => (
          <MenuItem key={code} value={code}>
            {code}
          </MenuItem>
        ))}
      </TextField>
      <TextField
        name="postal_code"
        label="Postal Code"
        value={formData.postal_code}
        onChange={handleChange}
        required
        fullWidth
      />
      <TextField
        name="phone_number"
        label="Phone Number"
        value={formData.phone_number}
        onChange={handleChange}
        required
        fullWidth
      />
    </FormDialog>
  );
};

export default LocationForm;
