import { Box, DialogTitle, IconButton, Typography } from "@mui/material";
import { X } from "lucide-react";

/**
 * The title bar every dialog shares: a title, an optional line under it, and a close button.
 * title can be a node (e.g. text plus a status chip). closeDisabled stops closing mid-save.
 */
const DialogHeader = ({ title, subtitle, onClose, closeDisabled = false }) => (
  <DialogTitle
    component="div"
    sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: 2 }}
  >
    <Box sx={{ minWidth: 0 }}>
      <Typography variant="h6" component="h2">
        {title}
      </Typography>
      {subtitle && (
        <Typography variant="body2" color="text.secondary" component="div">
          {subtitle}
        </Typography>
      )}
    </Box>
    {onClose && (
      <IconButton aria-label="Close" onClick={onClose} disabled={closeDisabled} size="small">
        <X size={20} />
      </IconButton>
    )}
  </DialogTitle>
);

export default DialogHeader;
