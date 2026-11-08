import { Eye } from "lucide-react";
import { actionsColumn } from "@/components/common/RowActions";
import { parseUtcDate } from "@/components/staff/SessionDashboard/utils/sessionHelpers";
import { StatusChip, tableLabel } from "./OrderColumns";

export const currency = new Intl.NumberFormat("en-CA", {
  style: "currency",
  currency: "CAD",
});

// History can span several days, so show the date as well as the time
export const dateTime = new Intl.DateTimeFormat("en-CA", {
  dateStyle: "medium",
  timeStyle: "short",
});

export const billLabel = (bill) => bill.billName || `Bill #${bill.billId}`;

export const getSessionHistoryColumns = ({ view }) => [
  {
    field: "tableNumbers",
    headerName: "Table",
    flex: 1,
    minWidth: 110,
    sortable: false,
    valueGetter: (value) => tableLabel(value),
  },
  {
    field: "sessionId",
    headerName: "Session",
    type: "number",
    align: "left",
    headerAlign: "left",
    flex: 0.7,
    minWidth: 90,
    valueFormatter: (value) => `#${value}`,
  },
  {
    field: "billNames",
    headerName: "Bills",
    flex: 1.4,
    minWidth: 150,
    sortable: false,
    valueGetter: (value) => (value || []).join(", "),
  },
  {
    field: "firstOrderAt",
    headerName: "First Order",
    type: "dateTime",
    flex: 1.3,
    minWidth: 170,
    valueGetter: (value) => parseUtcDate(value),
    valueFormatter: (value) => (value ? dateTime.format(value) : ""),
  },
  {
    field: "orderCount",
    headerName: "Orders",
    type: "number",
    align: "left",
    headerAlign: "left",
    flex: 0.6,
    minWidth: 80,
  },
  {
    field: "itemCount",
    headerName: "Items",
    type: "number",
    align: "left",
    headerAlign: "left",
    flex: 0.6,
    minWidth: 80,
  },
  {
    field: "total",
    headerName: "Total",
    type: "number",
    align: "left",
    headerAlign: "left",
    flex: 0.8,
    minWidth: 90,
    valueFormatter: (value) => currency.format(value || 0),
  },
  actionsColumn(
    (row) => [
      {
        label: "View table orders",
        ariaLabel: `View table orders for session #${row.sessionId}, ${tableLabel(row.tableNumbers)}`,
        icon: Eye,
        onClick: () => view(row),
      },
    ],
    { width: 80 },
  ),
];

export const toOrderItemRows = (order) =>
  order.items.map((item, index) => ({
    id: item.orderItemId ?? item.itemId ?? index,
    item: item.name,
    quantity: item.quantity,
    price: item.price > 0 ? item.price * item.quantity : null,
    status: item.status,
  }));

export const getOrderItemColumns = () => [
  { field: "item", headerName: "Item", flex: 2, minWidth: 160 },
  {
    field: "quantity",
    headerName: "Qty",
    type: "number",
    align: "left",
    headerAlign: "left",
    flex: 0.5,
    minWidth: 70,
  },
  {
    field: "price",
    headerName: "Price",
    type: "number",
    align: "left",
    headerAlign: "left",
    flex: 0.7,
    minWidth: 90,
    valueFormatter: (value) =>
      value == null ? "Included" : currency.format(value),
  },
  {
    field: "status",
    headerName: "Status",
    flex: 0.8,
    minWidth: 120,
    renderCell: ({ value }) => <StatusChip status={value} />,
  },
];
