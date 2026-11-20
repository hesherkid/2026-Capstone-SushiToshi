import React, { useState } from "react";
import { Alert, Box, Button, DialogActions, DialogContent, DialogTitle, Stack, Typography } from "@mui/material";
import { AlertTriangle } from "lucide-react";
import AppDialog from "@/components/common/AppDialog";
import MaterialSymbol from "@/components/common/MaterialSymbol";

// The customer side's big rounded buttons
const PILL_BUTTON_SX = { minHeight: 52, borderRadius: 999, fontSize: "1rem" };

// Confirmation for destructive actions. onConfirm may be async; the dialog closes when it settles,
// unless onConfirm returns false: then it stays open to show `error` and let the user retry.
// message can be a node (e.g. text with a chip); children go under it.
// confirmDisabled holds the confirm button back, e.g. until a question in children is answered.
// customer: the customer side's look, a floating card (even on phones) with stacked pill buttons,
// `icon` (a Material Symbol) above the title and the confirm button in the brand red
export const ConfirmDialog = ({
  open,
  title,
  message,
  confirmLabel = "Confirm",
  cancelLabel = "Cancel",
  onConfirm,
  onClose,
  error = null,
  customer = false,
  icon = "warning",
  confirmDisabled = false,
  children,
}) => {
  const [isProcessing, setIsProcessing] = useState(false);

  const handleConfirm = async () => {
    setIsProcessing(true);
    let keepOpen = false;
    try {
      keepOpen = (await onConfirm()) === false;
    } finally {
      setIsProcessing(false);
      if (!keepOpen) onClose();
    }
  };

  const errorAlert = error && (
    <Alert severity="error" sx={{ alignSelf: "stretch", textAlign: "left" }}>
      {error}
    </Alert>
  );

  if (customer) {
    return (
      <AppDialog
        open={open}
        onClose={isProcessing ? undefined : onClose}
        maxWidth="xs"
        fullScreen={false}
        slotProps={{ paper: { sx: { borderRadius: 4, mx: 2, width: "calc(100% - 32px)" } } }}
      >
        <DialogContent
          sx={{ display: "flex", flexDirection: "column", alignItems: "center", textAlign: "center", gap: 1, pt: 4 }}
        >
          <Box
            sx={{
              width: 64,
              height: 64,
              mb: 1,
              borderRadius: "50%",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              bgcolor: "action.hover",
            }}
          >
            <MaterialSymbol name={icon} size={34} sx={{ color: "primary.main" }} />
          </Box>
          <DialogTitle component="h2" variant="h6" sx={{ p: 0, fontWeight: 700 }}>
            {title}
          </DialogTitle>
          <Typography component="div" color="text.secondary">
            {message}
          </Typography>
          {children}
          {errorAlert}
        </DialogContent>

        {/* Main action on top, both full width */}
        <DialogActions sx={{ flexDirection: "column", alignItems: "stretch", pt: 1, pb: 3 }}>
          <Button variant="contained" size="large" onClick={handleConfirm} disabled={isProcessing || confirmDisabled} sx={PILL_BUTTON_SX}>
            {isProcessing ? "Working..." : confirmLabel}
          </Button>
          <Button variant="outlined" size="large" onClick={onClose} disabled={isProcessing} sx={PILL_BUTTON_SX}>
            {cancelLabel}
          </Button>
        </DialogActions>
      </AppDialog>
    );
  }

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
        <Typography component="div">{message}</Typography>
        {children}
        {errorAlert}
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
          {cancelLabel}
        </Button>
        <Button
          variant="contained"
          color="error"
          onClick={handleConfirm}
          disabled={isProcessing || confirmDisabled}
        >
          {isProcessing ? "Working..." : confirmLabel}
        </Button>
      </DialogActions>
    </AppDialog>
  );
};

export default ConfirmDialog;
