import {
  DialogContent,
  DialogActions,
  Box,
  Alert,
  CircularProgress,
} from "@mui/material";
import AppDialog from "@/components/common/AppDialog";
import DialogHeader from "@/components/common/DialogHeader";
import BillBreakdown from "./BillBreakdown";

/**
 * Shared shell for the staff and customer bill dialogs: title, close button, loading,
 * errors and the breakdown. Each dialog adds its own notices and action buttons.
 */
const BillDialogFrame = ({ open, onClose, summary, loading, error, onErrorClose, notices, actions }) => (
  <AppDialog open={open} onClose={onClose} maxWidth="md">
    <DialogHeader title="Bill Summary" onClose={onClose} />

    <DialogContent dividers>
      {loading ? (
        <Box display="flex" justifyContent="center" alignItems="center" minHeight="200px">
          <CircularProgress />
        </Box>
      ) : (
        <>
          {error && (
            <Alert severity="error" sx={{ mb: 2 }} onClose={onErrorClose}>
              {error}
            </Alert>
          )}
          {notices}
          {summary && <BillBreakdown summary={summary} />}
        </>
      )}
    </DialogContent>

    {actions && !loading && <DialogActions>{actions}</DialogActions>}
  </AppDialog>
);

export default BillDialogFrame;
