"use client";
import { useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Divider,
  IconButton,
  Paper,
  Typography,
} from "@mui/material";
import CartLine from "@/components/menu/CartLine";
import BillChoiceDialog from "@/components/menu/BillChoiceDialog";
import { cartSubtotal, orderItemsFor } from "@/components/menu/cartRules";
import { MenuListSkeleton } from "@/components/menu/MenuItemCardSkeleton";
import CustomerPageContainer from "@/components/customer/CustomerPageContainer";
import MaterialSymbol from "@/components/common/MaterialSymbol";
import api from "@/config/api";
import { useOrderActions } from "@/hooks/useOrderActions";
import useAutoRefresh from "@/hooks/useAutoRefresh";
import { NOT_SEATED_MESSAGE } from "@/services/tableSession";
import {
  useTableOrder,
  BILL_REQUESTED_MESSAGE,
  MENU_PATH,
} from "@/contexts/TableOrderContext";

const ORDERS_PATH = "/dashboard/orders";
// The bill reminder, button and padding
const ACTION_BAR_HEIGHT = 112;

const plural = (count, word) => `${count} ${word}${count === 1 ? "" : "s"}`;
const billName = (bill) => bill.bill_name || `Bill #${bill.bill_id}`;
const guestCount = (count) => `${count} guest${count === 1 ? "" : "s"}`;

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
    myBill,
    selectedBillId,
    needsBill,
    billChangeBlocked,
    billRequested,
    changeQuantity,
    limitStatus,
    clearCart,
    cartLines,
    cartCount,
    reviewOpenedFromMenuRef,
  } = useTableOrder();

  const [orderError, setOrderError] = useState(null);
  // The items just sent, for the confirmation screen
  const [placedItems, setPlacedItems] = useState(null);
  // Lines the server charged for going over a limit (e.g. extra Hot Stones), by item
  const [placedExtras, setPlacedExtras] = useState([]);
  const sending = useRef([]);

  // Re-check the table and bills (staff may have opened one since the menu loaded)
  useEffect(() => {
    refresh();
  }, [refresh]);

  const backToMenu = () => {
    // Real Back puts the guest exactly where they were on the menu
    if (reviewOpenedFromMenuRef.current) {
      reviewOpenedFromMenuRef.current = false;
      router.back();
    } else {
      router.push(MENU_PATH);
    }
  };

  const handleOrderSuccess = () => {
    setPlacedItems(sending.current);
    clearCart();
    refresh();
    window.scrollTo(0, 0);
  };

  const {
    createOrder,
    actionError,
    isLoading,
    clearActionError,
  } = useOrderActions(handleOrderSuccess);

  // Picks up a bill staff just opened, or limits used up by others at the table.
  // Paused while the order is being sent, and on the confirmation screen
  useAutoRefresh(refresh, { enabled: !isLoading && !placedItems });

  // Guests not on a bill yet pick or start one before they can order
  const [billDialogOpen, setBillDialogOpen] = useState(false);
  // Which step it opens on: choosing a bill, or the guest count on theirs
  const [billDialogView, setBillDialogView] = useState("choose");
  const openBillDialog = (view = "choose") => {
    setBillDialogView(view);
    setBillDialogOpen(true);
  };
  const askedForBill = useRef(false);
  useEffect(() => {
    if (needsBill && !askedForBill.current) {
      askedForBill.current = true;
      setBillDialogOpen(true);
    }
  }, [needsBill]);

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
  const unitCount = cartLines.reduce(
    (total, { quantity }) => total + quantity,
    0,
  );
  // Add-ons, plus anything past a limit that charges (e.g. extra Hot Stones)
  const subtotal = cartSubtotal(cartLines);
  const hasExtras = cartLines.some(({ extra }) => extra > 0);

  const handlePlaceOrder = async () => {
    if (!selectedBillId) {
      setOrderError(
        "There's no open bill for your table yet. Ask your server to open one.",
      );
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
    setPlacedExtras([]);

    const orderItems = orderItemsFor(cartLines, menuId);
    // What the "Order sent!" screen lists
    sending.current = cartLines.map(({ item, quantity }) => ({
      item_id: item.item_id,
      name: item.name,
      quantity,
    }));

    try {
      // The cart can't go over a limit (+ stops there, and Place order is disabled when it is)
      // so the server should accept these items, it still checks every order.

      // The order and its items are saved together, so a failure leaves no empty order behind
      const order = await createOrder(sessionId, selectedBillId, orderItems);
      if (!order)
        throw new Error("Couldn't place your order. Please try again.");

      setPlacedExtras(
        (order.orderItems ?? [])
          .filter((line) => line.charge_reason === "OverLimit")
          .map((line) => ({
            itemId: line.item_id,
            quantity: line.quantity,
            priceAtTime: line.price_at_time,
          })),
      );
    } catch (err) {
      console.error("Error processing order:", err);
      setOrderError(
        err.message || "Couldn't place your order. Please try again.",
      );
    }
  };

  // ---- Order sent ----
  if (placedItems) {
    const extrasTotal = placedExtras.reduce(
      (total, line) => total + line.quantity * line.priceAtTime,
      0,
    );
    return (
      <CustomerPageContainer sx={{ maxWidth: 640 }}>
        <Box sx={{ textAlign: "center", pt: { xs: 3, sm: 5 } }}>
          <MaterialSymbol
            name="check_circle"
            filled
            size={72}
            sx={{ color: "success.main" }}
          />
          <Typography
            variant="h5"
            component="h1"
            sx={{ mt: 1.5, fontWeight: 700 }}
          >
            Order sent!
          </Typography>
          <Typography color="text.secondary" sx={{ mt: 1 }}>
            The kitchen has your order. You can follow it on Orders.
          </Typography>
        </Box>

        {extrasTotal > 0 && (
          <Alert severity="info" sx={{ mt: 3 }}>
            Some items were over your table&apos;s limit. $
            {extrasTotal.toFixed(2)} in extras will be added to your bill.
          </Alert>
        )}

        <Paper
          variant="outlined"
          sx={{ mt: 3, borderRadius: 3, overflow: "hidden" }}
        >
          {placedItems.map(({ item_id, name, quantity }, index) => {
            const extra = placedExtras.find((line) => line.itemId === item_id);
            return (
              <Box key={item_id}>
                {index > 0 && <Divider />}
                <Box sx={{ display: "flex", gap: 1.5, px: 2, py: 1.5 }}>
                  <Typography sx={{ fontWeight: 700, minWidth: 28 }}>
                    {quantity}×
                  </Typography>
                  <Box sx={{ flex: 1, minWidth: 0 }}>
                    <Typography>{name}</Typography>
                    {extra && (
                      <Typography variant="body2" color="text.secondary">
                        {extra.quantity} extra × $
                        {Number(extra.priceAtTime).toFixed(2)}
                      </Typography>
                    )}
                  </Box>
                </Box>
              </Box>
            );
          })}
        </Paper>

        <Box sx={{ display: "flex", flexDirection: "column", gap: 1.5, mt: 3 }}>
          <Button
            variant="contained"
            size="large"
            onClick={() => router.push(ORDERS_PATH)}
          >
            Track my order
          </Button>
          <Button
            variant="outlined"
            size="large"
            onClick={() => router.push(MENU_PATH)}
          >
            Order more
          </Button>
        </Box>
      </CustomerPageContainer>
    );
  }

  // The table's limits can drop after this cart was filled (e.g. someone else at the table ordered)
  const overLimit = cartLines.filter(
    ({ category, item }) =>
      limitStatus(category.category_id, item.item_id).overLimit,
  );
  const canPlaceOrder =
    !isLoading &&
    !!selectedBillId &&
    !billRequested &&
    itemCount > 0 &&
    !!menuId &&
    overLimit.length === 0;
  const shownError =
    orderError ||
    actionError ||
    (overLimit.length > 0
      ? `Too many ${overLimit.map(({ item }) => item.name).join(", ")} for your table's limit. Remove some to place your order.`
      : null);
  const selectedBill = selectedBillId ? myBill : null;
  // Another bill to move to, or a new one to start, while they haven't ordered on this one
  const canChangeBill = !billChangeBlocked && !billRequested;
  // Cart items need the menu to show; the saved cart may arrive before it
  const menuLoading = !!sessionId && !itemsLoaded && cartCount > 0;

  return (
    <CustomerPageContainer sx={{ maxWidth: 720 }}>
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          gap: 1,
          mt: { xs: 3, sm: 2 },
          mb: 3,
        }}
      >
        <IconButton
          aria-label="Back to menu"
          onClick={backToMenu}
          sx={{ ml: -1 }}
        >
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
          <MaterialSymbol
            name="shopping_bag"
            size={56}
            sx={{ color: "text.disabled" }}
          />
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
          {needsBill && (
            <Alert
              severity="warning"
              sx={{ mb: 3 }}
              action={
                <Button color="inherit" size="small" onClick={() => openBillDialog()}>
                  Choose bill
                </Button>
              }
            >
              Choose your bill to place this order.
            </Alert>
          )}
          {myBill && !selectedBillId && (
            <Alert severity="info" sx={{ mb: 3 }}>
              {billName(myBill)} is {String(myBill.status).toLowerCase()}, so no
              more orders can be added. Ask your server if you&apos;d like
              anything else.
            </Alert>
          )}

          {/* ---- Bill: first thing on the page so nobody orders onto the wrong one ---- */}
          {selectedBill && (
            <Paper variant="outlined" sx={{ p: 2, mb: 4, borderRadius: 3 }}>
              {/* Which bill, with Change kept to the right however long the name is */}
              <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
                <Box
                  sx={{
                    width: 44,
                    height: 44,
                    flexShrink: 0,
                    borderRadius: 2,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    bgcolor: "action.hover",
                  }}
                >
                  <MaterialSymbol name="receipt_long" size={26} sx={{ color: "primary.main" }} />
                </Box>
                <Box sx={{ minWidth: 0, flex: 1 }}>
                  <Typography variant="body2" color="text.secondary">
                    Your bill
                  </Typography>
                  <Typography
                    variant="h6"
                    component="p"
                    sx={{ fontWeight: 700, lineHeight: 1.3, overflowWrap: "anywhere" }}
                  >
                    {billName(selectedBill)}
                  </Typography>
                </Box>
                {canChangeBill && (
                  <Button
                    variant="outlined"
                    size="small"
                    onClick={() => openBillDialog()}
                    aria-label={`Change bill from ${billName(selectedBill)}`}
                    sx={{ flexShrink: 0, borderRadius: 999, px: 2 }}
                  >
                    Change
                  </Button>
                )}
              </Box>

              <Divider sx={{ my: 1.5 }} />

              {/* Party size, which they can fix until they ask for the bill or staff lock it */}
              <Box sx={{ display: "flex", alignItems: "center", gap: 1, minHeight: 36 }}>
                <MaterialSymbol
                  name={selectedBill.counts_locked ? "lock" : "group"}
                  size={20}
                  sx={{ color: "text.secondary", flexShrink: 0 }}
                />
                <Box sx={{ flex: 1, minWidth: 0 }}>
                  <Typography variant="body2" component="span" sx={{ fontWeight: 600 }}>
                    {guestCount(selectedBill.total_count ?? 0)}
                  </Typography>{" "}
                  {selectedBill.counts_locked && (
                    <Typography variant="body2" component="span" color="text.secondary">
                      · confirmed by your server
                    </Typography>
                  )}
                </Box>
                {!selectedBill.counts_locked && !billRequested && (
                  <Button
                    size="small"
                    onClick={() => openBillDialog("guests")}
                    aria-label={`Edit guests on ${billName(selectedBill)}`}
                    sx={{ flexShrink: 0 }}
                  >
                    Edit
                  </Button>
                )}
              </Box>

              {/* Why there's no Change button: they ordered on it, or staff locked it */}
              {billChangeBlocked && !billRequested && (
                <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
                  {billChangeBlocked}
                </Typography>
              )}
            </Paper>
          )}

          {/* ---- Items ---- */}
          <Box
            sx={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              mb: 2,
            }}
          >
            <Typography variant="h6" component="h2" sx={{ fontWeight: 700 }}>
              Your items
            </Typography>
            <Button
              size="small"
              onClick={backToMenu}
              sx={{ minHeight: 0, py: 0.5 }}
            >
              Add more
            </Button>
          </Box>
          <Paper
            variant="outlined"
            sx={{ borderRadius: 3, overflow: "hidden" }}
          >
            {cartLines.map(
              ({ category, item, quantity, extra, extraPrice }, index) => (
                <Box key={`${category.category_id}-${item.item_id}`}>
                  {index > 0 && <Divider />}
                  <CartLine
                    item={item}
                    categoryId={category.category_id}
                    quantity={quantity}
                    extra={extra}
                    extraPrice={extraPrice}
                    onQuantityChange={handleQuantityChange}
                    atLimit={
                      limitStatus(category.category_id, item.item_id).atLimit
                    }
                  />
                </Box>
              ),
            )}
          </Paper>

          {/* ---- Summary ---- */}
          <Paper variant="outlined" sx={{ mt: 4, p: 2.5, borderRadius: 3 }}>
            <Typography color="text.secondary">
              {/* Counted the same way as the Place order button */}
              {plural(unitCount, "item")} in your order
            </Typography>
            <Divider sx={{ my: 2 }} />
            <Box sx={{ display: "flex", justifyContent: "space-between" }}>
              <Typography sx={{ fontWeight: 700 }}>
                {hasExtras ? "Add-ons and extras" : "Add-ons subtotal"}
              </Typography>
              <Typography sx={{ fontWeight: 700 }}>
                ${subtotal.toFixed(2)}
              </Typography>
            </Box>
            <Typography
              variant="caption"
              color="text.secondary"
              sx={{ display: "block", mt: 1.5, lineHeight: 1.5 }}
            >
              All-you-can-eat items are included. Your total and taxes are on
              your bill summary.
            </Typography>
            {/* The bill page says this too, but by then the food has been ordered */}
            <Typography
              variant="caption"
              color="text.secondary"
              sx={{ display: "block", mt: 1, lineHeight: 1.5 }}
            >
              Please order only what you can finish.
              <br />
              Additional leftover fees may apply.
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
                <Alert
                  severity="error"
                  sx={{ mb: 1.5 }}
                  onClose={() => {
                    setOrderError(null);
                    clearActionError();
                  }}
                >
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
                  <CircularProgress
                    size={24}
                    color="inherit"
                    aria-label="Placing order"
                  />
                ) : (
                  `Place order · ${plural(unitCount, "item")}`
                )}
              </Button>
            </Box>
          </Paper>
        </>
      )}
      <BillChoiceDialog
        open={billDialogOpen}
        initialView={billDialogView}
        onClose={() => setBillDialogOpen(false)}
      />
    </CustomerPageContainer>
  );
};

export default ReviewOrderPage;
