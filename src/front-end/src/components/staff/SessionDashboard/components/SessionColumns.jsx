import { Box, Chip, Tooltip } from "@mui/material";
import {
  Eye,
  ReceiptText,
  LogOut,
  Bell,
  AlertTriangle,
  Users,
} from "lucide-react";
import { actionsColumn } from "@/components/common/RowActions";
import {
  formatTime,
  getSessionDuration,
  getSessionMinutes,
  isLongSeating,
  parseUtcDate,
  sessionGuestCount,
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
        sx={{
          display: "inline-flex",
          alignItems: "center",
          gap: 0.5,
          color: "warning.dark",
          fontWeight: 600,
        }}
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

export const isOpenBill = (bill) =>
  String(bill.status).toUpperCase() === "OPEN";

// An open bill whose guest count staff should check: a guest started or changed it, or more people joined than it's for
export const billNeedsGuestCheck = (bill) =>
  isOpenBill(bill) &&
  Boolean(
    bill.started_by_guest ||
    bill.counts_changed_by_guest ||
    bill.people_on_bill > bill.total_guests,
  );

// Rows are sessions from /Dashboard/sessions plus a `requestCount` from the pending service requests
export const getSessionColumns = (actions) => [
  {
    field: "session_id",
    headerName: "Session",
    type: "number",
    align: "left",
    headerAlign: "left",
    flex: 0.8,
    minWidth: 90,
    valueFormatter: (value) => `#${value}`,
  },
  {
    field: "table_numbers",
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
    field: "started_at",
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
    valueGetter: (value, row) => getSessionMinutes(row.started_at),
    renderCell: ({ row }) => <SeatedFor startedAt={row.started_at} />,
  },
  {
    field: "guests",
    // People on the table's bills, not phones that scanned the QR code
    headerName: "Guests",
    type: "number",
    valueGetter: (value, row) => sessionGuestCount(row),
    align: "left",
    headerAlign: "left",
    flex: 0.7,
    minWidth: 80,
  },
  {
    field: "bills",
    headerName: "Open Bills",
    flex: 1.2,
    minWidth: 180,
    sortable: false,
    valueGetter: (value) => {
      const bills = value || [];
      return `${bills.filter(isOpenBill).length} of ${bills.length}`;
    },
    renderCell: ({ value, row }) => {
      const toCheck = (row.bills || []).filter(billNeedsGuestCheck).length;
      if (toCheck === 0) return value;
      return (
        <Box
          sx={{ display: "flex", alignItems: "center", gap: 1, height: "100%" }}
        >
          {value}
          <Tooltip
            title={
              toCheck === 1
                ? "A bill's guest count needs checking"
                : `${toCheck} bills' guest counts need checking`
            }
          >
            <Chip
              icon={<Users size={14} aria-hidden="true" />}
              label="Check guests"
              aria-label={`Check guests, ${tableLabel(row.table_numbers)}`}
              color="warning"
              size="small"
              variant="outlined"
              onClick={(event) => {
                event.stopPropagation();
                actions.viewSession(row);
              }}
            />
          </Tooltip>
        </Box>
      );
    },
  },
  actionsColumn(
    (row) => [
      {
        label: "View session",
        icon: Eye,
        onClick: () => actions.viewSession(row),
      },
      {
        label: "New bill",
        icon: ReceiptText,
        onClick: () => actions.newBill(row),
        hidden: !row.table_numbers?.length,
      },
      {
        label: "End session",
        icon: LogOut,
        color: "error",
        onClick: () => actions.endSession(row),
        // The API won't close a session while it still has open bills
        hidden: !row.is_closable,
      },
    ],
    {
      width: 140,
      getRowName: (row) =>
        `#${row.session_id}, ${tableLabel(row.table_numbers)}`,
    },
  ),
];
