"use client";

import { useId, useState } from "react";
import {
  Box,
  Button,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
} from "@mui/material";
import { visuallyHidden } from "@mui/utils";

// Chart data as a table component for accessibility, "Show data table" button on screen, always printed
const ChartDataTable = ({
  title,
  columns,
  rows,
  getRowKey = (_row, index) => index,
}) => {
  const [open, setOpen] = useState(false);
  const tableId = `${useId()}-data-table`;

  return (
    <Box sx={{ mt: 2 }}>
      <Button
        size="small"
        aria-expanded={open}
        aria-controls={tableId}
        onClick={() => setOpen((value) => !value)}
        sx={{ displayPrint: "none" }}
      >
        {open ? "Hide data table" : "Show data table"}
        {/* Possible several charts per page, so say which one this is */}
        <Box component="span" sx={visuallyHidden}>
          {` for ${title}`}
        </Box>
      </Button>

      <TableContainer
        id={tableId}
        hidden={!open}
        sx={{ mt: 1, overflowX: "auto", "@media print": { display: "block" } }}
      >
        <Table
          size="small"
          sx={{
            width: "auto",
            minWidth: { sm: 360 },
            "& caption": { captionSide: "top", px: 0 },
            "& thead th": {
              bgcolor: "grey.100",
              color: "text.primary",
              fontWeight: 600,
            },
            "& tbody th": {
              bgcolor: "transparent",
              color: "text.primary",
              fontWeight: 500,
            },
          }}
        >
          <caption>{title}</caption>
          <TableHead>
            <TableRow>
              {columns.map((column) => (
                <TableCell key={column.header} scope="col" align={column.align}>
                  {column.header}
                </TableCell>
              ))}
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((row, index) => (
              <TableRow key={getRowKey(row, index)}>
                {columns.map((column, columnIndex) =>
                  columnIndex === 0 ? (
                    <TableCell key={column.header} component="th" scope="row">
                      {column.value(row)}
                    </TableCell>
                  ) : (
                    <TableCell key={column.header} align={column.align}>
                      {column.value(row)}
                    </TableCell>
                  ),
                )}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Box>
  );
};

export default ChartDataTable;
