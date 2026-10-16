import { useState } from "react";
import {
  Dialog,
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
  Snackbar,
  Alert,
  CircularProgress,
} from "@mui/material";
import {
  X,
  Check,
  Trash2,
  Plus,
  Minus,
  Package,
  Edit2,
  RotateCcw,
  Save,
  CheckCircle,
} from "lucide-react";
import { useOrderDashboard } from "../../context/OrderDashboardContext";
import { useOrderDashboardActions } from "../../hooks/useOrderDashboardActions";
import { ORDER_STATUS, normalizeStatus } from "../../hooks/useOrderData";
import { StatusChip, tableLabel } from "../OrderColumns";
import ConfirmDialog from "./ConfirmDialog";

const QuantityChip = ({ quantity }) => (
  <Chip
    label={`× ${quantity}`}
    size="small"
    sx={{
      bgcolor: "grey.200",
      color: "text.primary",
      fontWeight: "medium",
      minWidth: "50px",
    }}
  />
);

const stepperSx = {
  border: "1px solid",
  borderColor: "divider",
  borderRadius: 1,
};

const OrderDetailDialog = () => {
  const {
    selectedOrder: order,
    closeOrder,
    isEditing,
    editedQuantities,
    startEditing,
    stopEditing,
    changeQuantity,
  } = useOrderDashboard();
  const actions = useOrderDashboardActions();
  const [pendingCancel, setPendingCancel] = useState(null);
  const [lastCancelled, setLastCancelled] = useState(null);

  const handleConfirmCancel = async () => {
    await actions.cancelItem(order, pendingCancel);
    setLastCancelled(pendingCancel);
    setPendingCancel(null);
  };

  const handleUndoCancel = async () => {
    if (!lastCancelled) return;
    await actions.undoCancelItem(order, lastCancelled);
    setLastCancelled(null);
  };

  const isPending = order?.status === ORDER_STATUS.PENDING;
  const isProcessing = order?.status === ORDER_STATUS.PROCESSING;
  const busy = actions.isSaving;

  const renderItemControls = (item) => {
    const itemStatus = normalizeStatus(item.status);

    if (itemStatus === ORDER_STATUS.CANCELLED) {
      return (
        <>
          <QuantityChip quantity={item.quantity} />
          {isPending && (
            <Button
              variant="outlined"
              color="info"
              size="small"
              startIcon={<RotateCcw size={16} />}
              onClick={() => actions.undoCancelItem(order, item)}
              sx={{ ml: 1 }}
            >
              Undo
            </Button>
          )}
        </>
      );
    }

    if (isPending && isEditing) {
      return (
        <>
          <IconButton
            aria-label="Decrease quantity"
            onClick={() => changeQuantity(item.itemId, -1)}
            size="small"
            sx={stepperSx}
          >
            <Minus size={16} />
          </IconButton>
          <Typography
            sx={{ minWidth: "40px", textAlign: "center", fontWeight: "medium" }}
          >
            {editedQuantities[item.itemId] ?? item.quantity}
          </Typography>
          <IconButton
            aria-label="Increase quantity"
            onClick={() => changeQuantity(item.itemId, 1)}
            size="small"
            sx={stepperSx}
          >
            <Plus size={16} />
          </IconButton>
          <IconButton
            aria-label="Remove item"
            color="error"
            size="small"
            onClick={() => setPendingCancel(item)}
          >
            <Trash2 size={18} />
          </IconButton>
        </>
      );
    }

    return (
      <>
        <QuantityChip quantity={item.quantity} />
        {isProcessing && itemStatus !== ORDER_STATUS.DELIVERED && (
          <Button
            variant="outlined"
            color="success"
            size="small"
            startIcon={<Check size={16} />}
            onClick={() => actions.markItemDelivered(order, item)}
            sx={{ minWidth: "120px", textTransform: "none" }}
          >
            Mark Delivered
          </Button>
        )}
      </>
    );
  };

  return (
    <>
      <Dialog
        open={!!order}
        onClose={closeOrder}
        maxWidth="sm"
        fullWidth
        slotProps={{ paper: { sx: { maxHeight: "90vh" } } }}
      >
        {order && (
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
                  <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                    <Typography variant="h6" component="span">
                      Order #{order.orderId} · {tableLabel(order.tableNumbers)}
                    </Typography>
                    <StatusChip status={order.status} />
                  </Box>
                  <Typography variant="body2" color="text.secondary">
                    Session #{order.sessionId} · {order.items.length} item
                    {order.items.length === 1 ? "" : "s"}
                  </Typography>
                </Box>
                <IconButton aria-label="Close" onClick={closeOrder} size="small">
                  <X size={20} />
                </IconButton>
              </Box>
            </DialogTitle>

            <DialogContent dividers sx={{ px: 0 }}>
              <List sx={{ py: 0 }}>
                {order.items.map((item, index) => (
                  <Box key={item.orderItemId ?? item.itemId}>
                    <ListItem
                      sx={{
                        py: 2,
                        px: 3,
                        display: "flex",
                        justifyContent: "space-between",
                        alignItems: "center",
                      }}
                    >
                      <Box sx={{ flex: 1 }}>
                        <Typography variant="body1" fontWeight="medium">
                          {item.name}
                        </Typography>
                        <StatusChip status={item.status} sx={{ mt: 0.5 }} />
                      </Box>
                      <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                        {renderItemControls(item)}
                      </Box>
                    </ListItem>
                    {index < order.items.length - 1 && <Divider />}
                  </Box>
                ))}
              </List>
            </DialogContent>

            <DialogActions sx={{ p: 2, gap: 1 }}>
              {isPending && isEditing && (
                <>
                  <Button onClick={stopEditing} disabled={busy} color="inherit">
                    Cancel
                  </Button>
                  <Box sx={{ flex: 1 }} />
                  <Button
                    variant="outlined"
                    onClick={() => actions.saveChanges(order)}
                    disabled={busy}
                    startIcon={busy ? <CircularProgress size={16} /> : <Save size={16} />}
                  >
                    Save Changes
                  </Button>
                  <Button
                    variant="contained"
                    color="success"
                    onClick={() => actions.saveAndApprove(order)}
                    disabled={busy}
                    startIcon={
                      busy ? <CircularProgress size={16} /> : <CheckCircle size={16} />
                    }
                  >
                    Save & Approve
                  </Button>
                </>
              )}

              {isPending && !isEditing && (
                <>
                  <Button
                    variant="outlined"
                    startIcon={<Edit2 />}
                    onClick={startEditing}
                    fullWidth
                  >
                    Edit Order
                  </Button>
                  <Button
                    variant="contained"
                    color="success"
                    startIcon={<Check />}
                    onClick={() => actions.approve(order)}
                    disabled={busy}
                    fullWidth
                    size="large"
                  >
                    Approve Order
                  </Button>
                </>
              )}

              {isProcessing && (
                <Button
                  variant="contained"
                  color="success"
                  startIcon={<Package />}
                  onClick={() => actions.markAllDelivered(order)}
                  disabled={busy}
                  fullWidth
                  size="large"
                >
                  Mark All Delivered
                </Button>
              )}
            </DialogActions>
          </>
        )}
      </Dialog>

      <ConfirmDialog
        open={!!pendingCancel}
        title="Remove order item"
        message={`Remove ${pendingCancel?.name ?? "this item"}? It will be cancelled and won't be delivered.`}
        confirmLabel="Remove Item"
        onConfirm={handleConfirmCancel}
        onClose={() => setPendingCancel(null)}
      />

      <Snackbar
        open={!!lastCancelled}
        autoHideDuration={6000}
        onClose={() => setLastCancelled(null)}
        anchorOrigin={{ vertical: "bottom", horizontal: "center" }}
      >
        <Alert
          severity="info"
          action={
            <Button
              color="inherit"
              size="small"
              startIcon={<RotateCcw size={16} />}
              onClick={handleUndoCancel}
            >
              Undo
            </Button>
          }
        >
          Item cancelled.
        </Alert>
      </Snackbar>
    </>
  );
};

export default OrderDetailDialog;
