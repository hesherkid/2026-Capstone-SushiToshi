import {
  DialogTitle,
  DialogContent,
  DialogActions,
  Box,
  Typography,
  Button,
  IconButton,
  Chip,
  List,
  ListItem,
  Divider,
} from "@mui/material";
import { X } from "lucide-react";
import AppDialog from "@/components/common/AppDialog";
import { parseUtcDate } from "@/components/staff/SessionDashboard/utils/sessionHelpers";
import { StatusChip, tableLabel } from "../OrderColumns";
import { currency, dateTime } from "../SessionHistoryColumns";

const plural = (count, word) => `${count} ${word}${count === 1 ? "" : "s"}`;

const formatPlaced = (createdAt) => {
  const date = parseUtcDate(createdAt);
  return date ? dateTime.format(date) : "";
};

// Read-only view of one dining session from the History tab: every order, split by bill.
// No approve, edit, deliver or undo actions
const SessionHistoryDialog = ({ session, onClose }) => (
  <AppDialog open={!!session} onClose={onClose}>
    {session && (
      <>
        <DialogTitle>
          <Box
            sx={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "flex-start",
            }}
          >
            <Box>
              <Typography variant="h6" component="span">
                {tableLabel(session.tableNumbers)} · Session #
                {session.sessionId}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {plural(session.bills.length, "bill")} ·{" "}
                {plural(session.orderCount, "order")} ·{" "}
                {currency.format(session.total)}
              </Typography>
            </Box>
            <IconButton aria-label="Close" onClick={onClose} size="small">
              <X size={20} />
            </IconButton>
          </Box>
        </DialogTitle>

        <DialogContent dividers sx={{ px: 0 }}>
          {session.bills.map((bill, billIndex) => (
            <Box
              key={bill.billId}
              component="section"
              aria-label={`Bill ${bill.billName || bill.billId}`}
            >
              <Box
                sx={{
                  px: 3,
                  py: 1.5,
                  bgcolor: "action.hover",
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "center",
                }}
              >
                <Typography variant="subtitle1" fontWeight="bold">
                  Bill: {bill.billName || `#${bill.billId}`}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {plural(bill.orders.length, "order")} ·{" "}
                  {currency.format(bill.total)}
                </Typography>
              </Box>

              {bill.orders.map((order) => (
                <Box key={order.orderId} sx={{ px: 3, pt: 1.5 }}>
                  <Box
                    sx={{
                      display: "flex",
                      alignItems: "center",
                      gap: 1,
                      flexWrap: "wrap",
                    }}
                  >
                    <Typography variant="body1" fontWeight="medium">
                      Order #{order.orderId}
                    </Typography>
                    <Typography variant="body2" color="text.secondary">
                      {formatPlaced(order.createdAt)}
                      {order.userName ? ` · ${order.userName}` : ""}
                    </Typography>
                    <StatusChip status={order.status} />
                  </Box>

                  <List dense sx={{ py: 0.5 }}>
                    {order.items.map((item) => (
                      <ListItem
                        key={item.orderItemId ?? item.itemId}
                        sx={{
                          px: 0,
                          display: "flex",
                          justifyContent: "space-between",
                          gap: 2,
                        }}
                      >
                        <Box
                          sx={{
                            display: "flex",
                            alignItems: "center",
                            gap: 1,
                            flex: 1,
                          }}
                        >
                          <Typography variant="body2">{item.name}</Typography>
                          <StatusChip status={item.status} />
                        </Box>
                        <Box
                          sx={{ display: "flex", alignItems: "center", gap: 1 }}
                        >
                          {item.price > 0 && (
                            <Typography variant="body2" color="text.secondary">
                              {currency.format(item.price * item.quantity)}
                            </Typography>
                          )}
                          <Chip label={`× ${item.quantity}`} size="small" />
                        </Box>
                      </ListItem>
                    ))}
                  </List>
                </Box>
              ))}

              {billIndex < session.bills.length - 1 && (
                <Divider sx={{ mt: 1 }} />
              )}
            </Box>
          ))}
        </DialogContent>

        <DialogActions>
          <Button onClick={onClose}>Close</Button>
        </DialogActions>
      </>
    )}
  </AppDialog>
);

export default SessionHistoryDialog;
