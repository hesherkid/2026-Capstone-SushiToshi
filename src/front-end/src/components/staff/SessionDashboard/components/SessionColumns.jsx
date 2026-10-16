import { Chip } from "@mui/material";
import { Eye, ReceiptText, LogOut, Bell } from "lucide-react";
import { actionsColumn } from "@/components/common/RowActions";
import { formatTime, getSessionDuration } from "../utils/sessionHelpers";

export const tableLabel = (tableNumbers = []) => {
  if (tableNumbers.length === 0) return "—";
  return tableNumbers.length > 1
    ? `Tables ${tableNumbers.join(", ")}`
    : `Table ${tableNumbers[0]}`;
};

export const isOpenBill = (bill) => String(bill.status).toUpperCase() === "OPEN";

// Rows are sessions from /Dashboard/sessions plus a `requestCount` from the pending service requests
export const getSessionColumns = (actions) => [
  {
    field: "session_Id",
    headerName: "Session",
    type: "number",
    align: "left",
    headerAlign: "left",
    flex: 0.8,
    minWidth: 90,
    valueFormatter: (value) => `#${value}`,
  },
  {
    field: "table_Numbers",
    headerName: "Table",
    flex: 1.2,
    minWidth: 120,
    sortable: false,
    valueGetter: (value) => tableLabel(value),
  },
  {
    field: "requestCount",
    headerName: "Requests",
    type: "number",
    align: "left",
    headerAlign: "left",
    flex: 0.9,
    minWidth: 110,
    renderCell: ({ value }) =>
      value > 0 ? (
        <Chip
          icon={<Bell size={14} />}
          label={value === 1 ? "1 request" : `${value} requests`}
          color="warning"
          size="small"
        />
      ) : (
        "—"
      ),
  },
  {
    field: "started_At",
    headerName: "Started",
    type: "dateTime",
    flex: 1,
    minWidth: 100,
    valueGetter: (value) => (value ? new Date(value) : null),
    valueFormatter: (value) => (value ? formatTime(value) : ""),
  },
  {
    field: "duration",
    headerName: "Seated For",
    flex: 0.8,
    minWidth: 100,
    sortable: false,
    valueGetter: (value, row) =>
      row.started_At ? getSessionDuration(row.started_At) : "—",
  },
  {
    field: "active_Participants",
    headerName: "Guests",
    type: "number",
    align: "left",
    headerAlign: "left",
    flex: 0.7,
    minWidth: 80,
  },
  {
    field: "bills",
    headerName: "Open Bills",
    flex: 0.8,
    minWidth: 100,
    sortable: false,
    valueGetter: (value) => {
      const bills = value || [];
      return `${bills.filter(isOpenBill).length} of ${bills.length}`;
    },
  },
  actionsColumn(
    (row) => [
      { label: "View session", icon: Eye, onClick: () => actions.viewSession(row) },
      {
        label: "New bill",
        icon: ReceiptText,
        onClick: () => actions.newBill(row),
        hidden: !row.table_Numbers?.length,
      },
      {
        label: "End session",
        icon: LogOut,
        color: "error",
        onClick: () => actions.endSession(row),
        // The API won't close a session while it still has open bills
        hidden: !row.is_Closable,
      },
    ],
    { width: 140 }
  ),
];
