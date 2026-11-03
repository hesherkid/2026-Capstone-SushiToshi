import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  DialogActions,
  DialogContent,
  Divider,
  TextField,
  Typography,
} from "@mui/material";
import AppDialog from "@/components/common/AppDialog";
import DialogHeader from "@/components/common/DialogHeader";
import CartLine from "@/components/menu/CartLine";
import { matchesSearch } from "@/components/menu/searchUtils";
import { useNotification } from "@/contexts/NotificationContext";
import { useSession } from "../../context/SessionContext";
import { useStaffOrder } from "../../hooks/useStaffOrder";
import { isOpenBill, tableLabel } from "../SessionColumns";

const money = (value) => `$${(Number(value) || 0).toFixed(2)}`;

// Staff ordering on one of a table's bills, recorded under the staff member.
// Same menu, prices and limits as the guests see
const StaffOrderDialog = ({ open, sessionId, billId, onClose }) => {
  const { sessions, refreshData } = useSession();
  const { notifySuccess } = useNotification();

  // The session as of the last poll, so a closed bill shows up here
  const session = open ? sessions.find((s) => s.session_Id === sessionId) ?? null : null;
  const bill = session?.bills?.find((b) => b.bill_Id === billId);

  const order = useStaffOrder(session);
  const [search, setSearch] = useState("");
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (!open) return;
    setSearch("");
    setError(null);
  }, [open, sessionId, billId]);

  const canOrder = Boolean(bill && isOpenBill(bill));

  const handleClose = () => {
    if (!isSaving) onClose();
  };

  // A changed cart may fix the last error (e.g. over a limit), so clear it
  const handleQuantityChange = (categoryId, itemId, delta) => {
    order.changeQuantity(categoryId, itemId, delta);
    setError(null);
  };

  const handlePlaceOrder = async () => {
    setIsSaving(true);
    setError(null);
    try {
      await order.placeOrder(billId);
      notifySuccess(`Order placed on ${bill.bill_Name}`);
      refreshData();
      onClose();
    } catch (err) {
      console.error("Error placing staff order:", err);
      setError(err.response?.data?.message || "Couldn't place the order. Please try again.");
    } finally {
      setIsSaving(false);
    }
  };

  // Picked items stay in the list whatever is searched
  const groups = order.categories
    .map((category) => ({
      category,
      items: (order.menuItems[category.category_id] || []).filter(
        (item) =>
          order.quantities[category.category_id]?.[item.item_id] > 0 || matchesSearch(item, search),
      ),
    }))
    .filter(({ items }) => items.length > 0);

  const lineFor = (itemId) => order.cartLines.find((line) => line.item.item_id === itemId);

  const renderMenu = () => {
    if (order.isLoading) {
      return (
        <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
          <CircularProgress aria-label="Loading the menu" />
        </Box>
      );
    }
    if (order.loadError) {
      return (
        <Alert severity="error" sx={{ mx: 3, mb: 2 }}>
          {order.loadError}
        </Alert>
      );
    }
    if (groups.length === 0) {
      return (
        <Typography color="text.secondary" sx={{ px: 3, pb: 2 }}>
          {search.trim() ? "No items match your search." : "This menu has no items."}
        </Typography>
      );
    }
    return groups.map(({ category, items }) => {
      const headingId = `staff-order-category-${category.category_id}`;
      return (
        <Box component="section" key={category.category_id} aria-labelledby={headingId}>
          <Typography
            id={headingId}
            variant="subtitle2"
            component="h3"
            sx={{ px: 3, py: 1, bgcolor: "grey.100" }}
          >
            {category.name || `Category ${category.category_id}`}
          </Typography>
          {items.map((item, index) => {
            const line = lineFor(item.item_id);
            return (
              <Box key={item.item_id}>
                {index > 0 && <Divider />}
                <CartLine
                  item={item}
                  categoryId={category.category_id}
                  quantity={order.quantities[category.category_id]?.[item.item_id] || 0}
                  onQuantityChange={handleQuantityChange}
                  atLimit={order.atLimit(category.category_id, item.item_id)}
                  extra={line?.extra ?? 0}
                  extraPrice={line?.extraPrice ?? 0}
                  nameComponent="h4"
                />
              </Box>
            );
          })}
        </Box>
      );
    });
  };

  return (
    <AppDialog open={open} onClose={handleClose} maxWidth="md">
      <DialogHeader
        title={bill ? `New Order · ${bill.bill_Name}` : "New Order"}
        subtitle={session && `Dining Session #${session.session_Id} · ${tableLabel(session.table_Numbers)}`}
        onClose={handleClose}
        closeDisabled={isSaving}
      />

      <DialogContent dividers sx={{ px: 0, pt: 2 }}>
        {!session ? (
          <Alert severity="info" sx={{ mx: 3 }}>
            This dining session has ended.
          </Alert>
        ) : (
          <>
            <Box sx={{ px: 3, pb: 2 }}>
              <TextField
                type="search"
                label="Search the menu"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                size="small"
                fullWidth
              />
            </Box>

            {!canOrder && (
              <Alert severity="warning" sx={{ mx: 3, mb: 2 }}>
                This bill is closed, so no more orders can be added to it.
              </Alert>
            )}
            {canOrder && bill.requested_At && (
              <Alert severity="info" sx={{ mx: 3, mb: 2 }}>
                The guests asked for this bill. Staff can still add to it.
              </Alert>
            )}
            {error && (
              <Alert severity="error" sx={{ mx: 3, mb: 2 }}>
                {error}
              </Alert>
            )}

            {renderMenu()}
          </>
        )}
      </DialogContent>

      <DialogActions sx={{ flexWrap: "wrap" }}>
        <Typography variant="body2" color="text.secondary" role="status" sx={{ mr: "auto" }}>
          {order.unitCount === 0
            ? "No items picked"
            : `${order.unitCount} ${order.unitCount === 1 ? "item" : "items"}` +
              (order.subtotal > 0 ? ` · ${money(order.subtotal)} extra` : "")}
        </Typography>
        <Button onClick={handleClose} disabled={isSaving}>
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={handlePlaceOrder}
          disabled={!canOrder || order.unitCount === 0 || isSaving}
        >
          {isSaving ? "Placing…" : "Place Order"}
        </Button>
      </DialogActions>
    </AppDialog>
  );
};

export default StaffOrderDialog;
