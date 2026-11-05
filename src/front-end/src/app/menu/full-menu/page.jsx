"use client";
import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { Typography, Box, Alert, Button } from "@mui/material";
import MenuItemCard from "@/components/menu/MenuItemCard";
import MenuBrowser from "@/components/menu/MenuBrowser";
import useMyItems from "@/components/menu/useMyItems";
import ReviewOrderButton from "@/components/menu/ReviewOrderButton";
import BillChoiceDialog from "@/components/menu/BillChoiceDialog";
import TrackedItem from "@/components/menu/TrackedItem";
import CustomerPageContainer from "@/components/customer/CustomerPageContainer";
import useAutoRefresh from "@/hooks/useAutoRefresh";
import { NOT_SEATED_MESSAGE } from "@/services/tableSession";
import {
  useTableOrder,
  BILL_REQUESTED_MESSAGE,
  REVIEW_ORDER_PATH,
} from "@/contexts/TableOrderContext";

// Browsing the menu before being seated, for customers with an account
const BROWSE_PATH = "/menu/browse";

// Browsing only: the cart and ordering live on the Review order page (/menu/review).
// Session, menu, bills and cart come from TableOrderContext, so they survive going there and back
const FullMenu = () => {
  const router = useRouter();
  const {
    sessionId,
    sessionChecked,
    error,
    setError,
    refresh,
    categories,
    menuItems,
    itemsLoaded,
    menuId,
    selectedBillId,
    myBill,
    needsBill,
    billRequested,
    quantities,
    changeQuantity,
    limitStatus,
    cartCount,
    menuScrollYRef,
    reviewOpenedFromMenuRef,
  } = useTableOrder();

  // Re-check the table and bills each visit; the menu already loaded shows meanwhile
  useEffect(() => {
    refresh();
  }, [refresh]);

  // Keep bills and limits current while the guest browses (others at the table order too)
  useAutoRefresh(refresh);

  // Guests not on a bill yet are asked which one is theirs as soon as the menu opens
  const [billDialogOpen, setBillDialogOpen] = useState(false);
  const askedForBill = useRef(false);
  useEffect(() => {
    if (needsBill && !askedForBill.current) {
      askedForBill.current = true;
      setBillDialogOpen(true);
    }
  }, [needsBill]);

  // Put the guest back where they were on the menu after Review order.
  // Read once on arrival, before scrolling to the top of this page can overwrite it
  const returnToY = useRef(0);
  useEffect(() => {
    returnToY.current = menuScrollYRef.current;
  }, []);
  const scrollRestored = useRef(false);
  useEffect(() => {
    if (scrollRestored.current || !itemsLoaded) return;
    scrollRestored.current = true;
    if (returnToY.current > 0) {
      requestAnimationFrame(() => window.scrollTo(0, returnToY.current));
    }
  }, [itemsLoaded]);

  useEffect(() => {
    const remember = () => {
      menuScrollYRef.current = window.scrollY;
    };
    window.addEventListener("scroll", remember, { passive: true });
    return () => window.removeEventListener("scroll", remember);
  }, [menuScrollYRef]);

  // The app bar's bag also opens Review order
  const reviewOrder = () => {
    reviewOpenedFromMenuRef.current = true;
    router.push(REVIEW_ORDER_PATH);
  };

  // Favourites and past orders, for customers with an account
  const myItems = useMyItems({ onError: setError });
  const { canSave, favourites, toggleFavourite } = myItems;

  // Without a session the info box below already shows `error` (e.g. not seated), so the
  // red alert only shows it once the guest is seated
  const displayError = sessionId ? error : null;

  return (
    <CustomerPageContainer>
      {/* Not seated: only the info message below shows, without a menu title */}
      {sessionId && (
        <Typography variant="h5" component="h1" sx={{ fontWeight: 700, mt: { xs: 3, sm: 2 }, mb: 3 }}>
          All You Can Eat Menu
        </Typography>
      )}

      {/* Error Alert */}
      {displayError && (
        <Alert
          severity="error"
          sx={{ mb: 2 }}
          onClose={() => setError(null)}
        >
          {displayError}
        </Alert>
      )}

      {sessionId ? (
        <MenuBrowser
          categories={categories}
          menuItems={menuItems}
          loading={!itemsLoaded && !error}
          ready={Boolean(menuId && itemsLoaded)}
          myItems={myItems}
          // What's in the cart stays on screen whatever the search
          keepItem={(item, categoryId) => quantities[categoryId]?.[item.item_id] > 0}
          notices={
            <>
              {selectedBillId && billRequested && (
                <Alert severity="info" sx={{ mb: 2 }}>
                  {BILL_REQUESTED_MESSAGE}
                </Alert>
              )}

              {needsBill && (
                <Alert
                  severity="warning"
                  sx={{ mb: 2 }}
                  action={
                    <Button color="inherit" size="small" onClick={() => setBillDialogOpen(true)}>
                      Choose bill
                    </Button>
                  }
                >
                  Choose your bill before placing an order.
                </Alert>
              )}

              {myBill && !selectedBillId && (
                <Alert severity="info" sx={{ mb: 2 }}>
                  {myBill.bill_name} is {String(myBill.status).toLowerCase()}, so no more
                  orders can be added. Ask your server if you&apos;d like anything else.
                </Alert>
              )}
            </>
          }
          renderItem={(item, category) => (
            // Records how long the card is on screen (browsing behavior analytics)
            <TrackedItem
              key={`item-${category.category_id}-${item.item_id}`}
              itemId={item.item_id}
              sessionId={sessionId}
            >
              <MenuItemCard
                item={item}
                categoryId={category.category_id}
                quantity={
                  quantities[category.category_id]?.[item.item_id] ||
                  0
                }
                onQuantityChange={changeQuantity}
                atLimit={
                  limitStatus(category.category_id, item.item_id).atLimit
                }
                // The bill's been asked for, so nothing more can be ordered onto it
                showQuantityControls={!billRequested}
                favourite={favourites.has(item.item_id)}
                onToggleFavourite={canSave ? toggleFavourite : undefined}
              />
            </TrackedItem>
          )}
        />
      ) : (
        // Only once the check is done; before that sessionId is just not known yet
        sessionChecked && (
          <Box sx={{ width: "100%", mb: 3 }}>
            <Alert
              severity="info"
              // Customers with an account can look over the menu before they're seated
              action={
                canSave && (
                  <Button color="inherit" size="small" onClick={() => router.push(BROWSE_PATH)}>
                    Browse the menu
                  </Button>
                )
              }
            >
              {error || NOT_SEATED_MESSAGE}
            </Alert>
          </Box>
        )
      )}

      {/* Ordering is over once the bill's been asked for */}
      {!billRequested && <ReviewOrderButton count={cartCount} onClick={reviewOrder} />}

      <BillChoiceDialog open={billDialogOpen} onClose={() => setBillDialogOpen(false)} />
    </CustomerPageContainer>
  );
};

export default FullMenu;
