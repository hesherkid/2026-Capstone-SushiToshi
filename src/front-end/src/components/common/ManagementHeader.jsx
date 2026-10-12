import React from "react";
import { Stack, Typography, Button } from "@mui/material";
import { Plus } from "lucide-react";

// Title + "Add New ..." button row shown above admin management lists
export const ManagementHeader = ({
  title,
  addLabel,
  onAdd,
  actions = null,
  sx = {},
}) => {
  return (
    <Stack
      direction="row"
      justifyContent="left"
      alignItems="center"
      mb={3}
      spacing={2}
      sx={sx}
    >
      <Typography variant="h4" component="h1">
        {title}
      </Typography>
      {onAdd && (
        <Button onClick={onAdd}>
          <Plus />
          {addLabel}
        </Button>
      )}
      {actions}
    </Stack>
  );
};

export default ManagementHeader;
