import React, { useState, useEffect, useCallback, useRef } from "react";
import { Box, Alert, CircularProgress } from "@mui/material";
import api from "@/config/api";
import storage from "@/utils/storage";
import { useOrder } from "@/contexts/OrderContext";
import { Header } from "./OrderHeader";
import { TabBar } from "./OrderTabBar";
import { OrderGrid } from "./OrderGrid";
import { OrderDetailDialog } from "./OrderDetailDialog";

const OrderDashboard = () => {
  const [orders, setOrders] = useState([]);
  const [loading, setLoading] = useState(true);
  const [selectedOrderId, setSelectedOrderId] = useState(null);
  const [editMode, setEditMode] = useState({});
  const [editedQuantities, setEditedQuantities] = useState({});
  const [currentTab, setCurrentTab] = useState("PENDING");
  const [pollingActive, setPollingActive] = useState(true);
  const pollingRef = useRef();

  const {
    approveOrder,
    completeOrder,
    removeOrderItem,
    completeOrderItem,
    updateOrderItem,
    actionError,
    isLoading,
    clearActionError,
  } = useOrder();

  const fetchAllOrders = useCallback(async () => {
    try {
      setLoading(true);

      // Get the selected location from storage
      const locationId = storage.get("branch-location");

      // Build params object with locationId if it exists
      const params = locationId ? { locationId } : {};

      const response = await api.get("/Order/all_orders", { params });

      if (!Array.isArray(response.data) || response.data.length === 0) {
        setOrders([]);
        return;
      }
      const mappedOrders = response.data.map((order) => ({
        orderId: order.orderId,
        sessionId: order.sessionId,
        billId: order.billId,
        userOid: order.userOid,
        userName: order.userName,
        status: order.status,
        itemTotal: order.items?.length || 0,
        orderTotal:
          order.items?.reduce(
            (sum, item) => sum + item.priceAtTime * item.quantity,
            0
          ) || 0,
        createdAt: order.createdAt,
        completedAt: order.completedAt,
        tableNumbers: order.tableNumbers || [],
        items: Array.isArray(order.items)
          ? order.items.map((item) => ({
              orderItemId: item.orderItemId,
              itemId: item.itemId,
              name: item.name,
              quantity: item.quantity,
              price: item.priceAtTime,
              isAddOn: item.isAddOn,
              status: item.status,
            }))
          : [],
      }));

      setOrders(mappedOrders);
    } catch (err) {
      console.error("Error fetching orders:", err);
      setOrders([]);
    } finally {
      setLoading(false);
    }
  }, []);

  // Start polling only when not editing or dialog open
  useEffect(() => {
    if (!pollingActive) return;
    
    // Initial fetch
    fetchAllOrders();
    
    // Set up polling interval
    pollingRef.current = setInterval(() => {
      fetchAllOrders();
    }, 30000);
    
    return () => clearInterval(pollingRef.current);
  }, [pollingActive, fetchAllOrders]);

  // When dialog opens or edit mode starts, pause polling
  // This is to prevent refreshes while user is doing something
  useEffect(() => {
    if (selectedOrderId || Object.values(editMode).some(Boolean)) {
      setPollingActive(false);
      clearInterval(pollingRef.current);
    } else {
      setPollingActive(true);
    }
  }, [selectedOrderId, editMode]);

  const selectedOrder = orders.find((o) => o.orderId === selectedOrderId);

  const toggleEditMode = (orderId) => {
    setEditMode((prev) => ({
      ...prev,
      [orderId]: !prev[orderId],
    }));

    if (!editMode[orderId]) {
      const order = orders.find((o) => o.orderId === orderId);
      const quantities = {};
      order?.items.forEach((item) => {
        quantities[`${orderId}-${item.itemId}`] = item.quantity;
      });
      setEditedQuantities((prev) => ({ ...prev, ...quantities }));
    }
  };

  const updateQuantity = (orderId, itemId, delta) => {
    const key = `${orderId}-${itemId}`;
    setEditedQuantities((prev) => {
      const currentQty = prev[key] || 0;
      const newQty = Math.max(1, currentQty + delta);
      return { ...prev, [key]: newQty };
    });
  };

  const handleMarkItemDelivered = async (orderId, orderItemId) => {
    await completeOrderItem(orderId, orderItemId);
    await fetchAllOrders();
  };

  const handelCancelItem = async (orderId, itemId) => {
    const order = orders.find((o) => o.orderId === orderId);
    const item = order?.items.find((i) => i.itemId === itemId);

    if (!item?.orderItemId) return;

    await updateOrderItem(orderId, item.orderItemId, { status: "Cancelled" });
    await fetchAllOrders();
  };

  const handleUndoCancelItem = async (orderId, itemId) => {
    const order = orders.find((o) => o.orderId === orderId);
    const item = order?.items.find((i) => i.itemId === itemId);
    if (!item?.orderItemId) return;

    await updateOrderItem(orderId, item.orderItemId, { status: "Pending" });
    await fetchAllOrders();
  };

  const handleApproveOrder = async (orderId) => {
    await approveOrder(orderId);
    setSelectedOrderId(null);
    await fetchAllOrders();
  };

  const handleSaveChanges = async (orderId) => {
    const order = orders.find((o) => o.orderId === orderId);
    
    for (const item of order.items) {
      const key = `${orderId}-${item.itemId}`;
      const newQuantity = editedQuantities[key];

      // If item is cancelled, set quantity to 0
      if (item.status?.toUpperCase() === "CANCELLED") {
        if (item.quantity !== 0) {
          await updateOrderItem(orderId, item.orderItemId, { quantity: 0 });
        }
        continue;
      }

      // For non-cancelled items, update quantity if changed
      if (newQuantity !== undefined && newQuantity !== item.quantity) {
        await updateOrderItem(orderId, item.orderItemId, {
          quantity: newQuantity,
        });
      }
    }

    setEditMode((prev) => ({ ...prev, [orderId]: false }));
    await fetchAllOrders();
  };

  const handleSaveAndApprove = async (orderId) => {
    // First save the quantities
    const order = orders.find((o) => o.orderId === orderId);
    // Edit the quantities and update if there are changes
    for (const item of order.items) {
      const key = `${orderId}-${item.itemId}`;
      const newQuantity = editedQuantities[key];

      // If item is cancelled, set quantity to 0 and skip status update
      if (item.status === "Cancelled") {
        if (item.quantity !== 0) {
          await updateOrderItem(orderId, item.orderItemId, { quantity: 0 });
        }
        continue;
      }

      // For non-cancelled items, update quantity if changed
      if (newQuantity !== undefined && newQuantity !== item.quantity) {
        await updateOrderItem(orderId, item.orderItemId, {
          quantity: newQuantity,
        });
      }
    }

    // Then approve the order
    await approveOrder(orderId);

    // Close dialog and reset edit mode
    setEditMode((prev) => ({ ...prev, [orderId]: false }));
    setSelectedOrderId(null);
    await fetchAllOrders();
  };

  const handleMarkAllDelivered = async (orderId) => {
    await completeOrder(orderId);
    setSelectedOrderId(null);
    await fetchAllOrders();
  };

  const getStatusColor = (status) => {
    if (!status) return "default";
    switch (status.toString().toUpperCase()) {
      case "PENDING":
        return "warning";
      case "APPROVED":
      case "PROCESSING":
        return "info";
      case "DELIVERED":
        return "success";
      case "CANCELLED":
        return "error";
      default:
        return "default";
    }
  };

  const filteredOrders = orders.filter((order) => {
    const status = order.status?.toString().toUpperCase();
    if (currentTab === "PENDING") return status === "PENDING";
    if (currentTab === "PROCESSING")
      return status === "PROCESSING" || status === "APPROVED";
    if (currentTab === "DELIVERED") return status === "DELIVERED";
    return false;
  });
  console.log(orders)
  const tabCounts = {
    PENDING: orders.filter((o) => o.status.toUpperCase() === "PENDING").length,
    PROCESSING: orders.filter(
      (o) => o.status.toUpperCase() === "PROCESSING" || o.status === "APPROVED"
    ).length,
    DELIVERED: orders.filter((o) => o.status.toUpperCase() === "DELIVERED").length,
  };
  console.log(tabCounts)
  if (loading) {
    return (
      <Box
        display="flex"
        justifyContent="center"
        alignItems="center"
        minHeight="100vh"
      >
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box sx={{ minHeight: "100vh", bgcolor: "background.default" }}>
      <Header onRefresh={fetchAllOrders} isLoading={isLoading} />

      {actionError && (
        <Box sx={{ p: 2 }}>
          <Alert severity="error" onClose={clearActionError}>
            {actionError}
          </Alert>
        </Box>
      )}

      <TabBar
        currentTab={currentTab}
        onChange={setCurrentTab}
        counts={tabCounts}
      />

      <OrderGrid orders={filteredOrders} onOrderClick={setSelectedOrderId} />

      <OrderDetailDialog
        open={!!selectedOrderId}
        onClose={() => {
          setSelectedOrderId(null);
          setEditMode((prev) => ({ ...prev, [selectedOrderId]: false }));
        }}
        order={selectedOrder}
        currentTab={currentTab}
        editMode={editMode[selectedOrderId]}
        editedQuantities={editedQuantities}
        isLoading={isLoading}
        onToggleEdit={toggleEditMode}
        onQuantityChange={updateQuantity}
        onCancelItem={handelCancelItem}
        onUndoCancelItem={handleUndoCancelItem}
        onMarkItemDelivered={handleMarkItemDelivered}
        onSaveChanges={handleSaveChanges}
        onSaveAndApprove={handleSaveAndApprove}
        onApprove={handleApproveOrder}
        onMarkAll={handleMarkAllDelivered}
        getStatusColor={getStatusColor}
      />
    </Box>
  );
};

export default OrderDashboard;
