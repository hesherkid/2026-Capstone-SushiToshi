"use client";
import React, { useState, useEffect } from "react";
import {
  Accordion,
  AccordionSummary,
  AccordionDetails,
  Typography,
  Box,
  Alert,
  Chip,
  IconButton,
  Button,
  ToggleButton,
  ToggleButtonGroup,
} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import { Plus } from "lucide-react";
import { useRouter } from "next/navigation";
import api from "@/config/api";
import { fetchMySessionId, getMyUserId, NOT_SEATED_MESSAGE } from "@/services/tableSession";
import { setPageLoading } from "@/utils/pageLoading";
import CustomerPageContainer from "@/components/customer/CustomerPageContainer";

// "table": every order at the table, so when one person orders for everyone the rest can see it.
// "mine": only orders this person placed.
const VIEWS = { TABLE: "table", MINE: "mine" };

const OrdersAccordion = () => {
  const router = useRouter();
  const [groupedOrders, setGroupedOrders] = useState({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [sessionId, setSessionId] = useState(null);
  const [view, setView] = useState(VIEWS.MINE);
  const myUserId = getMyUserId();

  const isGuest = () => typeof window !== "undefined" && localStorage.getItem("guest") === "true";

  const statusOrder = ["PENDING", "PROCESSING", "DELIVERED", "CANCELLED"];
  const statusColors = {
    PENDING: "#ff9800",
    PROCESSING: "#2196f3",
    DELIVERED: "#4caf50",
    CANCELLED: "#f44336",
  };

  // TODO: Double check API endpoints and modify router push paths if needed.
  // Fetch active session ID
  useEffect(() => {
    const getActiveSession = async (billId = "") => {

      const apiClient = api;

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

const fetchOrders = async () => {

  const apiClient = api;

  try {
    setLoading(true);
    setError(null);

    // Whole table: every order in the session. Mine: the server finds this person's own orders.
    const response =
      view === VIEWS.TABLE
        ? await apiClient.get(`/Order/session/${sessionId.session_id}`)
        : await apiClient.get("/Order/active-session/orders");


    //Check if there are no orders
    const orders = response.data || [];
    if (!orders.length) {
      setGroupedOrders({});
      setLoading(false);
      return;
    }

    //Sort each type of order into groups (Pending, Delivered, Processing and Cancelled)
    const grouped = orders.reduce((acc, order) => {
      const status = String(order.status || "PENDING").toUpperCase();
      if (!acc[status]) acc[status] = [];
      acc[status].push(order);
      return acc;
    }, {});

    setGroupedOrders(grouped);
    setLoading(false);
  } catch (err) {
    console.error("Error fetching orders:", err);
    const msg =
      typeof err?.response?.data?.message === "string"
        ? err.response.data.message
        : typeof err?.response?.data === "string"
        ? err.response.data
        : "Error loading orders";
    setError(msg);
    setGroupedOrders({});
    setLoading(false);
  }
};

  // TODO: Check if this is needed
  const handleNewOrder = (event) => {
    event.preventDefault();
    router.push("/menu/full-menu");
  };
  
  // The app bar's spinner is the page's only loading indicator
  useEffect(() => {
    setPageLoading(loading);
  }, [loading]);
  useEffect(() => () => setPageLoading(false), []);

  useEffect(() => {
    // The table view needs the session; it's ready after fetchMySessionId
    if (!sessionId) return;
    fetchOrders();
    const interval = setInterval(fetchOrders, 30000);
    return () => clearInterval(interval);
  }, [view, sessionId]);

  // Other guests' orders show only the bill they're on, never who placed them
  const orderLabel = (order) => {
    const parts = [];
    if (myUserId && order.userId === myUserId) parts.push("You");
    if (order.billName) parts.push(order.billName);
    return parts.join(" · ");
  };

  const renderOrderAccordion = (order, groupStatus) => (
    <Accordion
      key={order.orderId}
      sx={{
        mb: 1,
        border: groupStatus === "PENDING" ? "1px solid #ff9800" : "none",
      }}
    >
      <AccordionSummary
        expandIcon={<ExpandMoreIcon />}
        sx={{
          backgroundColor:
            groupStatus === "PENDING"
              ? "rgba(255, 152, 0, 0.05)"
              : "rgba(0, 0, 0, 0.03)",
        }}
      >
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            width: "100%",
            pr: 2,
          }}
        >
          {/* Item count sits under the order number, leaving room for the name beside it */}
          <Box>
            <Typography>Order #{order.orderId}</Typography>
            {order.items && (
              <Typography variant="body2" color="textSecondary">
                ({order.items.length} items)
              </Typography>
            )}
          </Box>
          {view === VIEWS.TABLE && (
            <Typography variant="body2" color="text.secondary">
              {orderLabel(order)}
            </Typography>
          )}
        </Box>
      </AccordionSummary>
      <AccordionDetails>
        {order.items && order.items.length > 0 ? (
          <Box component="ul" sx={{ listStyle: "none", p: 0, m: 0 }}>
            {order.items.map((item) => (
              <Box
                component="li"
                key={item.order_item_id ?? item.orderItemId ?? `${order.orderId}-${item.item_id ?? item.itemId}-${index}`}
                sx={{
                  py: 1.5,
                  borderBottom: "1px solid rgba(0, 0, 0, 0.12)",
                  "&:last-child": { borderBottom: "none" },
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "center",
                }}
              >
                <Box>
                  <Typography variant="body1" align="left">
                    {item.name || `Item #${item.item_id}`} - Quantity:{" "}
                    {item.quantity}
                  </Typography>
                  <Typography
                    variant="body2"
                    color="textSecondary"
                    align="left"
                  >
                    {(() => {
                      // "Mine" sends price; the table view sends priceAtTime
                      const price = item.price ?? item.priceAtTime;
                      if (price === 0) return "Included item";
                      return price ? `Price: $${Number(price).toFixed(2)}` : "Price unavailable";
                    })()}
                  </Typography>
                </Box>

                <Chip
                  label={item.status}
                  size="small"
                  sx={{
                    backgroundColor: statusColors[item.status.toUpperCase()],
                    color: "white",
                  }}
                />
              </Box>
            ))}
          </Box>
        ) : (
          <Typography color="textSecondary">
            No items found for this order.
          </Typography>
        )}
      </AccordionDetails>
    </Accordion>
  );

  if (error) {
    return (
      <Box p={2}>
        <Alert severity="error">
          {typeof error === "string" ? error : "An unexpected error occurred"}
        </Alert>
      </Box>
    );
  }

  return (
    <CustomerPageContainer>
      <Typography
        variant="h4"
        component="h1"
        sx={{ mt: 4, mb: 4, textAlign: "left", width: "100%", fontWeight: 200 }}
      >
        Table Orders
      </Typography>

      {/* Tabs on the left, Order More on the right of the same row */}
      <Box
        sx={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          gap: 2,
          mb: 3,
        }}
      >
        <ToggleButtonGroup
          exclusive
          size="small"
          value={view}
          onChange={(event, next) => next && setView(next)}
          aria-label="Which orders to show"
        >
          <ToggleButton value={VIEWS.MINE}>My orders</ToggleButton>
          <ToggleButton value={VIEWS.TABLE}>Whole table</ToggleButton>
        </ToggleButtonGroup>

        <Button
          variant="contained"
          startIcon={<Plus size={20} />}
          onClick={() => router.push("/menu/full-menu")}
          sx={{
            borderRadius: 2,
            px: { xs: 1.5, sm: 2.5 },
            py: 1.25,
            fontSize: { xs: "0.875rem", sm: "1rem" },
            fontWeight: 600,
            textTransform: "none",
            whiteSpace: "nowrap",
            boxShadow: "none",
            "&:hover": {
              boxShadow: 1,
            },
          }}
        >
          Order More
        </Button>
      </Box>

      {/* While loading, keep the current list; the app bar shows the spinner */}
      {Object.keys(groupedOrders).length === 0 ? (
        !loading && (
        <Alert severity="info">
          {view === VIEWS.MINE
            ? "You haven't ordered anything yet."
            : "No one at your table has ordered yet."}
        </Alert>
        )
      ) : (
        statusOrder.map((status) => {
          if (!groupedOrders[status]?.length) return null;

          return (
            <Box key={status} sx={{ mb: 4 }}>
              <Box
                sx={{
                  display: "flex",
                  alignItems: "center",
                  gap: 2,
                  mb: 2,
                }}
              >
                <Typography variant="h6">
                  {status.charAt(0) + status.slice(1).toLowerCase()} Orders
                </Typography>
                <Chip
                  label={groupedOrders[status].length}
                  size="small"
                  sx={{
                    backgroundColor: statusColors[status],
                    color: "white",
                    fontWeight: "bold",
                  }}
                />
              </Box>
              {groupedOrders[status].map((order) =>
                renderOrderAccordion(order, status)
              )}
            </Box>
          );
        })
      )}
    </CustomerPageContainer>
  );
};

export default OrdersAccordion;
