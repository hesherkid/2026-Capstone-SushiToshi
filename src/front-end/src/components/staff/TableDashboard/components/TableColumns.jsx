import { Chip } from "@mui/material";
import { Edit, Trash2, Power, PowerOff, Unlink, Plus } from "lucide-react";
import { actionsColumn } from "@/components/common/RowActions";
import { TABLE_STATUS } from "../hooks/useTableData";

export const STATUS_OPTIONS = {
  [TABLE_STATUS.AVAILABLE]: { label: "Available", color: "success" },
  [TABLE_STATUS.IN_SESSION]: { label: "In session", color: "error" },
  [TABLE_STATUS.INACTIVE]: { label: "Inactive", color: "default" },
};

// Sorts free tables first, then busy, then inactive
const STATUS_ORDER = [
  TABLE_STATUS.AVAILABLE,
  TABLE_STATUS.IN_SESSION,
  TABLE_STATUS.INACTIVE,
];

const statusColumn = {
  field: "status",
  headerName: "Status",
  flex: 1,
  minWidth: 130,
  type: "singleSelect",
  valueOptions: STATUS_ORDER.map((value) => ({
    value,
    label: STATUS_OPTIONS[value].label,
  })),
  sortComparator: (a, b) => STATUS_ORDER.indexOf(a) - STATUS_ORDER.indexOf(b),
  renderCell: ({ value }) => (
    <Chip
      label={STATUS_OPTIONS[value]?.label}
      color={STATUS_OPTIONS[value]?.color}
      size="small"
      variant={value === TABLE_STATUS.INACTIVE ? "outlined" : "filled"}
    />
  ),
};

export const getTableColumns = (actions, { canDelete = false } = {}) => [
  {
    field: "table_number",
    headerName: "Table Number",
    type: "number",
    align: "left",
    headerAlign: "left",
    flex: 1,
    minWidth: 90,
    valueFormatter: (value) => `Table ${value}`,
  },
  statusColumn,
  {
    field: "seat_count",
    headerName: "Seats",
    type: "number",
    align: "left",
    headerAlign: "left",
    flex: 1,
    minWidth: 80,
  },
  {
    field: "group_Name",
    headerName: "Group",
    flex: 1,
    minWidth: 120,
    renderCell: ({ value }) =>
      value ? <Chip label={value} size="small" variant="outlined" /> : "—",
  },
  actionsColumn(
    (row) => {
      const inSession = row.status === TABLE_STATUS.IN_SESSION;
      return [
        { label: "Edit table", icon: Edit, onClick: () => actions.editTable(row) },
        {
          label: row.is_active ? "Deactivate" : "Activate",
          icon: row.is_active ? PowerOff : Power,
          onClick: () => actions.toggleTable(row),
          hidden: inSession,
        },
        {
          label: "Remove from group",
          icon: Unlink,
          onClick: () => actions.removeTableFromGroup(row),
          color: "warning",
          hidden: !row.tableGroup_Id || inSession,
        },
        {
          label: "Delete table",
          icon: Trash2,
          onClick: () => actions.deleteTable(row),
          color: "error",
          // Only admins may delete tables; staff can deactivate instead
          hidden: !canDelete || inSession,
        },
      ];
    },
    { width: 180 }
  ),
];

export const getGroupColumns = (actions) => [
  {
    field: "group_Name",
    headerName: "Group",
    flex: 1.5,
    minWidth: 140,
  },
  statusColumn,
  {
    field: "tables",
    headerName: "Tables",
    flex: 2,
    minWidth: 160,
    sortable: false,
    valueGetter: (value) =>
      (value || [])
        .map((t) => t.table_number)
        .sort((a, b) => a - b)
        .join(", ") || "—",
  },
  {
    field: "total_seats",
    headerName: "Seats",
    type: "number",
    align: "left",
    headerAlign: "left",
    flex: 1,
    minWidth: 80,
  },
  actionsColumn(
    (row) => {
      const inSession = row.status === TABLE_STATUS.IN_SESSION;
      return [
        { label: "Edit group", icon: Edit, onClick: () => actions.editGroup(row) },
        {
          label: "Add table",
          icon: Plus,
          onClick: () => actions.addTableToGroup(row),
        },
        {
          label: row.is_Active ? "Deactivate" : "Activate",
          icon: row.is_Active ? PowerOff : Power,
          onClick: () => actions.toggleGroup(row),
          hidden: inSession,
        },
        {
          label: "Delete group",
          icon: Trash2,
          onClick: () => actions.deleteGroup(row),
          color: "error",
          hidden: inSession,
        },
      ];
    },
    { width: 180 }
  ),
];
