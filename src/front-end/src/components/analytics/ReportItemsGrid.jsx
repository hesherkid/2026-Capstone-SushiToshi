"use client";

import { Typography } from "@mui/material";
import ManagementGrid from "@/components/common/ManagementGrid";

// "#3" on screen, plain number in the CSV
export const rankColumn = (field) => ({
  field,
  headerName: "Rank",
  type: "number",
  width: 90,
  align: "left",
  headerAlign: "left",
  sortComparator: (a, b) => (a ?? Infinity) - (b ?? Infinity) || 0,
  renderCell: (params) => (params.value == null ? "None" : `#${params.value}`),
});

// "-" on screen, empty in the CSV, sorted below real values.
export const nullableNumberColumn = (
  field,
  headerName,
  format,
  options = {},
) => ({
  field,
  headerName,
  type: "number",
  width: 130,
  sortComparator: (a, b) => (a ?? -1) - (b ?? -1),
  renderCell: (params) => (params.value == null ? "-" : format(params.value)),
  ...options,
});

// "Top 5 items" to "top-5-items" for export file names
const slug = (text) =>
  text
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");

const COMPACT_MAX_ROWS = 10; // ManagementGrid's first page size; more rows than this means paging

// Search and column filters only narrow the rows already loaded for the report's date range,
// location, category and tag. The CSV holds the rows that match them, with the visible columns.
const ReportItemsGrid = ({
  title = "All items",
  caption,
  report,
  range,
  rows,
  columns,
  compact = false,
  selectedItemId,
  onSelect,
  highlightRow,
  emptyText,
}) => {
  const shownCaption =
    caption ??
    (compact
      ? null
      : "Search and column filters apply to this period's results. Export downloads the rows that match them as CSV, or prints them.");

  // e.g. item-performance_top-5-items_2026-10-01_to_2026-10-07.csv
  const fileName = [
    report,
    slug(title),
    range && `${range.from}_to_${range.to}`,
  ]
    .filter(Boolean)
    .join("_");

  return (
    <>
      <Typography variant="h6" gutterBottom={!shownCaption}>
        {title}
      </Typography>
      {shownCaption && (
        <Typography variant="body2" color="text.secondary" gutterBottom>
          {shownCaption}
        </Typography>
      )}
      <ManagementGrid
        rows={rows}
        columns={columns}
        getRowId={(row) => row.item_id}
        onRowClick={
          onSelect ? (params) => onSelect(params.row.item_id) : undefined
        }
        getRowClassName={(params) =>
          [
            highlightRow?.(params.row) && "report-row-highlight",
            params.row.item_id === selectedItemId && "report-row-selected",
          ]
            .filter(Boolean)
            .join(" ")
        }
        localeText={{ noRowsLabel: emptyText }}
        {...(compact && {
          disableColumnFilter: true,
          disableColumnSelector: true,
          disableDensitySelector: true,
          hideFooter: rows.length <= COMPACT_MAX_ROWS,
        })}
        slotProps={{
          toolbar: {
            ...(compact && { showQuickFilter: false }),
            csvOptions: { fileName },
            printOptions: { hideFooter: true, hideToolbar: true },
          },
        }}
        sx={{
          ...(onSelect && { "& .MuiDataGrid-row": { cursor: "pointer" } }),
          "& .report-row-highlight": { bgcolor: "warning.light" },
          "& .report-row-selected": { bgcolor: "action.selected" },
        }}
      />
      {/* Printing the whole report only captures the page of rows on screen; the grid's own Print has them all. */}
      {rows.length > COMPACT_MAX_ROWS && (
        <Typography
          variant="body2"
          color="text.secondary"
          sx={{ display: "none", displayPrint: "block", mt: 1 }}
        >
          Only the rows on the current page are printed here. Use this
          table&apos;s Export, then Print, for the full list.
        </Typography>
      )}
    </>
  );
};

export default ReportItemsGrid;
