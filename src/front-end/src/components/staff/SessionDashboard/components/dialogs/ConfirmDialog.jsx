import { useState } from "react";
import { Dialog, Box, Stack, Typography, Button } from "@mui/material";
import { AlertTriangle } from "lucide-react";

const ConfirmDialog = ({ open, title, message, confirmLabel, onConfirm, onClose }) => {
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
    <Dialog open={open} onClose={isProcessing ? undefined : onClose}>
      <Box sx={{ p: 3 }}>
        <Stack direction="row" spacing={1} alignItems="center" mb={2}>
          <AlertTriangle color="#d32f2f" />
          <Typography variant="h6">{title}</Typography>
        </Stack>
        <Typography mb={3}>{message}</Typography>
        <Stack direction="row" spacing={1} justifyContent="flex-end">
          <Button onClick={onClose} disabled={isProcessing}>
            Cancel
          </Button>
          <Button
            variant="contained"
            color="error"
            onClick={handleConfirm}
            disabled={isProcessing}
          >
            {isProcessing ? "Working..." : confirmLabel || "Confirm"}
          </Button>
        </Stack>
      </Box>
    </Dialog>
  );
};

export default ConfirmDialog;
