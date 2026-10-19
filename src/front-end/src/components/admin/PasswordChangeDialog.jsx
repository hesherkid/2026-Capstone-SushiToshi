import React, { useState } from "react";
import {
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Button,
  Alert,
  Box,
  CircularProgress,
} from "@mui/material";
import AppDialog from "@/components/common/AppDialog";
import { Key } from "lucide-react";

const PasswordChangeDialog = ({
  open,
  onClose,
  onSubmit,
  staffMember = null,
  isLoading = false,
  error = null,
  token = null,
}) => {
  const [passwords, setPasswords] = useState({
    new: "",
    confirm: "",
  });
  const [validationError, setValidationError] = useState(null);

  const getErrorMessage = (error) => {
    if (!error) return null;

    if (typeof error === "string") return error;

    if (error.message) return error.message;

    if (error.detail) {
      if (Array.isArray(error.detail)) {
        return error.detail.map((err) => err.msg).join(", ");
      }
      if (typeof error.detail === "string") {
        return error.detail;
      }
    }

    return "An error occurred while resetting the password";
  };

  const handleChange = (e) => {
    const { name, value } = e.target;
    setPasswords((prev) => ({
      ...prev,
      [name]: value,
    }));
    setValidationError(null);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (passwords.new !== passwords.confirm) {
      setValidationError("New passwords don't match");
      return;
    }

    if (passwords.new.length < 8) {
      setValidationError("New password must be at least 8 characters");
      return;
    }

    try {
      await onSubmit({
        new_password: passwords.new,
      });
    } catch (err) {
      console.error("Password reset error:", err);
      setValidationError(
        getErrorMessage(err?.response?.data) || "Failed to reset password"
      );
    }
  };

  const resetForm = () => {
    setPasswords({
      new: "",
      confirm: "",
    });
    setValidationError(null);
  };

  const handleClose = () => {
    resetForm();
    onClose();
  };

  const displayError = getErrorMessage(error) || validationError;

  return (
    <AppDialog open={open} onClose={handleClose}>
      <form onSubmit={handleSubmit}>
        <DialogTitle sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <Key />
          Reset Password{" "}
          {staffMember &&
            `for ${staffMember.first_name} ${staffMember.last_name}`}
        </DialogTitle>

        <DialogContent>
          <Box sx={{ display: "flex", flexDirection: "column", gap: 2, mt: 2 }}>
            {displayError && (
              <Alert
                severity="error"
                onClose={() => setValidationError(null)}
                sx={{ wordBreak: "break-word" }}
              >
                {displayError}
              </Alert>
            )}
            <TextField
              label="New Password"
              name="new"
              type="password"
              value={passwords.new}
              onChange={handleChange}
              required
              fullWidth
              autoFocus
              helperText="Minimum 8 characters"
              error={Boolean(displayError)}
            />
            <TextField
              label="Confirm New Password"
              name="confirm"
              type="password"
              value={passwords.confirm}
              onChange={handleChange}
              required
              fullWidth
              error={Boolean(displayError)}
            />
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={handleClose}>Cancel</Button>
          <Button
            type="submit"
            variant="contained"
            disabled={isLoading}
            startIcon={isLoading ? <CircularProgress size={20} /> : null}
          >
            Reset Password
          </Button>
        </DialogActions>
      </form>
    </AppDialog>
  );
};

export default PasswordChangeDialog;
