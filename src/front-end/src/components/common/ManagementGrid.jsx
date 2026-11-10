import React, { useMemo } from "react";
import { DataGrid } from "@mui/x-data-grid";
import { unwrap } from "@/utils/missingKeyWarnings";

// Shared DataGrid for admin management lists. Every prop can be overridden.
export const ManagementGrid = ({
  columns = [],
  rows,
  initialState,
  slotProps,
  sx = {},
  ...props
}) => {
  // display: "flex" makes DataGrid vertically center cell content (and reset its line-height)
  const flexColumns = useMemo(
    () => columns.map((column) => ({ display: "flex", ...column })),
    [columns],
  );

  // The grid reads row[field] for every column, computed ones too ("actions", "menu"), which
  // the development missing-key warnings would report as keys missing from the API response
  const plainRows = useMemo(() => rows?.map(unwrap), [rows]);

  return (
    <DataGrid
      columns={flexColumns}
      rows={plainRows}
      autoHeight
      showToolbar
      disableRowSelectionOnClick
      pageSizeOptions={[10, 25, 50]}
      initialState={{
        pagination: { paginationModel: { pageSize: 10 } },
        ...initialState,
      }}
      slotProps={{
        ...slotProps,
        toolbar: { showQuickFilter: true, ...slotProps?.toolbar },
      }}
      {...props}
      sx={{
        bgcolor: "background.paper",
        "& .MuiDataGrid-cell, & .MuiDataGrid-columnHeader": {
          "&:focus, &:focus-within": {
            outline: "none",
          },
          "&:focus-visible, &:has(:focus-visible)": {
            outline: (theme) => `2px solid ${theme.palette.primary.main}`,
            outlineOffset: "-2px",
          },
        },
        ...sx,
      }}
    />
  );
};

export default ManagementGrid;
