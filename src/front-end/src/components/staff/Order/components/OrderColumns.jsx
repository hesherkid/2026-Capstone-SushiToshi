import { Chip } from "@mui/material";
import { Eye, Check, Package } from "lucide-react";
import { actionsColumn } from "@/components/common/RowActions";
import { formatTime } from "@/components/staff/SessionDashboard/utils/sessionHelpers";
import { ORDER_STATUS, normalizeStatus } from "../hooks/useOrderData";

export const STATUS_OPTIONS = {
  [ORDER_STATUS.PENDING]: { label: "Pending", color: "warning" },
  [ORDER_STATUS.PROCESSING]: { label: "Processing", color: "info" },
  [ORDER_STATUS.DELIVERED]: { label: "Delivered", color: "success" },
  [ORDER_STATUS.CANCELLED]: { label: "Cancelled", color: "error" },
};

export const StatusChip = ({ status, sx }) => {
  const option = STATUS_OPTIONS[normalizeStatus(status)] ?? {
    label: status || "Pending",
    color: "default",
  };
  return <Chip label={option.label} color={option.color} size="small" sx={sx} />;
};

export const tableLabel = (tableNumbers = []) => {
  if (tableNumbers.length === 0) return "—";
  return tableNumbers.length > 1
    ? `Tables ${tableNumbers.join(", ")}`
    : `Table ${tableNumbers[0]}`;
};

const currency = new Intl.NumberFormat("en-CA", {
  style: "currency",
  currency: "CAD",
});

export const getOrderColumns = (actions) => [
  {
    field: "orderId",
    headerName: "Order",
    type: "number",
    align: "left",
    headerAlign: "left",
    flex: 0.8,
    minWidth: 90,
    valueFormatter: (value) => `#${value}`,
  },
  {
    field: "tableNumbers",
    headerName: "Table",
    flex: 1,
    minWidth: 110,
    sortable: false,
    valueGetter: (value) => tableLabel(value),
  },
  {
    field: "createdAt",
    headerName: "Order Placed",
    type: "dateTime",
    flex: 1,
    minWidth: 100,
    valueGetter: (value) => (value ? new Date(value) : null),
    valueFormatter: (value) => (value ? formatTime(value) : ""),
  },
  {
    field: "itemCount",
    headerName: "Items",
    type: "number",
    align: "left",
    headerAlign: "left",
    flex: 0.7,
    minWidth: 80,
  },
  {
    field: "orderTotal",
    headerName: "Total",
    type: "number",
    align: "left",
    headerAlign: "left",
    flex: 0.8,
    minWidth: 90,
    valueFormatter: (value) => currency.format(value || 0),
  },
  {
    field: "status",
    headerName: "Status",
    flex: 1,
    minWidth: 120,
    renderCell: ({ value }) => <StatusChip status={value} />,
  },
  actionsColumn(
    (row) => [
      { label: "View order", icon: Eye, onClick: () => actions.view(row) },
      {
        label: "Approve order",
        icon: Check,
        color: "success",
        onClick: () => actions.approve(row),
        hidden: row.status !== ORDER_STATUS.PENDING,
      },
      {
        label: "Mark all delivered",
        icon: Package,
        color: "success",
        onClick: () => actions.markAllDelivered(row),
        hidden: row.status !== ORDER_STATUS.PROCESSING,
      },
    ],
    { width: 130 }
  ),
];
