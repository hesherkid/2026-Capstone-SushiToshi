import React from "react";
import { Stack, IconButton, Tooltip } from "@mui/material";

// Icon buttons for a management grid's "actions" column.
// actions: [{ label, icon: LucideIcon, onClick, color?, hidden? }]
export const RowActions = ({ actions = [] }) => {
  return (
    <Stack direction="row" spacing={1} alignItems="center">
      {actions
        .filter((action) => !action.hidden)
        .map(({ label, icon: Icon, onClick, color = "default" }) => (
          <Tooltip title={label} key={label}>
            <IconButton
              onClick={onClick}
              size="small"
              color={color}
              aria-label={label}
            >
              <Icon size={20} />
            </IconButton>
          </Tooltip>
        ))}
    </Stack>
  );
};

// Column definition for an actions column; getActions(row) returns the actions array
export const actionsColumn = (getActions, options = {}) => ({
  field: "actions",
  headerName: "Actions",
  width: 150,
  sortable: false,
  filterable: false,
  disableColumnMenu: true,
  disableExport: true,
  renderCell: (params) => <RowActions actions={getActions(params.row)} />,
  ...options,
});

export default RowActions;
