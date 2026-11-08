import React from "react";
import { Stack, IconButton, Tooltip } from "@mui/material";

// waht to call a row ie "Edit Salmon Roll". Fall back to grid's row id if no name is available
export const defaultRowName = (row, params) =>
  row?.name ??
  row?.tag_name ??
  row?.category_name ??
  (params?.id != null ? "#${params.id}" : "");

// Icon buttons for a management grid's "actions" column.
// actions: [{ label, icon: LucideIcon, onClick, color?, hidden? }]
// aria-label for each action button is either provided via ariaLabel or constructed as "Action RowName" for accessibility
export const RowActions = ({ actions = [], rowName = "" }) => {
  return (
    <Stack direction="row" spacing={1} alignItems="center">
      {actions
        .filter((action) => !action.hidden)
        .map(({ label, ariaLabel, icon: Icon, onClick, color = "default" }) => (
          <Tooltip title={label} key={label}>
            <IconButton
              onClick={onClick}
              size="small"
              color={color}
              aria-label={
                ariaLabel ?? (rowName ? `${label} ${rowName}` : label)
              }
            >
              <Icon size={20} aria-hidden="true" />
            </IconButton>
          </Tooltip>
        ))}
    </Stack>
  );
};

// Column definition for an actions column; getActions(row) returns the actions array
// getRownName names the rown for each label
export const actionsColumn = (
  getActions,
  { getRowName = defaultRowName, ...options } = {},
) => ({
  field: "actions",
  headerName: "Actions",
  width: 150,
  sortable: false,
  filterable: false,
  disableColumnMenu: true,
  disableExport: true,
  renderCell: (params) => (
    <RowActions
      actions={getActions(params.row)}
      rowName={getRowName(params.row, params) ?? ""}
    />
  ),
  ...options,
});

export default RowActions;
