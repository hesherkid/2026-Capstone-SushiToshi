import { useMemo } from "react";
import {
  Button,
  Chip,
  DialogActions,
  DialogContent,
  Paper,
  Stack,
  Typography,
} from "@mui/material";
import AppDialog from "@/components/common/AppDialog";
import DialogHeader from "@/components/common/DialogHeader";
import ManagementGrid from "@/components/common/ManagementGrid";
import {
  formatDate,
  formatTime,
  parseUtcDate,
} from "@/components/staff/SessionDashboard/utils/sessionHelpers";
import { StatusChip, tableLabel } from "../OrderColumns";
import {
  billLabel,
  currency,
  getOrderItemColumns,
  toOrderItemRows,
} from "../SessionHistoryColumns";

const PAGE_SIZE = 100;

const plural = (count, word) => `${count} ${word}${count === 1 ? "" : "s"}`;

const OrderSection = ({ order, bill, formatPlaced, columns }) => {
  const rows = useMemo(() => toOrderItemRows(order), [order]);

  return (
    <Paper
      variant="outlined"
      component="section"
      aria-labelledby={`order-${order.orderId}`}
      sx={{ p: 2 }}
    >
      <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 0.5 }}>
        <Typography
          id={`order-${order.orderId}`}
          variant="subtitle1"
          component="h3"
          fontWeight="bold"
        >
          Order #{order.orderId}
        </Typography>
        <StatusChip status={order.status} />
      </Stack>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
        {[
          formatPlaced(order.createdAt),
          `Bill: ${billLabel(bill)}`,
          `Guest: ${order.userName || "—"}`,
        ]
          .filter(Boolean)
          .join(" · ")}
      </Typography>

      <ManagementGrid
        rows={rows}
        columns={columns}
        showToolbar={false}
        hideFooter={rows.length <= PAGE_SIZE}
        initialState={{
          pagination: { paginationModel: { pageSize: PAGE_SIZE } },
        }}
        pageSizeOptions={[PAGE_SIZE]}
        disableColumnMenu
        disableColumnSorting
        aria-label={`Order #${order.orderId} items`}
        localeText={{ noRowsLabel: "No items" }}
      />
    </Paper>
  );
};

const SessionHistoryDialog = ({ session, onClose }) => {
  const columns = useMemo(() => getOrderItemColumns(), []);

  const firstAt = session ? parseUtcDate(session.firstOrderAt) : null;
  const firstDay = firstAt?.toDateString();

  const formatPlaced = (createdAt) => {
    const date = parseUtcDate(createdAt);
    if (!date) return "";
    return date.toDateString() === firstDay
      ? formatTime(date)
      : `${formatDate(date)} ${formatTime(date)}`;
  };

  const title = session
    ? `${tableLabel(session.tableNumbers)} · Session #${session.sessionId}`
    : "";
  const subtitle =
    session &&
    [
      firstAt && formatDate(firstAt),
      plural(session.bills.length, "bill"),
      plural(session.orderCount, "order"),
    ]
      .filter(Boolean)
      .join(" · ");

  return (
    <AppDialog open={!!session} onClose={onClose} maxWidth="md">
      <DialogHeader title={title} subtitle={subtitle} onClose={onClose} />
      {session && (
        <DialogContent dividers>
          <Stack spacing={2}>
            {session.bills.flatMap((bill) =>
              bill.orders.map((order) => (
                <OrderSection
                  key={order.orderId}
                  order={order}
                  bill={bill}
                  formatPlaced={formatPlaced}
                  columns={columns}
                />
              )),
            )}
          </Stack>

          <Stack
            direction="row"
            spacing={1}
            useFlexGap
            flexWrap="wrap"
            alignItems="center"
            justifyContent="flex-end"
            sx={{ mt: 2 }}
          >
            <Typography variant="body2" color="text.secondary">
              Bill subtotals:
            </Typography>
            {session.bills.map((bill) => (
              <Chip
                key={bill.billId}
                label={`${billLabel(bill)}: ${currency.format(bill.total)}`}
                size="small"
                variant="outlined"
              />
            ))}
            <Chip
              label={`Total: ${currency.format(session.total)}`}
              size="small"
            />
          </Stack>
        </DialogContent>
      )}
      <DialogActions>
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </AppDialog>
  );
};

export default SessionHistoryDialog;
