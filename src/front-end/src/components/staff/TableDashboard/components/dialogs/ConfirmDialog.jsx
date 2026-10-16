import { useState } from "react";
import { Dialog, Box, Stack, Typography, Button } from "@mui/material";
import { AlertTriangle } from "lucide-react";
import { useTable } from "../../context/TableContext";

// data: { title, message, confirmLabel, onConfirm }
const ConfirmDialog = ({ open, data }) => {
  const { closeDialog } = useTable();
  const [isProcessing, setIsProcessing] = useState(false);

  const handleConfirm = async () => {
    setIsProcessing(true);
    try {
      await data.onConfirm();
    } finally {
      setIsProcessing(false);
      closeDialog();
    }
  };

  return (
    <Dialog open={open} onClose={isProcessing ? undefined : closeDialog}>
      <Box sx={{ p: 3 }}>
        <Stack direction="row" spacing={1} alignItems="center" mb={2}>
          <AlertTriangle color="#d32f2f" />
          <Typography variant="h6">{data?.title}</Typography>
        </Stack>
        <Typography mb={3}>{data?.message}</Typography>
        <Stack direction="row" spacing={1} justifyContent="flex-end">
          <Button onClick={closeDialog} disabled={isProcessing}>
            Cancel
          </Button>
          <Button
            variant="contained"
            color="error"
            onClick={handleConfirm}
            disabled={isProcessing}
          >
            {isProcessing ? "Working..." : data?.confirmLabel || "Confirm"}
          </Button>
        </Stack>
      </Box>
    </Dialog>
  );
};

export default ConfirmDialog;
