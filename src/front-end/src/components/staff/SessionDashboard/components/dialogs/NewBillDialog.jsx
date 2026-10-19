import { useState, useCallback, useMemo } from "react";
import {
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Box,
  TextField,
  Alert,
  CircularProgress,
  Typography,
  IconButton,
} from "@mui/material";
import AppDialog from "@/components/common/AppDialog";
import { X } from "lucide-react";
import { useSession } from "../../context/SessionContext";
import { validateBillData } from "@/components/staff/SessionDashboard/utils/sessionHelpers";
import { tableLabel } from "../SessionColumns";

// "Table 3" / "Tables 1, 2" for the dialog title, or null if the table isn't known
const useBillTableLabel = (sessionId, sessions = []) =>
  useMemo(() => {
    // Staff: the session dashboard has every active session's tables
    const session = sessions.find((s) => s.session_Id === Number(sessionId));
    if (session?.table_Numbers?.length) return tableLabel(session.table_Numbers);

    // Guests: the table number saved from their QR code
    const qrTable = typeof window !== "undefined" ? localStorage.getItem("tableNumber") : null;
    return qrTable ? `Table ${qrTable}` : null;
  }, [sessionId, sessions]);

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

  const handleSubmit = async (e) => {
    e.preventDefault();

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
    <AppDialog open={open} onClose={handleClose}>
      <DialogTitle>
        {billTable ? `Create New Bill for ${billTable}` : "Create New Bill"}
        <IconButton
          onClick={handleClose}
          sx={{ position: "absolute", right: 8, top: 8 }}
        >
          <X />
        </IconButton>
      </DialogTitle>
      <form onSubmit={handleSubmit}>
        <DialogContent>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Create a bill for your table.
          </Typography>
          {error?.submit && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error.submit}
            </Alert>
          )}

          {error?.guests && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error.guests}
            </Alert>
          )}

          <Box sx={{ mb: 2 }}>
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
          </Box>

          <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 2, mb: 2 }}>
            <TextField
              label="Adults"
              type="number"
              name="adultCount"
              value={formData.adultCount}
              onChange={handleChange}
              slotProps={{
                input: {
                  inputProps: {
                    min: 0,
                    max: 50,
                    inputMode: "numeric", // Numeric keyboard shows on mobile
                    pattern: "[0-9]*",
                  },
                },
              }}
              disabled={submitting}
            />
            <TextField
              label="Seniors"
              type="number"
              name="seniorCount"
              value={formData.seniorCount}
              onChange={handleChange}
              slotProps={{
                input: {
                  inputProps: {
                    min: 0,
                    max: 50,
                    inputMode: "numeric", // Numeric keyboard shows on mobile
                    pattern: "[0-9]*",
                  },
                },
              }}
              disabled={submitting}
            />
            <TextField
              label="Children"
              type="number"
              name="childCount"
              value={formData.childCount}
              onChange={handleChange}
              slotProps={{
                input: {
                  inputProps: {
                    min: 0,
                    max: 50,
                    inputMode: "numeric", // Numeric keyboard shows on mobile
                    pattern: "[0-9]*",
                  },
                },
              }}
              disabled={submitting}
            />
            <TextField
              label="Toddlers (6 & Under)"
              type="number"
              name="totCount"
              value={formData.totCount}
              onChange={handleChange}
              slotProps={{
                input: {
                  inputProps: {
                    min: 0,
                    max: 50,
                    inputMode: "numeric",
                    pattern: "[0-9]*",
                  },
                },
              }}
              disabled={submitting}
            />
          </Box>

          {/* Total Guests Summary */}
          <Box
            sx={{
              p: 2,
              bgcolor: "background.default",
              borderRadius: 1,
              textAlign: "center",
            }}
          >
            <Typography variant="h6" fontWeight="600">
              Total Guests: {totalGuests}
            </Typography>
          </Box>
        </DialogContent>

        <DialogActions>
          <Button onClick={handleClose} disabled={submitting}>
            Cancel
          </Button>
          <Button
            type="submit"
            variant="contained"
            disabled={submitting}
            startIcon={submitting && <CircularProgress size={16} />}
          >
            {submitting ? "Creating..." : "Create Bill"}
          </Button>
        </DialogActions>
      </form>
    </AppDialog>
  );
};

export default NewBillDialog;
