import { Box, Chip, Tooltip } from "@mui/material";
import { Eye, ReceiptText, LogOut, Bell, AlertTriangle } from "lucide-react";
import { actionsColumn } from "@/components/common/RowActions";
import {
  formatTime,
  getSessionDuration,
  getSessionMinutes,
  isLongSeating,
  parseUtcDate,
  LONG_SEATING_MINUTES,
} from "../utils/sessionHelpers";

// "Seated For" text, with a warning once a table passes the long-seating limit
export const SeatedFor = ({ startedAt }) => {
  if (!startedAt) return "—";
  const duration = getSessionDuration(startedAt);
  if (!isLongSeating(startedAt)) return duration;

  const hours = LONG_SEATING_MINUTES / 60;
  return (
    <Tooltip title={`Seated for more than ${hours} hours`}>
      <Box
        component="span"
        sx={{ display: "inline-flex", alignItems: "center", gap: 0.5, color: "warning.dark", fontWeight: 600 }}
      >
        {duration}
        {/* The Tooltip labels the wrapper for screen readers */}
        <AlertTriangle size={16} aria-hidden />
      </Box>
    </Tooltip>
  );
};

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
    valueGetter: (value) => parseUtcDate(value),
    valueFormatter: (value) => (value ? formatTime(value) : ""),
  },
  {
    field: "duration",
    headerName: "Seated For",
    type: "number",
    align: "left",
    headerAlign: "left",
    flex: 0.8,
    minWidth: 110,
    // Minutes, so the column sorts by how long tables have been seated
    valueGetter: (value, row) => getSessionMinutes(row.started_At),
    renderCell: ({ row }) => <SeatedFor startedAt={row.started_At} />,
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
