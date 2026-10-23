import React from "react";
import {
  Box,
  Button,
  DialogTitle,
  DialogContent,
  DialogActions,
  Typography,
  Alert,
} from "@mui/material";
import AppDialog from "@/components/common/AppDialog";
import TagChip from "./TagChip";

const DeleteTagDialog = ({ open, tag, error, onClose, onConfirm }) => (
  <AppDialog open={open} onClose={onClose} maxWidth="xs">
    <DialogTitle>Confirm Delete</DialogTitle>
    <DialogContent>
      <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
        <Typography>Are you sure you want to delete this tag?</Typography>
        {tag && (
          <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
            <Typography>Tag:</Typography>
            <TagChip tag={tag} size="l" />
          </Box>
        )}
        <Typography color="error">
          This action will remove the tag from all menu items and cannot be
          undone.
        </Typography>
        {error && <Alert severity="error">{error}</Alert>}
      </Box>
    </DialogContent>
    <DialogActions>
      <Button onClick={onClose}>Cancel</Button>
      <Button onClick={onConfirm} variant="contained" color="error">
        Delete
      </Button>
    </DialogActions>
  </AppDialog>
);

export default DeleteTagDialog;
