import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Box,
  Alert,
  CircularProgress,
  IconButton,
} from "@mui/material";
import { X, Receipt } from "lucide-react";
import BillBreakdown from "./BillBreakdown";

/**
 * Shared shell for the staff and customer bill dialogs: title, close button, loading,
 * errors and the breakdown. Each dialog adds its own notices and action buttons.
 */
const BillDialogFrame = ({ open, onClose, summary, loading, error, onErrorClose, notices, actions }) => (
  <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
    <DialogTitle>
      <Box display="flex" alignItems="center" gap={1}>
        <Receipt size={24} />
        Bill Summary
      </Box>
      <IconButton aria-label="Close" onClick={onClose} sx={{ position: "absolute", right: 8, top: 8 }}>
        <X />
      </IconButton>
    </DialogTitle>

    <DialogContent>
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

    {actions && !loading && <DialogActions sx={{ px: 3, py: 2 }}>{actions}</DialogActions>}
  </Dialog>
);

export default BillDialogFrame;
