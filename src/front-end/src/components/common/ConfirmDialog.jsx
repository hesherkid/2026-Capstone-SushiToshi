import React, { useState } from "react";
import { Button, DialogActions, DialogContent, DialogTitle, Stack, Typography } from "@mui/material";
import { AlertTriangle } from "lucide-react";
import AppDialog from "@/components/common/AppDialog";

// Confirmation for destructive actions. onConfirm may be async; the dialog closes when it settles.
export const ConfirmDialog = ({
  open,
  title,
  message,
  confirmLabel = "Confirm",
  onConfirm,
  onClose,
}) => {
  const [isProcessing, setIsProcessing] = useState(false);

  const handleConfirm = async () => {
    setIsProcessing(true);
    try {
      await onConfirm();
    } finally {
      setIsProcessing(false);
      onClose();
    }
  };

  return (
    <AppDialog open={open} onClose={isProcessing ? undefined : onClose} maxWidth="xs">
      {/* On phones (full screen) the icon, title and message sit centred in the middle */}
      <DialogContent
        sx={{
          display: "flex",
          flexDirection: "column",
          justifyContent: { xs: "center", sm: "flex-start" },
          alignItems: { xs: "center", sm: "flex-start" },
          textAlign: { xs: "center", sm: "left" },
          gap: 1.5,
          pt: 3,
        }}
      >
        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={1}
          alignItems="center"
        >
          <AlertTriangle color="#d32f2f" size={28} />
          <DialogTitle component="h2" variant="h6" sx={{ p: 0 }}>
            {title}
          </DialogTitle>
        </Stack>
        <Typography>{message}</Typography>
      </DialogContent>

      {/* Phones: full-width buttons stacked at the bottom, main action on top */}
      <DialogActions
        sx={{
          flexDirection: { xs: "column-reverse", sm: "row" },
          alignItems: { xs: "stretch", sm: "center" },
          "& > .MuiButton-root": { width: { xs: "100%", sm: "auto" }, minHeight: { xs: 48, sm: 0 } },
        }}
      >
        <Button onClick={onClose} disabled={isProcessing}>
          Cancel
        </Button>
        <Button
          variant="contained"
          color="error"
          onClick={handleConfirm}
          disabled={isProcessing}
        >
          {isProcessing ? "Working..." : confirmLabel}
        </Button>
      </DialogActions>
    </AppDialog>
  );
};

export default ConfirmDialog;
