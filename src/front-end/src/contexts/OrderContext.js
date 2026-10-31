import {
  createContext,
  useContext,
  useState,
  useCallback,
  useEffect,
} from "react";
import { useOrderActions } from "@/hooks/useOrderActions";

const OrderContext = createContext(null);

export const useOrder = () => {
  const context = useContext(OrderContext);
  if (!context) {
    throw new Error("useOrder must be used within OrderProvider");
  }
  return context;
};

export const OrderProvider = ({ children }) => {
  const [orders, setOrders] = useState([]);
  const [groupedOrders, setGroupedOrders] = useState({});
  const [selectedOrder, setSelectedOrder] = useState(null);
  const [updateTrigger, setUpdateTrigger] = useState(0);
  const [selectedBillId, setSelectedBillId] = useState("");

  const statusOrder = ["PENDING", "PROCESSING", "DELIVERED", "CANCELLED"];
  const statusColors = {
    PENDING: "#ff9800",
    PROCESSING: "#2196f3",
    DELIVERED: "#4caf50",
    CANCELLED: "#f44336",
  };

  const triggerUpdate = useCallback(() => {
    setUpdateTrigger((prev) => prev + 1);
  }, []);

  // useOrderActions now handles notifications internally
  const {
    // State
    actionError,
    isLoading,
    clearActionError,

    // Order Management
    createOrder: baseCreateOrder,
    approveOrder: baseApproveOrder,
    cancelOrder: baseCancelOrder,
    completeOrder: baseCompleteOrder,

    // Order Items
    addOrderItems: baseAddOrderItems,
    updateOrderItem: baseUpdateOrderItem,
    completeOrderItem: baseCompleteOrderItem,
    removeOrderItem: baseRemoveOrderItem,

    // Order Retrieval
    // getAllOrders,
    getOrderById,
    getOrdersBySession,
    getActiveSessionOrders,
  } = useOrderActions(triggerUpdate);

  // Group orders by status
  const groupOrdersByStatus = useCallback((ordersList) => {
    const grouped = ordersList.reduce((acc, order) => {
      const status = String(order.status || "PENDING").toUpperCase();
      if (!acc[status]) acc[status] = [];
      acc[status].push(order);
      return acc;
    }, {});
    setGroupedOrders(grouped);
  }, []);

  // Wrapped actions that update local state
  const createOrder = useCallback(
    async (sessionId, billId) => {
      const result = await baseCreateOrder(sessionId, billId);
      if (result) {
        setOrders((prev) => [...prev, result]);
        groupOrdersByStatus([...orders, result]);
        return result;
      }
      return null;
    },
    [baseCreateOrder, orders, groupOrdersByStatus],
  );

  const approveOrder = useCallback(
    async (orderId) => {
      const result = await baseApproveOrder(orderId);
      if (result) {
        setOrders((prev) =>
          prev.map((order) => (order.orderId === orderId ? result : order)),
        );
        groupOrdersByStatus(
          orders.map((order) => (order.orderId === orderId ? result : order)),
        );
        if (selectedOrder?.orderId === orderId) {
          setSelectedOrder(result);
        }
        return result;
      }
      return null;
    },
    [baseApproveOrder, orders, selectedOrder, groupOrdersByStatus],
  );

  const cancelOrder = useCallback(
    async (orderId) => {
      const result = await baseCancelOrder(orderId);
      if (result) {
        setOrders((prev) =>
          prev.map((order) => (order.orderId === orderId ? result : order)),
        );
        groupOrdersByStatus(
          orders.map((order) => (order.orderId === orderId ? result : order)),
        );
        if (selectedOrder?.orderId === orderId) {
          setSelectedOrder(result);
        }
        return result;
      }
      return null;
    },
    [baseCancelOrder, orders, selectedOrder, groupOrdersByStatus],
  );

  const completeOrder = useCallback(
    async (orderId) => {
      const result = await baseCompleteOrder(orderId);
      if (result) {
        setOrders((prev) =>
          prev.map((order) => (order.orderId === orderId ? result : order)),
        );
        groupOrdersByStatus(
          orders.map((order) => (order.orderId === orderId ? result : order)),
        );
        if (selectedOrder?.orderId === orderId) {
          setSelectedOrder(result);
        }
        return result;
      }
      return null;
    },
    [baseCompleteOrder, orders, selectedOrder, groupOrdersByStatus],
  );

  const addOrderItems = useCallback(
    async (orderId, items) => {
      const result = await baseAddOrderItems(orderId, items);
      if (result) {
        // Refresh the order to get updated items
        const updatedOrder = await getOrderById(orderId);
        if (updatedOrder) {
          setOrders((prev) =>
            prev.map((order) =>
              order.orderId === orderId ? updatedOrder : order,
            ),
          );
          groupOrdersByStatus(
            orders.map((order) =>
              order.orderId === orderId ? updatedOrder : order,
            ),
          );
          if (selectedOrder?.orderId === orderId) {
            setSelectedOrder(updatedOrder);
          }
        }
        return result;
      }
      return null;
    },
    [
      baseAddOrderItems,
      getOrderById,
      orders,
      selectedOrder,
      groupOrdersByStatus,
    ],
  );

  const updateOrderItem = useCallback(
    async (orderId, itemId, updateData) => {
      const result = await baseUpdateOrderItem(orderId, itemId, updateData);
      if (result) {
        // Refresh the order to get updated items
        const updatedOrder = await getOrderById(orderId);
        if (updatedOrder) {
          setOrders((prev) =>
            prev.map((order) =>
              order.orderId === orderId ? updatedOrder : order,
            ),
          );
          groupOrdersByStatus(
            orders.map((order) =>
              order.orderId === orderId ? updatedOrder : order,
            ),
          );
          if (selectedOrder?.orderId === orderId) {
            setSelectedOrder(updatedOrder);
          }
        }
        return result;
      }
      return null;
    },
    [
      baseUpdateOrderItem,
      getOrderById,
      orders,
      selectedOrder,
      groupOrdersByStatus,
    ],
  );

  const completeOrderItem = useCallback(
    async (orderId, itemId) => {
      const result = await baseCompleteOrderItem(orderId, itemId);
      if (result) {
        // Refresh the order to get updated items
        const updatedOrder = await getOrderById(orderId);
        if (updatedOrder) {
          setOrders((prev) =>
            prev.map((order) =>
              order.orderId === orderId ? updatedOrder : order,
            ),
          );
          groupOrdersByStatus(
            orders.map((order) =>
              order.orderId === orderId ? updatedOrder : order,
            ),
          );
          if (selectedOrder?.orderId === orderId) {
            setSelectedOrder(updatedOrder);
          }
        }
        return result;
      }
      return null;
    },
    [
      baseCompleteOrderItem,
      getOrderById,
      orders,
      selectedOrder,
      groupOrdersByStatus,
    ],
  );

  const removeOrderItem = useCallback(
    async (orderId, orderItemId) => {
      const result = await baseRemoveOrderItem(orderId, orderItemId);
      if (result) {
        // Refresh the order to get updated items
        const updatedOrder = await getOrderById(orderId);
        if (updatedOrder) {
          setOrders((prev) =>
            prev.map((order) =>
              order.orderId === orderId ? updatedOrder : order,
            ),
          );
          groupOrdersByStatus(
            orders.map((order) =>
              order.orderId === orderId ? updatedOrder : order,
            ),
          );
          if (selectedOrder?.orderId === orderId) {
            setSelectedOrder(updatedOrder);
          }
        }
        return result;
      }
      return null;
    },
    [
      baseRemoveOrderItem,
      getOrderById,
      orders,
      selectedOrder,
      groupOrdersByStatus,
    ],
  );

  // Fetch and set orders
  const fetchOrdersBySession = useCallback(
    async (sessionId) => {
      const result = await getOrdersBySession(sessionId);
      if (result) {
        setOrders(result);
        groupOrdersByStatus(result);
      }
      return result;
    },
    [getOrdersBySession, groupOrdersByStatus],
  );

  const fetchActiveSessionOrders = useCallback(
    async (billId = null) => {
      const result = await getActiveSessionOrders(billId);
      if (result) {
        setOrders(result);
        groupOrdersByStatus(result);
      }
      return result;
    },
    [getActiveSessionOrders, groupOrdersByStatus],
  );

  const selectOrder = useCallback(
    async (orderId) => {
      if (!orderId) {
        setSelectedOrder(null);
        return null;
      }

      const result = await getOrderById(orderId);
      if (result) {
        setSelectedOrder(result);
      }
      return result;
    },
    [getOrderById],
  );

  const setBillFilter = useCallback((billId) => {
    setSelectedBillId(billId);
  }, []);

  const value = {
    // State
    orders,
    groupedOrders,
    selectedOrder,
    selectedBillId,
    actionError,
    isLoading,
    updateTrigger,

    // Constants
    statusOrder,
    statusColors,

    // Order Management
    createOrder,
    approveOrder,
    cancelOrder,
    completeOrder,

    // Order Items
    addOrderItems,
    updateOrderItem,
    completeOrderItem,
    removeOrderItem,

    // Order Retrieval
    // fetchAllOrders,
    getOrderById,
    fetchOrdersBySession,
    fetchActiveSessionOrders,

    // Selection
    selectOrder,
    setBillFilter,

    // Utilities
    clearActionError,
    triggerUpdate,
    groupOrdersByStatus,
  };

  return (
    <OrderContext.Provider value={value}>{children}</OrderContext.Provider>
  );
};
