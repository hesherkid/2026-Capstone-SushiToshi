import React, { useMemo } from "react";
import { DataGrid } from "@mui/x-data-grid";

// Shared DataGrid for admin management lists. Every prop can be overridden.
export const ManagementGrid = ({
  columns = [],
  initialState,
  slotProps,
  sx = {},
  ...props
}) => {
  // display: "flex" makes DataGrid vertically center cell content (and reset its line-height)
  const flexColumns = useMemo(
    () => columns.map((column) => ({ display: "flex", ...column })),
    [columns]
  );

  return (
    <DataGrid
      columns={flexColumns}
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
        "& .MuiDataGrid-cell:focus, & .MuiDataGrid-cell:focus-within, & .MuiDataGrid-columnHeader:focus, & .MuiDataGrid-columnHeader:focus-within":
          {
            outline: "none",
          },
        ...sx,
      }}
    />
  );
};

export default ManagementGrid;
