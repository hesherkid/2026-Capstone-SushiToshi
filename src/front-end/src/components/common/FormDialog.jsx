import { Alert, Box, Button, CircularProgress, DialogActions, DialogContent, Stack } from "@mui/material";
import AppDialog from "@/components/common/AppDialog";
import DialogHeader from "@/components/common/DialogHeader";

/**
 * A dialog that fills in a form and saves it, so every form dialog looks and behaves the same:
 * - DialogHeader (title, subtitle, close button), then the error, then the fields
 * - Cancel and the save button at the bottom; Enter in a field saves too
 * - While busy: the save button shows a spinner and busyLabel, and the dialog can't be closed
 *
 * onSubmit runs only when the browser's required/min checks pass and submitDisabled is false.
 * extraActions go before Cancel (e.g. a "Remove" button pushed left with sx={{ mr: "auto" }}).
 */
const FormDialog = ({
  open,
  onClose,
  title,
  subtitle,
  error,
  onErrorClose,
  onSubmit,
  submitLabel = "Save",
  busyLabel,
  busy = false,
  submitDisabled = false,
  submitColor = "primary",
  extraActions,
  maxWidth = "sm",
  children,
}) => {
  const handleSubmit = (e) => {
    e.preventDefault();
    if (!busy && !submitDisabled) onSubmit();
  };

  return (
    <AppDialog open={open} onClose={busy ? undefined : onClose} maxWidth={maxWidth}>
      {/* A flex column, so on a phone (full screen) the fields scroll and the buttons stay at the bottom */}
      <Box
        component="form"
        onSubmit={handleSubmit}
        sx={{ display: "flex", flexDirection: "column", flex: "1 1 auto", minHeight: 0 }}
      >
        <DialogHeader title={title} subtitle={subtitle} onClose={onClose} closeDisabled={busy} />
        <DialogContent>
          {error && (
            <Alert severity="error" onClose={onErrorClose} sx={{ mb: 2, wordBreak: "break-word" }}>
              {error}
            </Alert>
          )}
          {/* pt keeps the first field's floating label clear of the title */}
          <Stack spacing={2} sx={{ pt: 1 }}>
            {children}
          </Stack>
        </DialogContent>
        <DialogActions>
          {extraActions}
          <Button onClick={onClose} disabled={busy}>
            Cancel
          </Button>
          <Button
            type="submit"
            variant="contained"
            color={submitColor}
            disabled={busy || submitDisabled}
            startIcon={busy ? <CircularProgress size={16} color="inherit" /> : null}
          >
            {busy ? (busyLabel ?? submitLabel) : submitLabel}
          </Button>
        </DialogActions>
      </Box>
    </AppDialog>
  );
};

export default FormDialog;
