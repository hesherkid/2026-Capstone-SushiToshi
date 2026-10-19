import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Drawer,
  FormControl,
  IconButton,
  InputLabel,
  MenuItem,
  Select,
  Typography,
} from "@mui/material";
import { X } from "lucide-react";
import MenuItemCard from "@/components/menu/MenuItemCard";

const CartDrawer = ({
  open,
  onClose,
  categories,
  quantities,
  menuItems,
  onQuantityChange,
  onConfirmOrder,
  isLoading,
  selectedBillId,
  // The table's open bills ({ bill_id, bill_name, requested_at }); the orderer picks one
  bills = [],
  onBillChange,
  // True after the guests ask for their bill: the cart stays visible but can't be ordered
  orderingLocked = false,
  lockedMessage,
  itemCount,
  totalQuantity,
}) => {
  const selectedItems = categories.flatMap((category) =>
    Object.entries(quantities[category.category_id] || [])
      .filter(([, quantity]) => quantity > 0)
      .map(([itemId, quantity]) => ({
        category,
        quantity,
        item: menuItems[category.category_id]?.find(
          (menuItem) => menuItem.item_id === Number(itemId)
        ),
      }))
      .filter(({ item }) => item)
  );
  const subtotal = selectedItems.reduce(
    (total, { item, quantity }) =>
      total + (item.is_add_on ? (Number(item.price) || 0) * quantity : 0),
    0
  );

  return (
  <Drawer 
    anchor="right" 
    open={open} 
    onClose={onClose}
    sx={{
      "& .MuiDrawer-paper": {
        top: { xs: 70, sm: 64 },
        height: { xs: "calc(100% - 70px)", sm: "calc(100% - 64px)" },
      },
    }}
    >
    <Box
      sx={{
        width: { xs: "100mv", sm: 420 },
        height: "100%",
        display: "flex",
        flexDirection: "column",
      }}
    >
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          px: 2.5,
          py: 2,
          borderBottom: 1,
          borderColor: "divider",
        }}
      >
        <Typography id="cart-drawer-title" component="h2" variant="h6">
          Your Order
        </Typography>
        <IconButton aria-label="Close cart" onClick={onClose}>
          <X size={20} />
        </IconButton>
      </Box>

      <Box sx={{ flex: 1, overflowY: "auto" }}>
        {orderingLocked && (
          <Alert severity="info" sx={{ m: 2 }}>
            {lockedMessage}
          </Alert>
        )}
        {!selectedBillId && (
          <Alert severity="warning" sx={{ m: 2 }}>
            There is no open bill for this session yet. Ask a staff member to open one before placing an order.
          </Alert>
        )}
        {selectedItems.length > 0 ? (
          <Box sx={{ display: "flex", flexDirection: "column", py: 1 }}>
            {selectedItems.map(({ category, item, quantity }) => (
              <MenuItemCard
                key={`${category.category_id}-${item.item_id}`}
                item={item}
                categoryId={category.category_id}
                quantity={quantity}
                showQuantityControls={false}
                onQuantityChange={onQuantityChange}
              />
            ))}
          </Box>
        ) : (
          <Typography sx={{ p: 3 }} color="text.secondary">
            Your bag is empty.
          </Typography>
        )}
      </Box>

      <Box sx={{ p: 2.5, borderTop: 1, borderColor: "divider" }}>
        {bills.length > 1 && (
          <FormControl fullWidth size="small" sx={{ mb: 2 }}>
            <InputLabel id="order-bill-label">Put this order on</InputLabel>
            <Select
              labelId="order-bill-label"
              label="Put this order on"
              value={selectedBillId || ""}
              onChange={(event) => onBillChange?.(Number(event.target.value))}
            >
              {bills.map((bill) => (
                // Bills the guests have asked for can't take more orders
                <MenuItem key={bill.bill_id} value={bill.bill_id} disabled={!!bill.requested_at}>
                  {bill.bill_name || `Bill #${bill.bill_id}`}
                  {bill.requested_at ? " (bill requested)" : ""}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        )}
        {bills.length === 1 && (
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
            Ordering on {bills[0].bill_name || `Bill #${bills[0].bill_id}`}
          </Typography>
        )}
        <Box
          sx={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            mb: 1.5,
          }}
        >
          <Typography variant="subtitle1" fontWeight={700}>
            Subtotal
          </Typography>

          <Typography variant="subtitle1" fontWeight={700}>
            ${subtotal.toFixed(2)}
          </Typography>
        </Box>
        <Typography variant="subtitle2" fontWeight={500}
        sx={{
               fontSize: "0.65rem",
               pb:3,
              }}>
            Total and taxes will be calculated on bill. Subtotal is for add-ons only.
          </Typography>
        <Button
          variant="contained"
          fullWidth
          onClick={onConfirmOrder}
          disabled={isLoading || !selectedBillId || orderingLocked || itemCount === 0}
        >
          {isLoading ? (
            <CircularProgress size={24} color="inherit" />
          ) : (
            `Place Order (${itemCount} ${itemCount === 1 ? "item" : "items"}, ${totalQuantity} ${totalQuantity === 1 ? "unit" : "units"})`
          )}
        </Button>
      </Box>
    </Box>
  </Drawer>
  );
};

export default CartDrawer;