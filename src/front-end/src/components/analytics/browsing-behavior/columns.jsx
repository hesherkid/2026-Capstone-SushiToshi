import { Chip } from "@mui/material";
import ReportItemsGrid, {
  nullableNumberColumn,
  rankColumn,
} from "@/components/analytics/ReportItemsGrid";
import { pct, secs } from "@/components/analytics/reportUtils";

// Badges for item current state.
const StatusChips = ({ item }) => (
  <>
    {item.status === "Seasonal" && (
      <Chip label="Seasonal" size="small" variant="outlined" sx={{ ml: 1 }} />
    )}
    {!item.currently_available && (
      <Chip
        label="Not available now"
        size="small"
        variant="outlined"
        color="default"
        sx={{ ml: 1 }}
      />
    )}
  </>
);

// Columns shared by several tables. Screen shows "#3", "12s" and "40%"; the CSV gets plain numbers.
const NAME_COL = {
  field: "name",
  headerName: "Item",
  flex: 1,
  minWidth: 180,
  renderCell: (params) => (
    <>
      {params.value}
      <StatusChips item={params.row} />
    </>
  ),
};
export const RANK_COL = rankColumn("view_rank");
export const VIEWS_COL = {
  field: "views",
  headerName: "Views",
  type: "number",
  width: 90,
};
export const AVG_VIEW_COL = nullableNumberColumn(
  "average_view_seconds",
  "Avg view (s)",
  secs,
);
export const CONVERSION_COL = nullableNumberColumn(
  "conversion_rate",
  "Conversion (%)",
  pct,
);
export const ORDERED_BY_COL = {
  field: "ordering_sessions",
  headerName: "Ordered by (tables)",
  type: "number",
  width: 150,
};
export const UNAVAILABLE_VIEWS_COL = {
  field: "unavailable_views",
  headerName: "Views while unavailable",
  type: "number",
  width: 190,
};
export const NOW_COL = {
  field: "available_now",
  headerName: "Now",
  width: 120,
  valueGetter: (_value, row) =>
    row.currently_available ? "Available" : "Unavailable",
};

// Short report tables: item name first, then the given columns, with an export button.
export const ItemTable = ({ items, columns, ...props }) => (
  <ReportItemsGrid
    compact
    report="browsing-behavior"
    rows={items}
    columns={[NAME_COL, ...columns]}
    {...props}
  />
);

// Same wording as StatusChips, as text so it also lands in the CSV.
const itemStatus = (item) => {
  if (!item.currently_available) return "Not available now";
  if (item.status === "Seasonal") return "Seasonal";
  return "Available";
};

// "All items" grid columns.
export const ALL_ITEMS_COLUMNS = [
  RANK_COL,
  { field: "name", headerName: "Item", flex: 1, minWidth: 180 },
  {
    field: "availability",
    headerName: "Status",
    width: 160,
    valueGetter: (_value, row) => itemStatus(row),
    renderCell: (params) =>
      params.value === "Available" ? (
        params.value
      ) : (
        <Chip label={params.value} size="small" variant="outlined" />
      ),
  },
  { field: "category_name", headerName: "Category", flex: 1, minWidth: 140 },
  VIEWS_COL,
  AVG_VIEW_COL,
  CONVERSION_COL,
];
