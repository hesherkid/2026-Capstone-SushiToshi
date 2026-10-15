import React from "react";
import NextLink from "next/link";
import {
  Alert,
  AlertTitle,
  Box,
  Button,
  Chip,
  List,
  ListItem,
  ListItemText,
  Typography,
} from "@mui/material";
import { keyframes } from "@mui/material/styles";
import { ChefHat, CheckCircle2 } from "lucide-react";
import { formatTime } from "@/components/staff/SessionDashboard/utils/sessionHelpers";
import { MoreLink, PREVIEW_LIMIT } from "@/components/home/MoreLink";

const pulse = keyframes`
  0%, 100% { transform: scale(1); }
  50% { transform: scale(1.25); }
`;

const STATUS_CHIP = {
  Pending: { label: "Pending", color: "warning" },
  Processing: { label: "Processing", color: "info" },
};

const itemSummary = (items = []) => {
  const units = items.reduce((total, item) => total + (item.quantity || 0), 0);
  return `${units} ${units === 1 ? "item" : "items"}`;
};

const tableLabel = (tableNumbers = []) =>
  tableNumbers.length > 1
    ? `Tables ${tableNumbers.join(", ")}`
    : `Table ${tableNumbers[0] ?? "?"}`;

// Open kitchen orders (pending and processing) for the staff home page
export const OrderAlerts = ({ orders = [], newOrderIds = new Set() }) => {
  if (orders.length === 0) {
    return (
      <Alert severity="success" icon={<CheckCircle2 size={22} />} sx={{ alignItems: "center" }}>
        No open orders. The kitchen is all caught up.
      </Alert>
    );
  }

  const pendingCount = orders.filter((o) => o.status === "Pending").length;
  const processingCount = orders.length - pendingCount;
  const hasNew = orders.some((o) => newOrderIds.has(o.orderId));
  const preview = orders.slice(0, PREVIEW_LIMIT);

  return (
    <Alert
      severity={pendingCount > 0 ? "warning" : "info"}
      role="status"
      icon={
        <Box
          component="span"
          sx={{ display: "flex", animation: hasNew ? `${pulse} 0.6s ease-in-out 3` : "none" }}
        >
          <ChefHat size={22} />
        </Box>
      }
      action={
        <Button
          component={NextLink}
          href="/staff/dashboard/orders"
          color="inherit"
          size="small"
          sx={{ whiteSpace: "nowrap" }}
        >
          Open Orders
        </Button>
      }
      sx={{ "& .MuiAlert-message": { width: "100%" } }}
    >
      <AlertTitle sx={{ fontWeight: 700 }}>
        {orders.length === 1 ? "1 open order" : `${orders.length} open orders`}
        <Typography component="span" variant="body2" sx={{ ml: 1, fontWeight: 400 }}>
          {pendingCount} pending · {processingCount} processing
        </Typography>
      </AlertTitle>
      <List dense disablePadding>
        {preview.map((order) => {
          const chip = STATUS_CHIP[order.status];
          return (
            <ListItem key={order.orderId} disableGutters>
              <ListItemText
                primary={
                  <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
                    <Typography component="span" sx={{ fontWeight: 600 }}>
                      {tableLabel(order.tableNumbers)}
                    </Typography>
                    {chip && <Chip label={chip.label} color={chip.color} size="small" variant="outlined" />}
                    {newOrderIds.has(order.orderId) && <Chip label="New" color="warning" size="small" />}
                    <Typography component="span" variant="body2" color="text.secondary">
                      {formatTime(order.createdAt)}
                    </Typography>
                  </Box>
                }
                secondary={`Order #${order.orderId} · ${itemSummary(order.items)}${
                  order.userName ? ` · ${order.userName}` : ""
                }`}
              />
            </ListItem>
          );
        })}
      </List>
      <MoreLink
        hiddenCount={orders.length - preview.length}
        href="/staff/dashboard/orders"
        noun={orders.length - preview.length === 1 ? "order" : "orders"}
      />
    </Alert>
  );
};

export default OrderAlerts;
