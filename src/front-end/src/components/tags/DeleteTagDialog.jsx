import React from "react";
import { Box, Typography } from "@mui/material";
import ConfirmDialog from "@/components/common/ConfirmDialog";
import TagChip from "./TagChip";

// onConfirm returns false when the delete fails, so the dialog stays open with the error
const DeleteTagDialog = ({ open, tag, error, onClose, onConfirm }) => (
  <ConfirmDialog
    open={open}
    title="Delete tag"
    message="Are you sure you want to delete this tag?"
    confirmLabel="Delete"
    error={error}
    onConfirm={onConfirm}
    onClose={onClose}
  >
    {tag && (
      <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
        <Typography>Tag:</Typography>
        <TagChip tag={tag} size="l" />
      </Box>
    )}
    <Typography color="error">
      This action will remove the tag from all menu items and cannot be undone.
    </Typography>
  </ConfirmDialog>
);

export default DeleteTagDialog;
