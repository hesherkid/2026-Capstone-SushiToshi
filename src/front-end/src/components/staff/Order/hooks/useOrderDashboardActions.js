import { useOrder } from "@/contexts/OrderContext";
import { useOrderDashboard } from "../context/OrderDashboardContext";
import { ORDER_STATUS, normalizeStatus } from "./useOrderData";

// Order and order item actions for the grid rows and the detail dialog.
// API calls go through the app-wide OrderContext, which also shows success/error notifications.
export const useOrderDashboardActions = () => {
  const {
    approveOrder,
    completeOrder,
    updateOrderItem,
    completeOrderItem,
    isLoading: isSaving,
    actionError,
    clearActionError,
  } = useOrder();
  const { fetchOrders, closeOrder, stopEditing, editedQuantities } =
    useOrderDashboard();

  const refresh = () => fetchOrders({ silent: true });

  // Cancelled items are zeroed; the rest are saved only if their quantity changed
  const saveQuantities = async (order) => {
    for (const item of order.items) {
      if (normalizeStatus(item.status) === ORDER_STATUS.CANCELLED) {
        if (item.quantity !== 0) {
          await updateOrderItem(order.orderId, item.orderItemId, { quantity: 0 });
        }
        continue;
      }

      const quantity = editedQuantities[item.itemId];
      if (quantity !== undefined && quantity !== item.quantity) {
        await updateOrderItem(order.orderId, item.orderItemId, { quantity });
      }
    }
  };

  return {
    isSaving,
    actionError,
    clearActionError,

    approve: async (order) => {
      await approveOrder(order.orderId);
      closeOrder();
      await refresh();
    },

    markAllDelivered: async (order) => {
      await completeOrder(order.orderId);
      closeOrder();
      await refresh();
    },

    saveChanges: async (order) => {
      await saveQuantities(order);
      stopEditing();
      await refresh();
    },

    saveAndApprove: async (order) => {
      await saveQuantities(order);
      await approveOrder(order.orderId);
      closeOrder();
      await refresh();
    },

    markItemDelivered: async (order, item) => {
      await completeOrderItem(order.orderId, item.orderItemId);
      await refresh();
    },

    cancelItem: async (order, item) => {
      await updateOrderItem(order.orderId, item.orderItemId, {
        status: "Cancelled",
      });
      await refresh();
    },

    undoCancelItem: async (order, item) => {
      await updateOrderItem(order.orderId, item.orderItemId, {
        status: "Pending",
      });
      await refresh();
    },
  };
};
