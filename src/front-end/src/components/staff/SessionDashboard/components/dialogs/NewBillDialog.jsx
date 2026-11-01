import { useState, useCallback, useMemo } from "react";
import { Box, TextField, Alert, Typography } from "@mui/material";
import FormDialog from "@/components/common/FormDialog";
import { useSession } from "../../context/SessionContext";
import { validateBillData } from "@/components/staff/SessionDashboard/utils/sessionHelpers";
import { tableLabel } from "../SessionColumns";
import storage from "@/utils/storage";

// "Table 3" / "Tables 1, 2" for the dialog title, or null if the table isn't known
const useBillTableLabel = (sessionId, sessions = []) =>
  useMemo(() => {
    // Staff: the session dashboard has every active session's tables
    const session = sessions.find((s) => s.session_Id === Number(sessionId));
    if (session?.table_Numbers?.length) return tableLabel(session.table_Numbers);

    // Guests: the table number saved from their QR code
    const qrTable = storage.get("tableNumber");
    return qrTable ? `Table ${qrTable}` : null;
  }, [sessionId, sessions]);

const GUEST_FIELDS = [
  { name: "adultCount", label: "Adults" },
  { name: "seniorCount", label: "Seniors" },
  { name: "childCount", label: "Children" },
  { name: "totCount", label: "Toddlers (6 & Under)" },
];

const NewBillDialog = ({ open, sessionId, onClose }) => {
  const { createBill, sessions } = useSession();
  const billTable = useBillTableLabel(sessionId, sessions);

  const [formData, setFormData] = useState({
    billName: "",
    adultCount: 0,
    childCount: 0,
    seniorCount: 0,
    totCount: 0,
  });
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState({});

  const totalGuests = useMemo(() => {
    return (
      Number(formData.adultCount || 0) +
      Number(formData.childCount || 0) +
      Number(formData.seniorCount || 0) +
      Number(formData.totCount || 0)
    );
  }, [
    formData.adultCount,
    formData.childCount,
    formData.seniorCount,
    formData.totCount,
  ]);

  const handleChange = useCallback((e) => {
    const { name, value } = e.target;

    // Remove leading zeros from number fields
    let sanitizedValue = value;
    if (['adultCount', 'childCount', 'seniorCount', 'totCount'].includes(name)) {
      sanitizedValue = value.replace(/^0+(?=\d)/, '');
    }

    setFormData((prev) => ({
      ...prev,
      [name]: sanitizedValue,
    }));

    setError((prev) => {
      if (!prev || !prev[name]) return prev || {};
      const { [name]: _, ...rest } = prev;
      return rest;
    });
  }, []);

  const handleSubmit = async () => {
    const validation = validateBillData(formData);

    if (!validation.isValid) {
      setError(validation.errors);
      return;
    }

    try {
      setSubmitting(true);
      setError({});

      const result = await createBill(sessionId, formData);
      if (result) {
        setFormData({
          billName: "",
          adultCount: "",
          childCount: "",
          seniorCount: "",
          totCount: "",
        });
        onClose();
      }
    } catch (error) {
      console.error("Error creating bill:", error);
      setError({
        submit: error.message || "Failed to create bill"
      });
    } finally {
      setSubmitting(false);
    }
  };

  const handleClose = () => {
    setFormData({
      billName: "",
      adultCount: "",
      childCount: "",
      seniorCount: "",
      totCount: "",
    });
    setError({});
    onClose();
  };

  return (
    <FormDialog
      open={open}
      onClose={handleClose}
      title={billTable ? `Create New Bill for ${billTable}` : "Create New Bill"}
      subtitle="Create a bill for your table."
      error={error?.submit}
      onErrorClose={() => setError(({ submit, ...rest }) => rest)}
      onSubmit={handleSubmit}
      submitLabel="Create Bill"
      busyLabel="Creating..."
      busy={submitting}
    >
      {error?.guests && <Alert severity="error">{error.guests}</Alert>}

      <TextField
        autoFocus
        label="Bill Name"
        name="billName"
        value={formData.billName}
        onChange={handleChange}
        fullWidth
        placeholder="e.g., Table 1 - Party of 4"
        error={!!error.billName}
        disabled={submitting}
        helperText={error.billName}
        required
      />

      <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 2 }}>
        {GUEST_FIELDS.map(({ name, label }) => (
          <TextField
            key={name}
            label={label}
            type="number"
            name={name}
            value={formData[name]}
            onChange={handleChange}
            slotProps={{
              htmlInput: {
                min: 0,
                max: 50,
                inputMode: "numeric", // Numeric keyboard shows on mobile
                pattern: "[0-9]*",
              },
            }}
            disabled={submitting}
          />
        ))}
      </Box>

      {/* Total Guests Summary */}
      <Box sx={{ p: 2, bgcolor: "background.default", borderRadius: 1, textAlign: "center" }}>
        <Typography variant="h6" fontWeight="600">
          Total Guests: {totalGuests}
        </Typography>
      </Box>
    </FormDialog>
  );
};

export default NewBillDialog;
