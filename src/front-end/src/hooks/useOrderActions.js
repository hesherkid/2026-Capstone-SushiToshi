import { useState, useCallback, useMemo } from "react";
import { createApiWrapper } from "@/utils/apiWrapper";
import { useNotification } from "@/contexts/NotificationContext";
import orderService from "@/services/orderService";

/**
 * Hook for managing order operations with loading and error states
 * @param {Function} onSuccess - Callback to run on successful operations
 * @returns {Object} Order action functions and state
 */
export const useOrderActions = (onSuccess) => {
  const [actionError, setActionError] = useState(null);
  const [isLoading, setIsLoading] = useState(false);

  const { notifySuccess, notifyError } = useNotification();

  const clearActionError = useCallback(() => {
    setActionError(null);
  }, []);

  // Centralized error handler
  const handleError = useCallback(
    (error, context) => {
      const errorMessage = error.userMessage || `Failed to ${context}`;

      setActionError(errorMessage);
      notifyError(errorMessage);
    },
    [notifyError],
  );

  // Wrapper that handles all the repetitive loading/error logic
  const apiWrapper = useMemo(
    () =>
      createApiWrapper({
        setIsLoading,
        setActionError,
        handleError,
        onSuccess,
      }),
    [handleError, onSuccess],
  );

  // ORDER CREATION

  const createOrder = useCallback(
    async (sessionId, billId, requestByOid = null, requestByName = null) => {
      return apiWrapper(
        "createOrder",
        async () => {
          const result = await orderService.createOrder({
            session_Id: sessionId,
            bill_Id: billId,
            request_by_Oid: requestByOid, // optional
            request_by_Name: requestByName,
          });
          return result;
        },
        { triggerSuccess: false },
      );
    },
    [apiWrapper], // Removed notifySuccess to avoid duplicate notifications. Create order in full-menu triggers its own success message with Alert.
  );

  // ORDER MANAGEMENT

  const approveOrder = useCallback(
    async (orderId) => {
      return apiWrapper(
        "approveOrder",
        async () => {
          const result = await orderService.approveOrder(orderId);
          notifySuccess("Order approved successfully");
          return result;
        },
        { triggerSuccess: true },
      );
    },
    [apiWrapper, notifySuccess],
  );

  const cancelOrder = useCallback(
    async (orderId) => {
      return apiWrapper(
        "cancelOrder",
        async () => {
          const result = await orderService.cancelOrder(orderId);
          notifySuccess("Order cancelled successfully");
          return result;
        },
        { triggerSuccess: true },
      );
    },
    [apiWrapper, notifySuccess],
  );

  const completeOrder = useCallback(
    async (orderId) => {
      return apiWrapper(
        "completeOrder",
        async () => {
          const result = await orderService.completeOrder(orderId);
          notifySuccess("Order completed successfully");
          return result;
        },
        { triggerSuccess: true },
      );
    },
    [apiWrapper, notifySuccess],
  );

  // ORDER ITEMS MANAGEMENT

  const addOrderItems = useCallback(
    async (orderId, items) => {
      return apiWrapper(
        "addOrderItems",
        async () => {
          const result = await orderService.addOrderItems(orderId, items);
          // No toast: Review order shows its own "Order sent!" screen with the items
          return result;
        },
        { triggerSuccess: true },
      );
    },
    [apiWrapper],
  );

  const updateOrderItem = useCallback(
    async (orderId, itemId, updateData) => {
      return apiWrapper(
        "updateOrderItem",
        async () => {
          const result = await orderService.updateOrderItem(
            orderId,
            itemId,
            updateData,
          );
          const itemName = updateData.name || result?.name || "Order item";
          notifySuccess(`${itemName} updated successfully`);
          return result;
        },
        { triggerSuccess: true },
      );
    },
    [apiWrapper, notifySuccess],
  );

  const completeOrderItem = useCallback(
    async (orderId, itemId, itemName = null) => {
      return apiWrapper(
        "completeOrderItem",
        async () => {
          const result = await orderService.completeOrderItem(orderId, itemId);
          const name = itemName || result?.name || "Order item";
          notifySuccess(`${name} marked as complete`);
          return result;
        },
        { triggerSuccess: true },
      );
    },
    [apiWrapper, notifySuccess],
  );

  const removeOrderItem = useCallback(
    async (orderId, orderItemId, itemName = null) => {
      return apiWrapper(
        "removeOrderItem",
        async () => {
          const result = await orderService.removeOrderItem(
            orderId,
            orderItemId,
          );
          const name = itemName || result?.name || "Order item";
          notifySuccess(`${name} removed successfully`);
          return result;
        },
        { triggerSuccess: true },
      );
    },
    [apiWrapper, notifySuccess],
  );

  // ORDER RETRIEVAL

  // TODO: Might need to comment this out later in front and backend as there is no filterization which will cause performance issues as it calls ALL orders without filter.
  const getAllOrders = useCallback(async () => {
    return apiWrapper("getAllOrders", async () => orderService.getAllOrders(), {
      defaultReturn: [],
    });
  }, [apiWrapper]);

  const getAllOrdersByLocation = useCallback(
    async (locationId = null) => {
      return apiWrapper(
        "getAllOrdersByLocation",
        async () => orderService.getAllOrdersByLocation(locationId),
        { defaultReturn: [] },
      );
    },
    [apiWrapper],
  );

  const getOrderById = useCallback(
    async (orderId) => {
      return apiWrapper("getOrderById", async () =>
        orderService.getOrderById(orderId),
      );
    },
    [apiWrapper],
  );

  const getOrdersBySession = useCallback(
    async (sessionId, billId = null) => {
      // supports filtering by bill when splitting
      return apiWrapper(
        "getOrdersBySession",
        async () => orderService.getOrdersBySession(sessionId, billId),
        { defaultReturn: [] },
      );
    },
    [apiWrapper],
  );

  const getActiveSessionOrders = useCallback(
    async (billId = null) => {
      return apiWrapper(
        "getActiveSessionOrders",
        async () => orderService.getActiveSessionOrders(billId),
        { defaultReturn: [] },
      );
    },
    [apiWrapper],
  );

  return {
    // State
    actionError,
    isLoading,
    clearActionError,

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
    getAllOrders,
    getAllOrdersByLocation,
    getOrderById,
    getOrdersBySession,
    getActiveSessionOrders,
  };
};

export default useOrderActions;
