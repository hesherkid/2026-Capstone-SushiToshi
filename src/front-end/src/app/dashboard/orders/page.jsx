"use client";
import React, { useState, useEffect, useRef } from "react";
import { Alert, Box, Button, Chip, Divider, Paper, Skeleton, Tab, Tabs, Typography } from "@mui/material";
import { useRouter } from "next/navigation";
import api from "@/config/api";
import { fetchMySessionId, getMyUserId, NOT_SEATED_MESSAGE } from "@/services/tableSession";
import { setPageLoading } from "@/utils/pageLoading";
import CustomerPageContainer from "@/components/customer/CustomerPageContainer";
import MaterialSymbol from "@/components/common/MaterialSymbol";
import useAutoRefresh from "@/hooks/useAutoRefresh";
import { formatCurrency, formatTime } from "@/components/staff/SessionDashboard/utils/sessionHelpers";

const MENU_PATH = "/menu/full-menu";

// "table": every order at the table, so when one person orders for everyone the rest can see it.
// "mine": only orders this person placed.
const VIEWS = { TABLE: "table", MINE: "mine" };

// Where an order or item is at, in the customer's words. Sections show in this order
const STATUSES = {
  PENDING: { section: "Sent to the kitchen", chip: "Ordered", color: "warning" },
  PROCESSING: { section: "Being prepared", chip: "Preparing", color: "info" },
  DELIVERED: { section: "Served", chip: "Served", color: "success" },
  CANCELLED: { section: "Cancelled", chip: "Cancelled", color: "default" },
};
const STATUS_ORDER = Object.keys(STATUSES);

const statusKey = (status) => {
  const key = String(status || "PENDING").toUpperCase();
  return STATUSES[key] ? key : "PENDING";
};

// Add-ons and over-limit extras show what they cost; all you can eat items are included
const itemPrice = (item) => {
  // "Mine" sends price; the table view sends priceAtTime
  const price = Number(item.price ?? item.priceAtTime);
  if (!price) return null;
  const quantity = item.quantity || 1;
  return quantity > 1
    ? `${formatCurrency(price * quantity)} (${formatCurrency(price)} each)`
    : formatCurrency(price);
};

const OrdersSkeleton = () => (
  <Box aria-label="Loading orders" role="status">
    <Skeleton variant="rounded" height={180} sx={{ borderRadius: 3, mb: 1.5 }} />
    <Skeleton variant="rounded" height={140} sx={{ borderRadius: 3 }} />
  </Box>
);

