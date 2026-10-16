import { createContext, useContext, useState } from "react";
import { useOrderData } from "../hooks/useOrderData";

const OrderDashboardContext = createContext();

export const useOrderDashboard = () => {
  const context = useContext(OrderDashboardContext);
  if (!context) {
    throw new Error(
      "useOrderDashboard must be used within an OrderDashboardProvider"
    );
  }
  return context;
};

export const OrderDashboardProvider = ({ children }) => {
  const [selectedOrderId, setSelectedOrderId] = useState(null);
  const [isEditing, setIsEditing] = useState(false);
  // itemId -> quantity for the order being edited
  const [editedQuantities, setEditedQuantities] = useState({});

  // Polling pauses while an order is open so it isn't refreshed under the user
  const orderData = useOrderData({ paused: selectedOrderId !== null });

  const selectedOrder =
    orderData.orders.find((o) => o.orderId === selectedOrderId) ?? null;

  const openOrder = (orderId) => {
    setSelectedOrderId(orderId);
    setIsEditing(false);
    setEditedQuantities({});
  };

  const closeOrder = () => {
    setSelectedOrderId(null);
    setIsEditing(false);
    setEditedQuantities({});
  };

  const startEditing = () => {
    setEditedQuantities(
      Object.fromEntries(
        (selectedOrder?.items || []).map((item) => [item.itemId, item.quantity])
      )
    );
    setIsEditing(true);
  };

  const stopEditing = () => {
    setIsEditing(false);
    setEditedQuantities({});
  };

  const changeQuantity = (itemId, delta) => {
    setEditedQuantities((prev) => ({
      ...prev,
      [itemId]: Math.max(1, (prev[itemId] ?? 1) + delta),
    }));
  };

  return (
    <OrderDashboardContext.Provider
      value={{
        ...orderData,
        selectedOrder,
        openOrder,
        closeOrder,
        isEditing,
        editedQuantities,
        startEditing,
        stopEditing,
        changeQuantity,
      }}
    >
      {children}
    </OrderDashboardContext.Provider>
  );
};
