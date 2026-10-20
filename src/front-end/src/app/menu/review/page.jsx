"use client";
import { useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Divider,
  FormControl,
  IconButton,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Typography,
} from "@mui/material";
import CartLine from "@/components/menu/CartLine";
import { MenuListSkeleton } from "@/components/menu/MenuItemCardSkeleton";
import CustomerPageContainer from "@/components/customer/CustomerPageContainer";
import MaterialSymbol from "@/components/common/MaterialSymbol";
import api from "@/config/api";
import { useOrderActions } from "@/hooks/useOrderActions";
import { NOT_SEATED_MESSAGE } from "@/services/tableSession";
import {
  useTableOrder,
  BILL_REQUESTED_MESSAGE,
  MENU_PATH,
} from "@/contexts/TableOrderContext";

const ORDERS_PATH = "/dashboard/orders";
// Order created but its items not added yet (e.g. a limit was hit); reused on the next try
const PENDING_ORDER_KEY = "pendingOrder";
// The bill reminder, button and padding
const ACTION_BAR_HEIGHT = 112;

const plural = (count, word) => `${count} ${word}${count === 1 ? "" : "s"}`;
const billName = (bill) => bill.bill_name || `Bill #${bill.bill_id}`;

// The guest's cart as a full page: check the items, pick the bill, place the order,
// then a confirmation with where to go next
const ReviewOrderPage = () => {
  const router = useRouter();
  const {
    sessionId,
    sessionChecked,
    error,
    refresh,
    itemsLoaded,
    menuId,
    openBills,
    selectedBillId,
    chooseBill,
    billRequested,
    changeQuantity,
    clearCart,
    cartLines,
    cartCount,
    reviewOpenedFromMenu,
  } = useTableOrder();

  const [orderError, setOrderError] = useState(null);
  // The items just sent, for the confirmation screen
  const [placedItems, setPlacedItems] = useState(null);
  const sending = useRef([]);
  const [pendingOrderId, setPendingOrderIdState] = useState(null);

  // Re-check the table and bills (staff may have opened one since the menu loaded)
  useEffect(() => {
    refresh();
  }, [refresh]);

  const backToMenu = () => {
    // Real Back puts the guest exactly where they were on the menu
    if (reviewOpenedFromMenu.current) {
      reviewOpenedFromMenu.current = false;
      router.back();
    } else {
      router.push(MENU_PATH);
    }
  };

  const setPendingOrderId = (orderId) => {
    setPendingOrderIdState(orderId);
    if (orderId) {
      localStorage.setItem(
        PENDING_ORDER_KEY,
        JSON.stringify({ orderId, sessionId, billId: selectedBillId }),
      );
    } else {
      localStorage.removeItem(PENDING_ORDER_KEY);
    }
  };

  // Restore it once the session and bill are known
  useEffect(() => {
    if (!sessionId || !selectedBillId) return;
    try {
      const saved = JSON.parse(localStorage.getItem(PENDING_ORDER_KEY) || "null");
      if (
        saved?.orderId &&
        String(saved.sessionId) === String(sessionId) &&
        String(saved.billId) === String(selectedBillId)
      ) {
        setPendingOrderIdState(Number(saved.orderId));
      } else {
        localStorage.removeItem(PENDING_ORDER_KEY);
        setPendingOrderIdState(null);
      }
    } catch {
      localStorage.removeItem(PENDING_ORDER_KEY);
      setPendingOrderIdState(null);
    }
  }, [sessionId, selectedBillId]);

  const handleOrderSuccess = () => {
    setPlacedItems(sending.current);
    clearCart();
    setPendingOrderId(null);
    window.scrollTo(0, 0);
  };

  const { createOrder, addOrderItems, actionError, isLoading, clearActionError } =
    useOrderActions(handleOrderSuccess);

  // A changed cart may fix the last error (e.g. over a limit), so clear it
  const handleQuantityChange = useCallback(
    (categoryId, itemId, delta) => {
      changeQuantity(categoryId, itemId, delta);
      setOrderError(null);
      clearActionError();
    },
    [changeQuantity, clearActionError],
  );

  const itemCount = cartLines.length;
  const unitCount = cartLines.reduce((total, { quantity }) => total + quantity, 0);
  const subtotal = cartLines.reduce(
    (total, { item, quantity }) =>
      total + (item.is_add_on ? (Number(item.price) || 0) * quantity : 0),
    0,
  );

  const handlePlaceOrder = async () => {
    if (!selectedBillId) {
      setOrderError("There's no open bill for your table yet. Ask your server to open one.");
      return;
    }
    if (!sessionId || !menuId) {
      setOrderError("The menu is still loading. Please try again in a moment.");
      return;
    }
    if (billRequested) {
      setOrderError(BILL_REQUESTED_MESSAGE);
      return;
    }
    if (itemCount === 0) {
      setOrderError("Add at least one item first.");
      return;
    }

    setOrderError(null);
    clearActionError();

    const orderItems = cartLines.map(({ item, quantity }) => ({
      menu_id: Number(menuId),
      item_id: Number(item.item_id),
      quantity: Number(quantity),
      price_at_time: item.is_add_on ? Number(item.price) || 0 : 0,
      // Not stored; names the items in the "added to order" notification
      name: item.name,
    }));
    sending.current = orderItems.map(({ item_id, name, quantity }) => ({ item_id, name, quantity }));

    try {
      let orderId = pendingOrderId;

      // The saved order may have been cancelled, approved, or filled since; only reuse it
      // while it still exists, is pending, and has no items
      if (orderId) {
        const existing = await api
          .get(`/Order/${orderId}`)
          .then((res) => res.data)
          .catch(() => null);
        const reusable =
          existing &&
          String(existing.status).toLowerCase() === "pending" &&
          (existing.items?.length ?? 0) === 0;
        if (!reusable) {
          setPendingOrderId(null);
          orderId = null;
        }
      }

      if (!orderId) {
        const orderResponse = await createOrder(sessionId, selectedBillId);
        if (!orderResponse) throw new Error("Couldn't start your order. Please try again.");
        orderId = Number(orderResponse.order_Id);
        setPendingOrderId(orderId);
      }

      await addOrderItems(orderId, orderItems);
    } catch (err) {
      console.error("Error processing order:", err);
      setOrderError(err.message || "Couldn't place your order. Please try again.");
    }
  };

  // ---- Order sent ----
  if (placedItems) {
    return (
      <CustomerPageContainer sx={{ maxWidth: 640 }}>
        <Box sx={{ textAlign: "center", pt: { xs: 3, sm: 5 } }}>
          <MaterialSymbol name="check_circle" filled size={72} sx={{ color: "success.main" }} />
          <Typography variant="h5" component="h1" sx={{ mt: 1.5, fontWeight: 700 }}>
            Order sent!
          </Typography>
          <Typography color="text.secondary" sx={{ mt: 1 }}>
            The kitchen has your order. You can follow it on Orders.
          </Typography>
        </Box>

        <Paper variant="outlined" sx={{ mt: 3, borderRadius: 3, overflow: "hidden" }}>
          {placedItems.map(({ item_id, name, quantity }, index) => (
            <Box key={item_id}>
              {index > 0 && <Divider />}
              <Box sx={{ display: "flex", gap: 1.5, px: 2, py: 1.5 }}>
                <Typography sx={{ fontWeight: 700, minWidth: 28 }}>{quantity}×</Typography>
                <Typography>{name}</Typography>
              </Box>
            </Box>
          ))}
        </Paper>

        <Box sx={{ display: "flex", flexDirection: "column", gap: 1.5, mt: 3 }}>
          <Button variant="contained" size="large" onClick={() => router.push(ORDERS_PATH)}>
            Track my order
          </Button>
          <Button variant="outlined" size="large" onClick={() => router.push(MENU_PATH)}>
            Order more
          </Button>
        </Box>
      </CustomerPageContainer>
    );
  }

  const canPlaceOrder =
    !isLoading && !!selectedBillId && !billRequested && itemCount > 0 && !!menuId;
  const shownError = orderError || actionError;
  const multipleBills = openBills.length > 1;
  const selectedBill = openBills.find((bill) => bill.bill_id === selectedBillId);
  // Cart items need the menu to show; the saved cart may arrive before it
  const menuLoading = !!sessionId && !itemsLoaded && cartCount > 0;

  return (
    <CustomerPageContainer sx={{ maxWidth: 720 }}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mt: { xs: 3, sm: 2 }, mb: 3 }}>
        <IconButton aria-label="Back to menu" onClick={backToMenu} sx={{ ml: -1 }}>
          <MaterialSymbol name="arrow_back" />
        </IconButton>
        <Typography variant="h5" component="h1" sx={{ fontWeight: 700 }}>
          Review order
        </Typography>
      </Box>

      {!sessionChecked || menuLoading ? (
        <MenuListSkeleton />
      ) : !sessionId ? (
        <Alert severity="info">{error || NOT_SEATED_MESSAGE}</Alert>
      ) : itemCount === 0 ? (
        // ---- Empty bag ----
        <Box sx={{ textAlign: "center", py: 6 }}>
          <MaterialSymbol name="shopping_bag" size={56} sx={{ color: "text.disabled" }} />
          <Typography variant="h6" component="h2" sx={{ mt: 1.5 }}>
            Your bag is empty
          </Typography>
          <Typography color="text.secondary" sx={{ mt: 0.5, mb: 3 }}>
            Add something from the menu and it will show up here.
          </Typography>
          <Button variant="contained" onClick={backToMenu}>
            Browse the menu
          </Button>
        </Box>
      ) : (
        <>
          {billRequested && (
            <Alert severity="info" sx={{ mb: 3 }}>
              {BILL_REQUESTED_MESSAGE}
            </Alert>
          )}
          {!selectedBillId && (
            <Alert
              severity="warning"
              sx={{ mb: 3 }}
              action={
                <Button color="inherit" size="small" onClick={refresh}>
                  Check again
                </Button>
              }
            >
              Your table doesn&apos;t have an open bill yet. Ask your server to open one, then
              check again.
            </Alert>
          )}

          {/* ---- Bill: first thing on the page so nobody orders onto the wrong one ---- */}
          {openBills.length > 0 && (
            <Paper
              variant="outlined"
              sx={{
                p: 2.5,
                mb: 4,
                borderRadius: 3,
                ...(multipleBills && { borderColor: "primary.main", borderWidth: 2 }),
              }}
            >
              <Box sx={{ display: "flex", alignItems: "center", gap: 2 }}>
                <MaterialSymbol name="receipt_long" size={32} sx={{ color: "primary.main" }} />
                <Box sx={{ minWidth: 0 }}>

                  {multipleBills ? (
                    <Typography variant="body2" color="text.secondary">
                      Your table has {openBills.length} bills. Select your bill.
                    </Typography>
                  ) : (
                    <Typography variant="h6" component="p" sx={{ fontWeight: 700, lineHeight: 1.3 }}>
                      {billName(openBills[0])}
                    </Typography>
                  )}
                </Box>
              </Box>

              {multipleBills && (
                <FormControl fullWidth sx={{ mt: 2.5 }}>
                  <InputLabel id="order-bill-label">Put this order on</InputLabel>
                  <Select
                    labelId="order-bill-label"
                    label="Put this order on"
                    value={selectedBillId || ""}
                    onChange={(event) => chooseBill(Number(event.target.value))}
                    sx={{ fontWeight: 600 }}
                  >
                    {openBills.map((bill) => (
                      // Bills the guests have asked for can't take more orders
                      <MenuItem key={bill.bill_id} value={bill.bill_id} disabled={!!bill.requested_at}>
                        {billName(bill)}
                        {bill.requested_at ? " (bill requested)" : ""}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            </Paper>
          )}

          {/* ---- Items ---- */}
          <Box
            sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}
          >
            <Typography variant="h6" component="h2" sx={{ fontWeight: 700 }}>
              Your items
            </Typography>
            <Button size="small" onClick={backToMenu} sx={{ minHeight: 0, py: 0.5 }}>
              Add more
            </Button>
          </Box>
          <Paper variant="outlined" sx={{ borderRadius: 3, overflow: "hidden" }}>
            {cartLines.map(({ category, item, quantity }, index) => (
              <Box key={`${category.category_id}-${item.item_id}`}>
                {index > 0 && <Divider />}
                <CartLine
                  item={item}
                  categoryId={category.category_id}
                  quantity={quantity}
                  onQuantityChange={handleQuantityChange}
                />
              </Box>
            ))}
          </Paper>

          {/* ---- Summary ---- */}
          <Paper variant="outlined" sx={{ mt: 4, p: 2.5, borderRadius: 3 }}>
            <Typography color="text.secondary">
              {plural(itemCount, "item")} · {plural(unitCount, "unit")}
            </Typography>
            <Divider sx={{ my: 2 }} />
            <Box sx={{ display: "flex", justifyContent: "space-between" }}>
              <Typography sx={{ fontWeight: 700 }}>Add-ons subtotal</Typography>
              <Typography sx={{ fontWeight: 700 }}>${subtotal.toFixed(2)}</Typography>
            </Box>
            <Typography
              variant="caption"
              color="text.secondary"
              sx={{ display: "block", mt: 1.5, lineHeight: 1.5 }}
            >
              All-you-can-eat items are included. Your total and taxes are on your bill summary.
            </Typography>
          </Paper>

          {/* Room so the action bar never covers the summary */}
          <Box sx={{ height: ACTION_BAR_HEIGHT + 24 }} />

          {/* ---- Place order: always in reach at the bottom (the tab bar is hidden on this page) ---- */}
          <Paper
            elevation={0}
            square
            sx={{
              position: "fixed",
              left: 0,
              right: 0,
              bottom: 0,
              zIndex: (theme) => theme.zIndex.appBar,
              borderTop: "1px solid rgba(0, 0, 0, 0.08)",
              boxShadow: "0 -2px 12px rgba(0, 0, 0, 0.06)",
              px: 2,
              pt: 1.5,
              pb: "calc(16px + env(safe-area-inset-bottom))",
            }}
          >
            <Box sx={{ maxWidth: 720, mx: "auto" }}>
              {shownError && (
                <Alert severity="error" sx={{ mb: 1.5 }} onClose={() => {
                  setOrderError(null);
                  clearActionError();
                }}>
                  {shownError}
                </Alert>
              )}
              {selectedBill && (
                <Typography
                  variant="body2"
                  color="text.secondary"
                  sx={{ textAlign: "center", mb: 1 }}
                >
                  Order on <strong>{billName(selectedBill)}</strong>
                </Typography>
              )}
              <Button
                variant="contained"
                fullWidth
                size="large"
                onClick={handlePlaceOrder}
                disabled={!canPlaceOrder}
                sx={{ borderRadius: 999, minHeight: 52 }}
              >
                {isLoading ? (
                  <CircularProgress size={24} color="inherit" aria-label="Placing order" />
                ) : (
                  `Place order · ${plural(unitCount, "item")}`
                )}
              </Button>
            </Box>
          </Paper>
        </>
      )}
    </CustomerPageContainer>
  );
};

export default ReviewOrderPage;