// The guest's orders as the kitchen works through them, with every item's status in view.
// Grouped by where each order is at; "Whole table" also shows orders others placed
const OrdersPage = () => {
  const router = useRouter();
  const [groupedOrders, setGroupedOrders] = useState({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [sessionId, setSessionId] = useState(null);
  const [view, setView] = useState(VIEWS.MINE);
  // Set when a background refresh fails; the orders on screen are from lastUpdated
  const [refreshFailed, setRefreshFailed] = useState(false);
  const [lastUpdated, setLastUpdated] = useState(null);
  const latestRequest = useRef(0);
  // Set once the page has picked its opening tab (see fetchOrders)
  const openingTabChosen = useRef(false);
  const myUserId = getMyUserId();

  useEffect(() => {
    const getActiveSession = async () => {
      try {
        // Ask the server (guests included) rather than trusting a saved session_id
        const mySessionId = await fetchMySessionId();
        if (mySessionId) {
          setSessionId({ session_id: mySessionId });
        } else {
          setError(NOT_SEATED_MESSAGE);
          setLoading(false);
        }
      } catch (err) {
        console.error("Error fetching session:", err);
        setError(
          typeof err.response?.data?.detail === "string"
            ? err.response.data.detail
            : "Error fetching session"
        );
        setLoading(false);
      }
    };

    getActiveSession();
  }, []);

  // quiet: a background refresh. No spinner, and if it fails the orders already shown stay up
  const fetchOrders = async ({ quiet = false } = {}) => {
    // Only the latest request may update the page, so a slow refresh for the old tab
    // can't overwrite the tab the guest just switched to
    const requestId = ++latestRequest.current;
    const isStale = () => requestId !== latestRequest.current;
    // Moving to the other tab: it loads next, so the spinner stays on and nothing flashes
    let switchingTab = false;

    try {
      if (!quiet) {
        setLoading(true);
        setError(null);
      }

      // Whole table: every order in the session. Mine: the server finds this person's own orders.
      const response =
        view === VIEWS.TABLE
          ? await api.get(`/Order/session/${sessionId.session_id}`)
          : await api.get("/Order/active-session/orders");
      if (isStale()) return;

      const orders = response.data || [];

      // Opens on My orders, but when someone else ordered for this person the table's
      // orders are what they want. Only on arrival, so it never flips the tab under them
      if (!openingTabChosen.current && !quiet) {
        openingTabChosen.current = true;
        if (view === VIEWS.MINE && orders.length === 0) {
          switchingTab = true;
          setView(VIEWS.TABLE);
          return;
        }
      }

      const grouped = orders.reduce((acc, order) => {
        const status = statusKey(order.status);
        if (!acc[status]) acc[status] = [];
        acc[status].push(order);
        return acc;
      }, {});

      setGroupedOrders(grouped);
      setError(null);
      setRefreshFailed(false);
      setLastUpdated(new Date());
    } catch (err) {
      if (isStale()) return;

      // "No active session": staff closed the table. Not an error; fetchMySessionId sees the
      // session is gone and ends the visit, which shows the thank-you page
      if (err?.response?.status === 404 && view === VIEWS.MINE) {
        try {
          if (!(await fetchMySessionId())) return;
        } catch {
          // Couldn't check; handled as a failed load below
        }
      }

      console.error("Error fetching orders:", err);
      // A failed background refresh leaves the orders up and just says they may be out of date
      if (quiet) {
        setRefreshFailed(true);
        return;
      }
      const msg =
        typeof err?.response?.data?.message === "string"
          ? err.response.data.message
          : typeof err?.response?.data === "string"
            ? err.response.data
            : "Error loading orders";
      setError(msg);
      setGroupedOrders({});
    } finally {
      if (!quiet && !isStale() && !switchingTab) setLoading(false);
    }
  };

  // The app bar's spinner is the page's only loading indicator
  useEffect(() => {
    setPageLoading(loading);
  }, [loading]);
  useEffect(() => () => setPageLoading(false), []);

  useEffect(() => {
    // The table view needs the session; it's ready after fetchMySessionId
    if (!sessionId) return;
    // The other tab's orders aren't these, so don't leave them up while these load
    setGroupedOrders({});
    setRefreshFailed(false);
    fetchOrders();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [view, sessionId]);

  // Order status changes as the kitchen works through it
  useAutoRefresh(() => fetchOrders({ quiet: true }), { enabled: !!sessionId });

  // Other guests' orders show only the bill they're on, never who placed them
  const orderLabel = (order) => {
    const parts = [];
    if (myUserId && order.userId === myUserId) parts.push("You");
    if (order.billName) parts.push(order.billName);
    return parts.join(" · ");
  };

  // One card per order, laid out open so each item's status shows without tapping into anything
  const renderOrderCard = (order) => {
    const items = order.items || [];
    const itemCount = items.reduce((sum, item) => sum + (item.quantity || 0), 0);
    const placedAt = formatTime(order.createdAt);
    const label = view === VIEWS.TABLE ? orderLabel(order) : "";

    return (
      <Paper
        key={order.orderId}
        component="section"
        aria-label={`Order #${order.orderId}`}
        variant="outlined"
        sx={{ borderRadius: 3, overflow: "hidden", textAlign: "left" }}
      >
        <Box
          sx={{
            display: "flex",
            alignItems: "flex-start",
            justifyContent: "space-between",
            gap: 2,
            px: 2,
            py: 1.75,
          }}
        >
          <Box sx={{ minWidth: 0 }}>
            <Typography component="h3" sx={{ fontWeight: 700, lineHeight: 1.3 }}>
              Order #{order.orderId}
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25 }}>
              {[placedAt, `${itemCount} ${itemCount === 1 ? "item" : "items"}`]
                .filter(Boolean)
                .join(" · ")}
            </Typography>
          </Box>
          {label && (
            <Typography variant="body2" color="text.secondary" sx={{ textAlign: "right", flexShrink: 0 }}>
              {label}
            </Typography>
          )}
        </Box>

        {items.length > 0 ? (
          <Box component="ul" sx={{ listStyle: "none", p: 0, m: 0 }}>
            {items.map((item, index) => {
              const status = statusKey(item.status);
              const cancelled = status === "CANCELLED";
              const price = itemPrice(item);
              return (
                <Box
                  component="li"
                  key={item.order_item_id ?? item.orderItemId ?? `${order.orderId}-${item.item_id ?? item.itemId}-${index}`}
                >
                  <Divider />
                  <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, px: 2, py: 1.5 }}>
                    <Typography sx={{ fontWeight: 700, minWidth: 28 }}>{item.quantity}×</Typography>
                    <Box sx={{ flex: 1, minWidth: 0 }}>
                      <Typography
                        sx={{
                          fontWeight: 600,
                          lineHeight: 1.3,
                          overflowWrap: "anywhere",
                          textDecoration: cancelled ? "line-through" : "none",
                          color: cancelled ? "text.secondary" : "text.primary",
                        }}
                      >
                        {item.name || `Item #${item.item_id ?? item.itemId}`}
                      </Typography>
                      {/* Same wording as Review order */}
                      <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25 }}>
                        {price ? (
                          <>
                            Add-on{" "}
                            <Box component="span" sx={{ ml: 0.5, fontWeight: 600, color: "primary.main" }}>
                              {price}
                            </Box>
                          </>
                        ) : (
                          "Included"
                        )}
                      </Typography>
                    </Box>
                    <Chip
                      label={STATUSES[status].chip}
                      color={STATUSES[status].color}
                      size="small"
                      sx={{ fontWeight: 600, flexShrink: 0 }}
                    />
                  </Box>
                </Box>
              );
            })}
          </Box>
        ) : (
          <>
            <Divider />
            <Typography color="text.secondary" sx={{ px: 2, py: 1.5 }}>
              No items found for this order.
            </Typography>
          </>
        )}
      </Paper>
    );
  };

  const hasOrders = Object.keys(groupedOrders).length > 0;
  // Without a session there's nothing to show or switch between, so only the message stays up
  const sessionProblem = !sessionId && error;

  return (
    <CustomerPageContainer sx={{ maxWidth: 720 }}>
      <Typography variant="h5" component="h1" sx={{ fontWeight: 700, mt: { xs: 3, sm: 2 }, mb: 3 }}>
        {view === VIEWS.TABLE ? "Your table's orders" : "Your orders"}
      </Typography>

      {sessionProblem ? (
        <Alert severity={error === NOT_SEATED_MESSAGE ? "info" : "error"}>
          {typeof error === "string" ? error : "An unexpected error occurred"}
        </Alert>
      ) : (
        <>
          {/* Same underlined tabs as the Bill page might make a component in the future*/}
          <Tabs
            value={view}
            onChange={(_, next) => setView(next)}
            variant="scrollable"
            scrollButtons={false}
            aria-label="Which orders to show"
            sx={{ mb: 3, borderBottom: 1, borderColor: "divider" }}
          >
            {[
              { value: VIEWS.MINE, label: "My orders" },
              { value: VIEWS.TABLE, label: "Whole table" },
            ].map(({ value, label }) => (
              <Tab
                key={value}
                value={value}
                label={label}
                sx={{ textTransform: "none", fontWeight: 600, fontSize: "0.95rem" }}
              />
            ))}
          </Tabs>

          {error && (
            <Alert
              severity="error"
              sx={{ mb: 3 }}
              action={
                <Button color="inherit" size="small" onClick={() => fetchOrders()}>
                  Retry
                </Button>
              }
            >
              {typeof error === "string" ? error : "An unexpected error occurred"}
            </Alert>
          )}

          {refreshFailed && hasOrders && (
            <Typography variant="body2" color="text.secondary" role="status" sx={{ mb: 2 }}>
              Couldn&apos;t refresh
              {lastUpdated ? ` — showing orders as of ${formatTime(lastUpdated.toISOString())}` : ""}
              . Trying again shortly.
            </Typography>
          )}

          {!hasOrders ? (
            loading ? (
              <OrdersSkeleton />
            ) : (
              !error && (
                // ---- Nothing ordered yet ----
                <Box sx={{ textAlign: "center", py: 6 }}>
                  <MaterialSymbol name="room_service" size={56} sx={{ color: "text.disabled" }} />
                  <Typography variant="h6" component="h2" sx={{ mt: 1.5 }}>
                    No orders yet
                  </Typography>
                  <Typography color="text.secondary" sx={{ mt: 0.5, mb: 3 }}>
                    {view === VIEWS.MINE
                      ? "You haven't ordered anything yet."
                      : "No one at your table has ordered yet."}
                  </Typography>
                  <Box sx={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 1 }}>
                    <Button variant="contained" onClick={() => router.push(MENU_PATH)}>
                      Browse the menu
                    </Button>
                    {/* Someone else at the table may have ordered for them */}
                    {view === VIEWS.MINE && (
                      <Button onClick={() => setView(VIEWS.TABLE)} sx={{ textTransform: "none", fontWeight: 600 }}>
                        See the whole table&apos;s orders
                      </Button>
                    )}
                  </Box>
                </Box>
              )
            )
          ) : (
            <>
              {STATUS_ORDER.map((status) => {
                const orders = groupedOrders[status];
                if (!orders?.length) return null;

                return (
                  <Box key={status} sx={{ mb: 4 }}>
                    {/* Section heading with its count, like the menu's categories */}
                    <Typography variant="h6" component="h2" sx={{ fontWeight: 600, mb: 1.5, textAlign: "left" }}>
                      {STATUSES[status].section}{" "}
                      <Typography
                        component="span"
                        sx={{ fontSize: "0.75rem", fontWeight: 400, color: "text.secondary", ml: 0.5 }}
                      >
                        {orders.length} {orders.length === 1 ? "order" : "orders"}
                      </Typography>
                    </Typography>
                    <Box sx={{ display: "flex", flexDirection: "column", gap: 1.5 }}>
                      {orders.map(renderOrderCard)}
                    </Box>
                  </Box>
                );
              })}

              <Button
                variant="contained"
                fullWidth
                size="large"
                onClick={() => router.push(MENU_PATH)}
                startIcon={<MaterialSymbol name="add" size={22} />}
                sx={{ mb: 3, minHeight: 52, borderRadius: 999, fontSize: "1rem" }}
              >
                Order more
              </Button>
            </>
          )}
        </>
      )}
    </CustomerPageContainer>
  );
};

export default OrdersPage;
