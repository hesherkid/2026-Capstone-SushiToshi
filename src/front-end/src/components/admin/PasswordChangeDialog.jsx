import React, { useState } from "react";
import { TextField } from "@mui/material";
import FormDialog from "@/components/common/FormDialog";

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

  const handleSubmit = async () => {
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
    <FormDialog
      open={open}
      onClose={handleClose}
      title="Reset Password"
      subtitle={staffMember && `${staffMember.first_name} ${staffMember.last_name}`}
      error={displayError}
      onErrorClose={() => setValidationError(null)}
      onSubmit={handleSubmit}
      submitLabel="Reset Password"
      busyLabel="Resetting..."
      busy={isLoading}
    >
      <TextField
        label="New Password"
        name="new"
        type="password"
        value={passwords.new}
        onChange={handleChange}
        required
        fullWidth
        autoFocus
        autoComplete="new-password"
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
        autoComplete="new-password"
        error={Boolean(displayError)}
      />
    </FormDialog>
  );
};

export default PasswordChangeDialog;
